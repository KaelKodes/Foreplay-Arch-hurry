using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class NetworkHUD : Control
{
    private LineEdit _ipInput;
    private Button _hostButton;
    private Button _joinButton;
    private Label _statusLabel;
    private NetworkManager _networkManager;

    public override void _Ready()
    {
        _networkManager = GetNodeOrNull<NetworkManager>("../NetworkManager");
        if (_networkManager == null)
        {
            // Try searching globally or in common paths if not a sibling
            _networkManager = GetTree().CurrentScene.GetNodeOrNull<NetworkManager>("NetworkManager");
        }

        // Setup UI programmatically if not using a scene file,
        // or just bind if using a .tscn (Using .tscn is better, but let's assume we build it here for speed/simplicity)

        // We will assume this script is attached to the Root Control of a scene constructed in Godot,
        // OR we can build the UI here. Constructing here is safer for the agent.

        BuildUI();
    }

    private void BuildUI()
    {
        // Container
        var panel = new Panel();
        panel.Size = new Vector2(300, 200);
        panel.Position = new Vector2(20, 20);
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.Position = new Vector2(10, 10);
        vbox.Size = new Vector2(280, 180);
        panel.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "Multiplayer Lobby";
        vbox.AddChild(title);

        // IP Input
        _ipInput = new LineEdit();
        _ipInput.PlaceholderText = "127.0.0.1";
        vbox.AddChild(_ipInput);

        // Host Button
        _hostButton = new Button();
        _hostButton.Text = "Host Game";
        _hostButton.Pressed += OnHostPressed;
        vbox.AddChild(_hostButton);

        // Join Button
        _joinButton = new Button();
        _joinButton.Text = "Join Game";
        _joinButton.Pressed += OnJoinPressed;
        vbox.AddChild(_joinButton);

        // Status
        _statusLabel = new Label();
        _statusLabel.Text = "Ready";
        vbox.AddChild(_statusLabel);

        // IP Display
        var ipLabel = new Label();
        ipLabel.Text = "Local IP: " + GetLocalIP();
        ipLabel.Modulate = Colors.Gray; // Subtle styling
        vbox.AddChild(ipLabel);
    }

    private string GetLocalIP()
    {
        foreach (var ip in IP.GetLocalAddresses())
        {
            if (ip.Contains(".") && !ip.StartsWith("127.") && !ip.StartsWith("169.254"))
            {
                return ip;
            }
        }
        return "Unknown";
    }

    private void OnHostPressed()
    {
        if (_networkManager == null) { _statusLabel.Text = "Error: No NetworkManager"; return; }

        _statusLabel.Text = "Hosting...";
        _networkManager.HostGame();
        Visible = false; // Hide HUD on start
    }

    private void OnJoinPressed()
    {
        if (_networkManager == null) { _statusLabel.Text = "Error: No NetworkManager"; return; }

        string ip = _ipInput.Text.Trim();
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        _statusLabel.Text = $"Joining {ip}...";
        _networkManager.JoinGame(ip);
        Visible = false; // Hide HUD on connect? Maybe wait for success signal?
    }
}
