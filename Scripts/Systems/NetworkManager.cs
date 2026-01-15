using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
    // Configuration
    [Export] public int Port = 7777;
    [Export] public PackedScene PlayerScene; // Drag PlayerController.tscn here

    private ENetMultiplayerPeer _peer;
    private Dictionary<long, PlayerController> _players = new Dictionary<long, PlayerController>();

    [Signal] public delegate void PlayerConnectedEventHandler(long id, PlayerController player);
    [Signal] public delegate void PlayerDisconnectedEventHandler(long id);

    public static NetworkManager Instance { get; private set; }

    public PlayerController GetPlayer(long id)
    {
        if (_players.ContainsKey(id)) return _players[id];
        return null;
    }

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
            // Prevent this node from being destroyed when loading new scenes
            // Note: If added via Autoload in Project Settings, this is automatic (it's in /root/).
            // If instantiated manually, we might need to be careful. 
            // We'll assume strict Autoload usage.
        }
        else
        {
            QueueFree();
            return;
        }

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public void HostGame()
    {
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(Port, 4); // Max 4 players
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        GD.Print("Server started on port " + Port);

        // Server immediately loads the game scene
        CallDeferred(nameof(LoadGameScene));

        SetupUPnP();
        PrintIPs();
    }

    private void SetupUPnP()
    {
        var upnp = new Upnp();
        int err = upnp.Discover();

        if (err != (int)Error.Ok)
        {
            GD.PrintErr($"UPnP Discovery Failed! Error: {err}");
            return;
        }

        if (upnp.GetGateway() != null && upnp.GetGateway().IsValidGateway())
        {
            GD.Print($"UPnP Discovery Successful! Gateway: {upnp.GetGateway().QueryExternalAddress()}");

            // Try to map the port (UDP is usually preferred for Games, but ENet can use UDP)
            // Godot's ENet uses UDP.
            upnp.AddPortMapping(Port, Port, "Godot_Game_UDP", "UDP");
            upnp.AddPortMapping(Port, Port, "Godot_Game_TCP", "TCP"); // Just in case

            GD.Print($"UPnP Port Mapping Attempted for {Port}");
        }
        else
        {
            GD.PrintErr("UPnP: No valid gateway found.");
        }
    }

    private void PrintIPs()
    {
        GD.Print("--- Available IP Addresses ---");
        foreach (var ip in IP.GetLocalAddresses())
        {
            if (ip.Contains(".")) // Simple filter for IPv4
            {
                GD.Print($"  {ip}");
            }
        }
        GD.Print("------------------------------");
    }

    public void JoinGame(string ip)
    {
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateClient(ip, Port);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create client: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        GD.Print($"Connecting to {ip}:{Port}...");
    }

    private void LoadGameScene()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Levels/TerrainTest.tscn");
        GD.Print("Loading TerrainTest...");
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"Peer Connected: {id}");

        // Ensure we only spawn on server
        if (!Multiplayer.IsServer()) return;

        // We need to find the root node (Level) to spawn into.
        // Assuming the Main Scene (Level) is the current scene, or we can fallback to Root
        Node root = GetTree().CurrentScene;

        // If the current scene is clearly a Level (has MultiplayerSpawner), go for it.
        // Safety check: Don't spawn if we are in Main Menu
        if (root.Name != "MainMenu")
        {
            SpawnPlayer(id, root);
        }
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Peer Disconnected: {id}");
        if (_players.ContainsKey(id))
        {
            // If we tracked specific data, clean it up
            _players.Remove(id);
        }
        EmitSignal(SignalName.PlayerDisconnected, id);
    }

    private void OnConnectedToServer()
    {
        GD.Print("Connected to Server!");
        // Client loads game scene upon successful connection
        CallDeferred(nameof(LoadGameScene));
    }

    private void OnConnectionFailed()
    {
        GD.Print("Connection Failed!");
    }

    private void OnServerDisconnected()
    {
        GD.Print("Server Disconnected!");
        _peer = null;
        // Cleanup?
    }


    public async void LevelLoaded(Node root)
    {
        if (!Multiplayer.IsServer()) return;

        GD.Print("NetworkManager: Level Loaded, Spawning Players...");

        // Wait for a physics frame to ensure collisions (CSG) are baked
        await ToSignal(GetTree(), "physics_frame");
        await ToSignal(GetTree(), "physics_frame"); // Double frame for safety

        // Spawn for Self (Host)
        SpawnPlayer(1, root);

        // Spawn for connected clients
        foreach (var id in Multiplayer.GetPeers())
        {
            SpawnPlayer(id, root);
        }
    }

    private void SpawnPlayer(long id, Node root)
    {
        if (PlayerScene == null)
        {
            GD.Print("PlayerScene not assigned. Loading default 'res://Scenes/Entities/Player.tscn'.");
            PlayerScene = GD.Load<PackedScene>("res://Scenes/Entities/Player.tscn");

            if (PlayerScene == null)
            {
                GD.PrintErr("CRITICAL: Could not load Player.tscn from default path!");
                return;
            }
        }

        var player = PlayerScene.Instantiate<PlayerController>();
        player.Name = id.ToString(); // Unique Name is crucial for replication

        // Sequential Indexing: 0, 1, 2, 3 based on join order
        // _players contains already spawned players. For the FIRST one (Host), count is 0.
        // But we add to _players AFTER this. So let's check count.
        int newIndex = _players.Count;
        player.SetPlayerIndex(newIndex);

        player.SetMultiplayerAuthority((int)id); // Authority assignment

        // FIND SPAWN POSITION FIRST (before AddChild so spawn packet has correct position)
        // Try SpawnPoint first, then TeeBox, then VisualTee
        Node3D spawnPoint = root.GetNodeOrNull<Node3D>("SpawnPoint");
        if (spawnPoint == null) spawnPoint = root.FindChild("TeeBox", true, false) as Node3D;
        if (spawnPoint == null) spawnPoint = root.FindChild("VisualTee", true, false) as Node3D;
        if (spawnPoint == null) spawnPoint = root.FindChild("Tee", true, false) as Node3D;

        Vector3 spawnPos = Vector3.Zero;
        Vector3 spawnRot = Vector3.Zero;

        if (spawnPoint != null)
        {
            // Add safety height and random offset to prevent stacking
            float rngX = (float)GD.RandRange(-1.0, 1.0);
            float rngZ = (float)GD.RandRange(-1.0, 1.0);
            spawnPos = spawnPoint.GlobalPosition + new Vector3(rngX, 2.0f, rngZ);
            spawnRot = spawnPoint.GlobalRotation;
            GD.Print($"NetworkManager: Spawn position calculated as {spawnPos} from {spawnPoint.Name}");
        }
        else
        {
            GD.PrintErr("NetworkManager: No spawn point found (tried SpawnPoint, TeeBox, VisualTee, Tee)! Spawning at origin.");
        }

        // Use LOCAL Position/Rotation (works before AddChild, GlobalPosition requires being in tree)
        // Since player is added directly to scene root, local = global
        player.Position = spawnPos;
        player.Rotation = spawnRot;

        // Add to scene at root (Walking sim style)
        // If there's a specific "Players" node, use it, otherwise root.
        var playersNode = root.GetNodeOrNull("Players") ?? root;
        playersNode.AddChild(player, true); // force_readable_name = true

        _players[id] = player;
        // EmitSignal(SignalName.PlayerConnected, id, player);

        GD.Print($"Spawned Player for ID: {id} at {player.GlobalPosition}");
    }
}
