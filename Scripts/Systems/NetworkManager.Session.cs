using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class NetworkManager
{
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
}
