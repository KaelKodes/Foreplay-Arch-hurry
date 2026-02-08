using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Archery;

namespace Archery;

public partial class NetworkManager : Node
{
	// Configuration
	[Export] public int Port = 7777;
	[Export] public PackedScene PlayerScene; // Drag PlayerController.tscn here
	[Export] public bool IsTargetable = false;
	[Export] public MobaTeam Team = MobaTeam.None;
	public MobaTeam SelectedTeam = MobaTeam.None;

	private ENetMultiplayerPeer _peer;
	private Dictionary<long, PlayerController> _players = new Dictionary<long, PlayerController>();
	private Dictionary<long, string> _playerClassMap = new();
	private HashSet<long> _spawnedPlayerIds = new();
	private bool _isLevelLoaded = false;


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

		// Ensure PlayerScene is loaded (Script-only Autoloads don't preserve exports)
        if (PlayerScene == null)
        {
            GD.Print("NetworkManager: PlayerScene was null, loading from res://Scenes/Entities/Player.tscn");
            PlayerScene = GD.Load<PackedScene>("res://Scenes/Entities/Player.tscn");
        }
    }

    public void HostGame()
    {
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(Port, 8); // Max 8 players
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        GD.Print("Server started on port " + Port);

        // Server immediately loads the lobby scene
        CallDeferred(nameof(LoadLobbyScene));

        SetupUPnP();
        PrintIPs();
    }

    /// <summary>
    /// Hosts the game without reloading the scene. Used for hosting from an active session.
    /// </summary>
    public void HostActiveGame()
    {
        if (Multiplayer.MultiplayerPeer != null)
        {
            GD.PrintErr("NetworkManager: Already hosting or connected.");
            return;
        }

        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(Port, 8);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        GD.Print("Server started in active session on port " + Port);

        // Notify systems that we are now the server
        LevelLoaded(GetTree().CurrentScene);

        SetupUPnP();
        PrintIPs();
    }

    private void SetupUPnP()
    {
        try
        {
            var upnp = new Upnp();
            int err = upnp.Discover();

            if (err != (int)Error.Ok)
            {
                GD.PrintErr($"UPnP Discovery Failed! Error Code: {err}");
                return;
            }

            var gateway = upnp.GetGateway();
            if (gateway != null && gateway.IsValidGateway())
            {
                string extAddress = "";
                try
                {
                    extAddress = gateway.QueryExternalAddress();
                    GD.Print($"UPnP Discovery Successful! Gateway: {extAddress}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"UPnP: Failed to query external address: {ex.Message}");
                }

                // Try to map the port
                upnp.AddPortMapping(Port, Port, "Godot_Game_UDP", "UDP");
                upnp.AddPortMapping(Port, Port, "Godot_Game_TCP", "TCP");

                GD.Print($"UPnP Port Mapping Attempted for {Port}");
            }
            else
            {
                GD.PrintErr("UPnP: No valid gateway found.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"UPnP: Unhandled exception during setup: {ex.Message}");
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

    public void LoadLobbyScene()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Menus/Lobby.tscn");
        GD.Print("Loading Lobby...");
    }

    private void LoadGameScene()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Levels/MOBA1.tscn");
        GD.Print("Loading MOBA1...");
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"Peer Connected: {id}");

        // REMOVED: Do NOT spawn immediately. Wait for Client to load map and send NotifyClientReady.
        // This prevents void spawning.
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Peer Disconnected: {id}");
        if (_players.ContainsKey(id))
        {
            var player = _players[id];
            if (player != null && IsInstanceValid(player))
            {
                GD.Print($"NetworkManager: Removing disconnected player object {player.Name}");
                player.QueueFree();
            }
            _players.Remove(id);
        }
        EmitSignal(SignalName.PlayerDisconnected, id);
    }

    private void OnConnectedToServer()
    {
        GD.Print("Connected to Server!");
        // Client loads lobby scene upon successful connection
        CallDeferred(nameof(LoadLobbyScene));
    }

    private void OnConnectionFailed()
    {
        GD.Print("Connection Failed!");
        ReturnToMainMenu();
    }

    private void OnServerDisconnected()
    {
        GD.Print("Server Disconnected!");
        ReturnToMainMenu();
    }

    public void ReturnToMainMenu()
    {
        GD.Print("NetworkManager: Returning to Main Menu...");

        // Reset Peer
        if (_peer != null)
        {
            _peer.Close();
            GD.Print("NetworkManager: Peer closed.");
        }

        Multiplayer.MultiplayerPeer = null;
        _peer = null;
        _players.Clear();
        _spawnedPlayerIds.Clear();
        _isLevelLoaded = false;

        // Change Scene
        GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");
    }

    // Called by ArcherySystem._Ready() on both Client and Server
    private string _currentLevelPath = "";

    public async void LevelLoaded(Node root)
    {
        string levelPath = root.GetPath().ToString();
        if (_isLevelLoaded && _currentLevelPath == levelPath)
        {
            GD.Print($"NetworkManager: Level {root.Name} already initialized, skipping.");
            return;
        }

        GD.Print($"NetworkManager: Level {root.Name} loaded.");
        _isLevelLoaded = true;
        _currentLevelPath = levelPath;

        // Clear spawned set when a new level loads to allow re-spawning
        _spawnedPlayerIds.Clear();

        GD.Print($"NetworkManager: Level Loaded ({root.Name}). Waiting for physics bake...");

        // Wait for physics frames to ensure collisions (CSG) are baked and ground is solid
        await ToSignal(GetTree(), "physics_frame");
        await ToSignal(GetTree(), "physics_frame");
        await ToSignal(GetTree(), "physics_frame");

        SetupObjectSpawner();
        SetupTerrainSpawner();

        if (Multiplayer.IsServer())
        {
			// If we are in the Lobby scene, we don't spawn players yet
			if (GetTree().CurrentScene.Name == "Lobby")
			{
				GD.Print("NetworkManager: Lobby loaded. Waiting for players to join...");
				LobbyManager.Instance.AddPlayer(1, "Host");
				return;
			}

			// If we are the Host, spawn ourselves immediately
			var lobbyData = LobbyManager.Instance.GetPlayers().FirstOrDefault(p => p.Id == 1);
			MobaTeam hostTeam = lobbyData?.Team ?? SelectedTeam;
			string hostClass = lobbyData?.ClassName ?? "Ranger";
			SpawnPlayer(1, hostTeam, hostClass);
		}
		else
		{
			if (GetTree().CurrentScene.Name == "Lobby")
			{
				GD.Print("NetworkManager: Client Lobby loaded.");
				return;
			}

			GD.Print($"NetworkManager: Client Ready. Sending Spawn Request...");
			var lobbyData = LobbyManager.Instance.GetPlayers().FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
			MobaTeam clientTeam = lobbyData?.Team ?? SelectedTeam;
			string clientClass = lobbyData?.ClassName ?? "Ranger";
			RpcId(1, nameof(NotifyClientReady), (int)clientTeam, clientClass);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void NotifyClientReady(int teamInt, string className)
	{
		// Executed on Server when Client says "I'm loaded!"
		if (!Multiplayer.IsServer()) return;

		MobaTeam team = (MobaTeam)teamInt;
		long senderId = Multiplayer.GetRemoteSenderId();
		GD.Print($"NetworkManager: Received ClientReady from ID {senderId} with Team {team}. Spawning...");

		// Safety check
		if (_players.ContainsKey(senderId))
		{
			GD.Print($"NetworkManager: Player {senderId} already exists. Ignoring duplicate spawn request.");
			return;
		}

		// Find root scene to spawn into
		Node root = GetTree().CurrentScene;
		SpawnPlayer(senderId, team, className);

		// Sync terrain data to this client
		SyncTerrainToClient(senderId);

		// Sync existing world objects to this client
		SyncWorldObjectsToClient(senderId);
	}

	private void SyncTerrainToClient(long clientId)
	{
		var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
		if (terrain != null)
		{
			GD.Print($"NetworkManager: Syncing Heightmap to client {clientId}...");
			float[] heights = terrain.GetFlattenedHeightData();
			int[] types = terrain.GetFlattenedTypeData();
			RpcId((int)clientId, nameof(NetSyncHeightmap), heights, types);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void NetSyncHeightmap(float[] heights, int[] types)
	{
		GD.Print("NetworkManager: Received Heightmap Sync from Server.");
		var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
		if (terrain != null)
		{
			terrain.SetFlattenedData(heights, types);
		}
		else
		{
			GD.PrintErr("NetworkManager: Could not find terrain for sync!");
		}
	}

	private void SyncWorldObjectsToClient(long clientId)
	{
		if (_worldObjectsContainer == null) return;

		GD.Print($"NetworkManager: Syncing {_worldObjectsContainer.GetChildCount()} world objects to client {clientId}...");

		foreach (Node child in _worldObjectsContainer.GetChildren())
		{
			if (child is InteractableObject io)
			{
				string resourcePath = io.ModelPath;
				if (string.IsNullOrEmpty(resourcePath) && !string.IsNullOrEmpty(io.SceneFilePath))
				{
					resourcePath = io.SceneFilePath;
				}
				if (string.IsNullOrEmpty(resourcePath)) continue;

				// Handle Monster species
				string species = "";
				if (io is Monsters monster)
				{
					species = monster.Species;
				}

				RpcId((int)clientId, nameof(NetSpawnExistingObject), resourcePath, io.GlobalPosition, io.GlobalRotation, io.Scale, species, io.Name);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void NetSpawnExistingObject(string resourcePath, Vector3 position, Vector3 rotation, Vector3 scale, string species, string nodeName)
	{
		GD.Print($"NetworkManager: Client spawning existing object {nodeName} from {resourcePath}");

		// Ensure container exists
		if (_worldObjectsContainer == null)
		{
			_worldObjectsContainer = GetTree().CurrentScene.GetNodeOrNull("WorldObjects");
			if (_worldObjectsContainer == null)
			{
				_worldObjectsContainer = new Node3D();
				_worldObjectsContainer.Name = "WorldObjects";
				GetTree().CurrentScene.AddChild(_worldObjectsContainer);
			}
		}

		// Check if we already have this object (avoid duplicates)
		if (_worldObjectsContainer.HasNode(nodeName))
		{
			GD.Print($"NetworkManager: Object {nodeName} already exists, skipping.");
			return;
		}

		// Directly instantiate (don't use spawner to avoid tracker issues)
        Node obj = null;

        if (resourcePath.EndsWith(".gltf") || resourcePath.EndsWith(".fbx") || resourcePath.EndsWith(".glb"))
        {
            if (!ResourceLoader.Exists(resourcePath))
            {
                GD.PrintErr($"NetworkManager: GLTF not found: {resourcePath}");
                return;
            }
            var scene = GD.Load<PackedScene>(resourcePath);
            if (scene == null) return;

            var model = scene.Instantiate();
            var wrapper = new InteractableObject();
            wrapper.Name = nodeName;
            wrapper.ObjectName = System.IO.Path.GetFileNameWithoutExtension(resourcePath);
            wrapper.ModelPath = resourcePath;
            wrapper.AddChild(model);

            wrapper.AddDynamicCollision();

            obj = wrapper;
        }
        else
        {
            // PackedScene (e.g. Monsters.tscn)
            string actualPath = resourcePath;
			int colonIndex = resourcePath.LastIndexOf(':');
            if (colonIndex > 4)
            {
                actualPath = resourcePath.Substring(0, colonIndex);
            }

            if (!ResourceLoader.Exists(actualPath))
            {
                GD.PrintErr($"NetworkManager: Scene not found: {actualPath}");
                return;
            }
            var scene = GD.Load<PackedScene>(actualPath);
            if (scene == null) return;

            var instance = scene.Instantiate();
            instance.Name = nodeName;

            if (instance is InteractableObject io)
            {
                io.ObjectName = System.IO.Path.GetFileNameWithoutExtension(actualPath);
                io.ModelPath = resourcePath;

                if (io is Monsters monster && !string.IsNullOrEmpty(species))
                {
                    monster.Species = species;
                }
            }
            obj = instance;
        }

        if (obj is Node3D n3d)
        {
            n3d.Position = position;
            n3d.Rotation = rotation;
            n3d.Scale = scale;
        }

        // Add to scene (NOT through spawner)
        _worldObjectsContainer.AddChild(obj);
        GD.Print($"NetworkManager: Successfully spawned existing object {nodeName}");
    }

    public void SpawnPlayer(long id, MobaTeam team, string className = "Warrior")
    {
        if (!_isLevelLoaded) return;

        // Anti-Duplicate Guard
        if (_spawnedPlayerIds.Contains(id))
        {
            GD.Print($"NetworkManager: Player {id} already spawned, skipping.");
            return;
        }

        if (PlayerScene == null)
        {
            GD.PrintErr("NetworkManager: PlayerScene is not set!");
            return;
        }

        _spawnedPlayerIds.Add(id);

        var player = PlayerScene.Instantiate<PlayerController>();
        player.Name = id.ToString(); // Unique Name is crucial for replication
        player.Team = team;
        player.SynchronizedModel = className;
        player.SetMultiplayerAuthority((int)id);

        GD.Print($"NetworkManager: Spawning player {id} on team {team} as {className}");
        // Sequential Indexing: 0, 1, 2, 3 based on join order
        int playerIndex = Multiplayer.GetPeers().Length;
        player.PlayerIndex = playerIndex;

        // Find spawn position
        Vector3 spawnPos = Vector3.Zero;
        Vector3 spawnRot = Vector3.Zero;

        // Try to find a standard spawn point
        Node3D spawnPoint = null;
        string[] spawnNames = { "SpawnPoint", "TeeBox", "VisualTee", "Tee" };
        foreach (var name in spawnNames)
        {
            spawnPoint = GetTree().CurrentScene.FindChild(name, true, false) as Node3D;
            if (spawnPoint != null) break;
        }

        if (spawnPoint != null)
        {
            float rngX = (float)GD.RandRange(-1.0, 1.0);
            float rngZ = (float)GD.RandRange(-1.0, 1.0);
            spawnPos = spawnPoint.GlobalPosition + new Vector3(rngX, 2.0f, rngZ);
            spawnRot = spawnPoint.GlobalRotation;
            GD.Print($"NetworkManager: Spawn position calculated as {spawnPos} from {spawnPoint.Name}");
        }
        else
        {
            // FALLBACK: Team-based spawn at creep locations from MobaGameManager
            if (MobaGameManager.Instance != null)
            {
                Vector3 teamSpawn = team == MobaTeam.Red ? MobaGameManager.Instance.RedSpawnPos : MobaGameManager.Instance.BlueSpawnPos;
                if (teamSpawn != Vector3.Zero)
                {
                    spawnPos = teamSpawn + new Vector3(id % 3 * 2f, 2.0f, (id / 3) * 2f);
                    GD.Print($"NetworkManager: No standard spawn point found. Spawning at {team} creep spawn: {teamSpawn}");
                }
                else
                {
                    GD.PrintErr("NetworkManager: No spawn point found AND MobaGameManager spawn pos is Zero! Spawning at origin.");
                }
            }
            else
            {
                GD.PrintErr("NetworkManager: No spawn point found and MobaGameManager not present! Spawning at origin.");
            }
        }

        player.Position = spawnPos;
        player.Rotation = spawnRot;

        // Add to Players container if it exists, otherwise root
        Node root = GetTree().CurrentScene;
        var playersNode = root.GetNodeOrNull("Players") ?? root;
        playersNode.AddChild(player, true);

        _players[id] = player;

        // Force Teleport (RPC) to ensure everyone agrees on position
        player.Rpc(nameof(PlayerController.NetTeleport), spawnPos, player.RotationDegrees);
        player.Rpc(nameof(PlayerController.NetSetPlayerIndex), playerIndex);

        GD.Print($"Spawned Player for ID: {id} at {spawnPos} (RPC Sent)");
    }

    // --- Dynamic Object Spawning ---
    private Node _worldObjectsContainer;
    private MultiplayerSpawner _objectSpawner;

    public void SetupObjectSpawner()
    {
        // Container for spawned objects
        _worldObjectsContainer = GetTree().CurrentScene.GetNodeOrNull("WorldObjects");
        if (_worldObjectsContainer == null)
        {
            _worldObjectsContainer = new Node3D();
            _worldObjectsContainer.Name = "WorldObjects";
            GetTree().CurrentScene.AddChild(_worldObjectsContainer);
        }

        // Clean up old spawner if it exists (e.g. scene reload)
        if (_objectSpawner != null)
        {
            _objectSpawner.QueueFree();
            _objectSpawner = null;
        }

        // Spawner - Add to SCENE, not NetworkManager, to ensure it cleans up with level?
        // Actually, Autoload is safer for logic, but Spawner needs to be same path on both?
		// Let's add it to NetworkManager (Autoload) but update SpawnPath.
		_objectSpawner = new MultiplayerSpawner();
		_objectSpawner.Name = "ObjectSpawner";
		AddChild(_objectSpawner); // Add to tree FIRST so it can resolve paths
		_objectSpawner.SpawnPath = _worldObjectsContainer.GetPath();
		_objectSpawner.SpawnFunction = new Callable(this, nameof(SpawnNetworkObject));

		GD.Print($"NetworkManager: ObjectSpawner setup complete. Target: {_objectSpawner.SpawnPath}");
	}

	private MultiplayerSpawner _terrainSpawner;
	public void SetupTerrainSpawner()
	{
		// Ensure CSG Combiner exists
		var root = GetTree().CurrentScene;
		var csgRoot = root.GetNodeOrNull<CsgCombiner3D>("TerrainCombiner");
		if (csgRoot == null)
		{
			csgRoot = new CsgCombiner3D();
			csgRoot.Name = "TerrainCombiner";
			csgRoot.UseCollision = true;
			root.AddChild(csgRoot);
			// Optionally setup bedrock/defaults here?
			// BuildManager logic handles defaults. We just need the container.
		}

		if (_terrainSpawner != null) { _terrainSpawner.QueueFree(); _terrainSpawner = null; }

		_terrainSpawner = new MultiplayerSpawner();
		_terrainSpawner.Name = "TerrainSpawner";
		AddChild(_terrainSpawner);
		_terrainSpawner.SpawnPath = csgRoot.GetPath();
		_terrainSpawner.SpawnFunction = new Callable(this, nameof(SpawnNetworkObject)); // Reuse same function

		GD.Print($"NetworkManager: TerrainSpawner setup complete. Target: {_terrainSpawner.SpawnPath}");
	}

	// RPC called by Client to request an object placement
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RequestSpawnObject(string resourcePath, Vector3 position, Vector3 rotation, Vector3 scale)
	{
		if (!Multiplayer.IsServer()) return;

		GD.Print($"[NetworkManager] RequestSpawnObject: {resourcePath} at {position}");

		if (string.IsNullOrEmpty(resourcePath))
		{
			GD.PrintErr("[NetworkManager] RequestSpawnObject: resourcePath is empty!");
			return;
		}

		// Data to pass to SpawnFunction (must be Variant-compatible)
		var data = new Godot.Collections.Dictionary
		{
			{ "path", resourcePath },
			{ "pos", position },
			{ "rot", rotation },
			{ "scale", scale }
		};

		// This triggers SpawnNetworkObject on Server, adds to tree, and replicates to Clients
		var node = _objectSpawner.Spawn(data);
		if (node == null)
		{
			GD.PrintErr($"[NetworkManager] Spawn FAILED for {resourcePath}");
		}
		else
		{
			GD.Print($"[NetworkManager] Spawn SUCCESS: {node.Name}");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RequestDeleteObject(string nodeName)
	{
		if (!Multiplayer.IsServer()) return;

		if (_worldObjectsContainer != null && _worldObjectsContainer.HasNode(nodeName))
		{
			var node = _worldObjectsContainer.GetNode(nodeName);
			GD.Print($"NetworkManager: Server deleting networked object {nodeName}");
			node.QueueFree();
		}
		else
		{
			GD.PrintErr($"NetworkManager: Server failed to delete {nodeName} - Node not found in WorldObjects.");
		}
	}

	// --- Terrain Sync ---

	// RPC called by Client to request a terrain bake
	// Supports both Heightmap (Deform) and CSG (Spawn)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RequestBakeTerrain(Godot.Collections.Array<Vector3> points, float elevation, int type)
	{
		if (!Multiplayer.IsServer()) return;

		GD.Print($"NetworkManager: RequestBakeTerrain received. Type: {type}, Elev: {elevation}, Pts: {points.Count}");

		// Check for Heightmap first using same logic as BuildManager
		var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
		// Or search group if name differs? BuildManager searches group.
		if (heightmap == null)
		{
			var terrains = GetTree().GetNodesInGroup("terrain");
			if (terrains.Count > 0 && terrains[0] is HeightmapTerrain ht) heightmap = ht;
		}

		if (heightmap != null)
		{
			// Case A: Heightmap -> Multicast Deform to everyone
			Rpc(nameof(NetDeformTerrain), points, elevation, type);
		}
		else
		{
			// Case B: CSG -> Spawn networked node via Spawner
			// Prepare data dictionary for SpawnFunction
			var data = new Godot.Collections.Dictionary
			{
				{ "type", "terrain" },
				{ "points", points },
				{ "elev", elevation },
				{ "tType", type },
				{ "pos", Vector3.Zero },
				{ "rot", Vector3.Zero },
				{ "scale", Vector3.One } // Dummy transforms, points are absolute/local to root
			};

			// Use TerrainSpawner to ensure it spawns as child of TerrainCombiner (for CSG)
			_terrainSpawner.Spawn(data);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void NetDeformTerrain(Godot.Collections.Array<Vector3> points, float elevation, int type)
	{
		// Execute on all clients
		GD.Print("NetworkManager: NetDeformTerrain executing locally...");

		var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
		if (heightmap == null)
		{
			var terrains = GetTree().GetNodesInGroup("terrain");
			if (terrains.Count > 0 && terrains[0] is HeightmapTerrain ht) heightmap = ht;
		}

		if (heightmap != null)
		{
			// Convert Godot Array to C# Array
			Vector3[] pts = new Vector3[points.Count];
			for (int i = 0; i < points.Count; i++) pts[i] = points[i];

			heightmap.DeformArea(pts, elevation, type);
		}
	}

	// The Spawn Function (run by Spawner)
	// Instantiates the object. NOTE: Must return the Node.
	private Node SpawnNetworkObject(Godot.Collections.Dictionary data)
	{
		string type = data.ContainsKey("type") ? (string)data["type"] : "object";
		Vector3 pos = (Vector3)data["pos"];
		Vector3 rot = (Vector3)data["rot"];
		Vector3 scale = (Vector3)data["scale"];

		Node obj = null;

		if (type == "terrain")
		{
			// Reconstruct CSG Terrain
			// Logic borrowed from BuildManager.BakeTerrain (CSG part)
			// But we need to make it a standalone node we can return.
			// BuildManager creates a SurveyedTerrain node.

			var pointsVariant = (Godot.Collections.Array<Vector3>)data["points"];
			float elevation = (float)data["elev"];
			int tType = (int)data["tType"];

			Vector3[] pointsToUse = new Vector3[pointsVariant.Count];
			for (int i = 0; i < pointsVariant.Count; i++) pointsToUse[i] = pointsVariant[i];

			var bakedNode = new SurveyedTerrain();
			bakedNode.Name = $"Terrain_{tType}_{Time.GetTicksMsec()}"; // Unique name
			bakedNode.Points = pointsToUse; // Requires SurveyedTerrain to have public setter? It does.
			bakedNode.TerrainType = tType;

			// Geometry construction logic (simplified from BuildManager)
			Vector3 centroid = Vector3.Zero;
			foreach (var p in pointsToUse) centroid += p;
			if (pointsToUse.Length > 0) centroid /= pointsToUse.Length;

			Vector2[] localPoly = new Vector2[pointsToUse.Length];
			for (int i = 0; i < pointsToUse.Length; i++)
				localPoly[i] = new Vector2(pointsToUse[i].X - centroid.X, pointsToUse[i].Z - centroid.Z);

			bakedNode.Polygon = localPoly;
			bakedNode.Mode = CsgPolygon3D.ModeEnum.Depth;
			bakedNode.RotationDegrees = new Vector3(90, 0, 0); // Local rotation for CSG
			bakedNode.UseCollision = true; // Wait, BuildManager toggles this?

			// Material
			var mat = new StandardMaterial3D();
			// Simple color mapping (should match BuildManager)
			switch (tType)
			{
				case 0: mat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f); break;
				case 1: mat.AlbedoColor = new Color(0.15f, 0.3f, 0.15f); break;
				case 2: mat.AlbedoColor = new Color(0.08f, 0.2f, 0.08f); break;
				case 3: mat.AlbedoColor = new Color(0.1f, 0.6f, 0.1f); break;
				case 4: mat.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); break;
				case 5: mat.AlbedoColor = new Color(0.1f, 0.3f, 0.8f); break;
			}
			bakedNode.MaterialOverride = mat;

			// Depth & Operation
			if (elevation >= 0)
			{
				bakedNode.Operation = CsgShape3D.OperationEnum.Union;
				bakedNode.Depth = 0.1f + elevation;
				// Position will be set later by Spawner using returned obj?
				// Wait, Spawner sets Transform if 'pos' is passed?
				// But CSG logic sets GlobalPosition based on centroid.
				// We should set pos = centroid in our data?
				// OR we set it here and ignore Spawner's override?
                // Spawner overrides obj properties? No, custom spawn func returns node, Spawner adds it.
                // Check documentation: Spawner does NOT auto-sync transform unless MultiplayerSynchronizer is used?
                // OR spawn() data is just for init.

				// Let's set Transform here explicitly.
				pos = new Vector3(centroid.X, 0.1f, centroid.Z);
			}
			else
			{
				float depth = Mathf.Abs(elevation);
				bakedNode.Operation = CsgShape3D.OperationEnum.Subtraction;
				bakedNode.Depth = depth;
				pos = new Vector3(centroid.X, 0.1f - depth, centroid.Z);

				// Note: Filler mesh logic for water/sand omitted for brevity/complexity in Spawn function.
				// Ideally should be a proper configured scene.
			}

			obj = bakedNode;
		}
		else
		{
			// OBJECT Spawn Logic (Original)
			string path = (string)data["path"];
			// Logic similar to MainHUDController selection
			if (path.EndsWith(".gltf") || path.EndsWith(".fbx") || path.EndsWith(".glb"))
			{
				// GLTF Direct Load
				if (!ResourceLoader.Exists(path))
				{
					GD.PrintErr($"NetworkManager: RESOURCE NOT FOUND at '{path}'. This client cannot spawn the object. Ensure assets are identical.");
					return null;
				}
				var scene = GD.Load<PackedScene>(path);
				if (scene == null)
				{
					GD.PrintErr($"NetworkManager: Failed to load GLTF at {path}");
					return null;
				}
				var model = scene.Instantiate();
				var wrapper = new InteractableObject();
				wrapper.Name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + Time.GetTicksMsec();
				wrapper.ObjectName = System.IO.Path.GetFileNameWithoutExtension(path);
				wrapper.ModelPath = path; // Preserve it for persistence
				wrapper.AddChild(model);

				// Collision handled dynamically
				wrapper.AddDynamicCollision();

				obj = wrapper;
			}
			else
			{
				// PackedScene Load
				string actualPath = path;
				string species = "";
				int colonIndex = path.LastIndexOf(':');
				// Check if the colon is not the one in 'res://' (index 3)
				if (colonIndex > 4)
				{
					actualPath = path.Substring(0, colonIndex);
					species = path.Substring(colonIndex + 1);
				}

				// Override species if provided directly in data
				if (data.ContainsKey("species") && !string.IsNullOrEmpty((string)data["species"]))
				{
					species = (string)data["species"];
				}

				if (!ResourceLoader.Exists(actualPath))
				{
					GD.PrintErr($"[NetworkManager] PackedScene NOT found at {actualPath}");
					return null;
				}
				var scene = GD.Load<PackedScene>(actualPath);
				if (scene != null)
				{
					var instance = scene.Instantiate();
					instance.Name = System.IO.Path.GetFileNameWithoutExtension(actualPath) + "_" + Time.GetTicksMsec();

					if (instance is InteractableObject io)
					{
						io.ObjectName = System.IO.Path.GetFileNameWithoutExtension(actualPath);
						// Only set ModelPath if it's not already defined (scene may have FBX path for texturing)
                        if (string.IsNullOrEmpty(io.ModelPath))
                            io.ModelPath = path;

                        if (io is Monsters monster && !string.IsNullOrEmpty(species))
                        {
                            monster.Species = species;
                        }
                    }
                    obj = instance;
                    GD.Print($"[NetworkManager] Instantiated TSCN: {obj.Name}");
                }
                else
                {
                    GD.PrintErr($"[NetworkManager] Failed to load PackedScene: {actualPath}");
                }
            }
        }

        if (obj is Node3D n3d)
        {
            // Use LOCAL properties because node is not in tree yet.
            // Parent is WorldObjects (at 0,0,0), so Position == GlobalPosition.
            n3d.Position = pos;
            n3d.Rotation = rot;
            n3d.Scale = scale;
        }

        return obj;
    }
}
