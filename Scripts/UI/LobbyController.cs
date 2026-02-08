using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class LobbyController : Control
{
	private GridContainer _redGrid;
	private GridContainer _blueGrid;
	private Button _startButton;
	private Button _backButton;
	private Label _titleLabel;

	private List<LobbySlot> _redSlots = new();
	private List<LobbySlot> _blueSlots = new();

	public override void _Ready()
	{
		_redGrid = GetNode<GridContainer>("MarginContainer/VBox/TeamsContainer/RedTeam/Grid");
		_blueGrid = GetNode<GridContainer>("MarginContainer/VBox/TeamsContainer/BlueTeam/Grid");
		_startButton = GetNode<Button>("MarginContainer/VBox/HBox/StartBtn");
		_backButton = GetNode<Button>("MarginContainer/VBox/HBox/BackBtn");
		_titleLabel = GetNode<Label>("MarginContainer/VBox/Title");

		_startButton.Pressed += OnStartPressed;
		_backButton.Pressed += OnBackPressed;

		// Initialize 4 slots per team
		for (int i = 0; i < 4; i++)
		{
			var rSlot = new LobbySlot();
			_redGrid.AddChild(rSlot);
			_redSlots.Add(rSlot);

			var bSlot = new LobbySlot();
			_blueGrid.AddChild(bSlot);
			_blueSlots.Add(bSlot);
		}

		LobbyManager.Instance.Connect(LobbyManager.SignalName.PlayerListUpdated, Callable.From(RefreshUI));

		// Register local player
		string name = Multiplayer.IsServer() ? "Host" : $"Guest_{Multiplayer.GetUniqueId() % 1000}";
		LobbyManager.Instance.AddPlayer(Multiplayer.GetUniqueId(), name);

		RefreshUI();

		GD.Print("[LobbyController] Premium ready.");
	}

	private void RefreshUI()
	{
		var players = LobbyManager.Instance.GetPlayers();
		bool isHost = Multiplayer.IsServer();
		long localId = Multiplayer.GetUniqueId();

		var redPlayers = players.Where(p => p.Team == MobaTeam.Red).ToList();
		var bluePlayers = players.Where(p => p.Team == MobaTeam.Blue).ToList();

		for (int i = 0; i < 4; i++)
		{
			var redData = i < redPlayers.Count ? redPlayers[i] : null;
			_redSlots[i].Setup(redData, redData?.Id == localId, isHost, MobaTeam.Red);

			var blueData = i < bluePlayers.Count ? bluePlayers[i] : null;
			_blueSlots[i].Setup(blueData, blueData?.Id == localId, isHost, MobaTeam.Blue);
		}

		_startButton.Visible = isHost;
		_titleLabel.Text = $"BATTLE LOBBY - {players.Count} WARRIORS";
	}

	private void OnStartPressed()
	{
		GD.Print("[LobbyController] Entering battle...");
		GetTree().ChangeSceneToFile("res://Scenes/Levels/MOBA1.tscn");
	}

	private void OnBackPressed()
	{
		GD.Print("[LobbyController] Returning to menu...");
		LobbyManager.Instance.ClearLobby();
		NetworkManager.Instance.ReturnToMainMenu();
	}
}
