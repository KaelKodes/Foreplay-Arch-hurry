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
	private string _currentLevelPath = "";

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

		// Ensure PlayerScene is loaded
		if (PlayerScene == null)
		{
			GD.Print("NetworkManager: PlayerScene was null, loading from res://Scenes/Entities/Player.tscn");
			PlayerScene = GD.Load<PackedScene>("res://Scenes/Entities/Player.tscn");
		}
	}
}
