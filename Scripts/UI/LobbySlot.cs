using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class LobbySlot : PanelContainer
{
    private Label _nameLabel;
    private Label _classLabel;
    private Button _cycleClassBtn;
    private Button _switchTeamBtn;
    private Button _addBotBtn;
    private ColorRect _teamIndicator;

    private long _playerId = 0;
    private bool _isEmpty = true;

    private SubViewport _viewport;
    private Node3D _previewWorld;
    private Node3D _currentModel;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(220, 360);

        // Premium Glass Panel Style
        var style = new StyleBoxFlat();
        style.BgColor = new Color(1, 1, 1, 0.05f);
        style.BorderWidthLeft = style.BorderWidthTop = style.BorderWidthRight = style.BorderWidthBottom = 1;
        style.BorderColor = new Color(1, 1, 1, 0.1f);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 12;
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 15);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_right", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        _nameLabel = new Label();
        _nameLabel.Text = "AVAILABLE";
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.3f));
        vbox.AddChild(_nameLabel);

        // Class Preview 3D
        var viewportContainer = new SubViewportContainer();
        viewportContainer.CustomMinimumSize = new Vector2(180, 200);
        viewportContainer.Stretch = true;
        viewportContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        viewportContainer.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(viewportContainer);

        _viewport = new SubViewport();
        _viewport.OwnWorld3D = true;
        _viewport.TransparentBg = true;
        _viewport.Msaa3D = Viewport.Msaa.Msaa4X;
        viewportContainer.AddChild(_viewport);

        _previewWorld = new Node3D();
        _viewport.AddChild(_previewWorld);

        // Better Lighting
        var camera = new Camera3D();
        _previewWorld.AddChild(camera);
        camera.Position = new Vector3(0, 1.25f, 2.5f);
        camera.LookAt(new Vector3(0, 0.9f, 0));

        var light = new DirectionalLight3D();
        _previewWorld.AddChild(light);
        light.Position = new Vector3(2, 4, 3);
        light.LookAt(Vector3.Zero);
        light.LightEnergy = 1.5f;

        var fillLight = new OmniLight3D();
        _previewWorld.AddChild(fillLight);
        fillLight.Position = new Vector3(-3, 2, 1);
        fillLight.LightEnergy = 0.8f;

        _classLabel = new Label();
        _classLabel.Text = "---";
        _classLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _classLabel.AddThemeFontSizeOverride("font_size", 14);
        _classLabel.SelfModulate = new Color(0.7f, 0.7f, 0.8f);
        vbox.AddChild(_classLabel);

        var btnVBox = new VBoxContainer();
        btnVBox.AddThemeConstantOverride("separation", 5);
        vbox.AddChild(btnVBox);

        _cycleClassBtn = new Button();
        _cycleClassBtn.Text = "CHOOSE CLASS";
        _cycleClassBtn.CustomMinimumSize = new Vector2(0, 35);
        _cycleClassBtn.Visible = false;
        _cycleClassBtn.Pressed += () => LobbyManager.Instance.CycleClass(_playerId);
        btnVBox.AddChild(_cycleClassBtn);

        _switchTeamBtn = new Button();
        _switchTeamBtn.Text = "SWITCH TEAM";
        _switchTeamBtn.CustomMinimumSize = new Vector2(0, 35);
        _switchTeamBtn.Visible = false;
        _switchTeamBtn.Pressed += () => LobbyManager.Instance.SwitchTeam(_playerId);
        btnVBox.AddChild(_switchTeamBtn);

        _addBotBtn = new Button();
        _addBotBtn.Text = "+ ADD BOT";
        _addBotBtn.CustomMinimumSize = new Vector2(0, 45);
        _addBotBtn.Visible = false;
        vbox.AddChild(_addBotBtn);

        _teamIndicator = new ColorRect();
        _teamIndicator.CustomMinimumSize = new Vector2(0, 5);
        _teamIndicator.Color = new Color(1, 1, 1, 0.1f);
        vbox.AddChild(_teamIndicator);
    }

    public void Setup(LobbyPlayerData data, bool isLocalPlayer, bool isHost, MobaTeam slotTeam)
    {
        ClearModel();

        if (data == null)
        {
            _isEmpty = true;
            _playerId = 0;
            _nameLabel.Text = "AVAILABLE";
            _classLabel.Text = "---";
            _nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.15f));

            _teamIndicator.Color = slotTeam == MobaTeam.Red ? new Color(0.8f, 0.2f, 0.2f, 0.05f) : new Color(0.2f, 0.2f, 0.8f, 0.05f);

            _cycleClassBtn.Visible = false;
            _switchTeamBtn.Visible = false;
            _addBotBtn.Visible = isHost;

            foreach (var connection in _addBotBtn.GetSignalConnectionList("pressed"))
                _addBotBtn.Disconnect("pressed", (Callable)connection["callable"]);

            _addBotBtn.Pressed += () => LobbyManager.Instance.AddBot(slotTeam);

            // Clean panel
            var cleanStyle = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
            cleanStyle.BorderColor = new Color(1, 1, 1, 0.05f);
            cleanStyle.ShadowSize = 0;
            AddThemeStyleboxOverride("panel", cleanStyle);
            return;
        }

        _isEmpty = false;
        _playerId = data.Id;
        _nameLabel.Text = data.IsBot ? $"BOT" : data.Name.ToUpper();
        _nameLabel.AddThemeColorOverride("font_color", Colors.White);
        _classLabel.Text = data.ClassName.ToUpper();

        Color teamColor = data.Team == MobaTeam.Red ? new Color(1.0f, 0.3f, 0.3f) : new Color(0.3f, 0.6f, 1.0f);
        _teamIndicator.Color = teamColor;

        _addBotBtn.Visible = false;

        bool canControl = isLocalPlayer || (isHost && data.IsBot);
        _cycleClassBtn.Visible = canControl;
        _switchTeamBtn.Visible = canControl;

        UpdatePreview(data.ClassName);

        // Dynamic Glow Style
        var style = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
        style.ShadowColor = new Color(teamColor, 0.2f);
        style.ShadowSize = 15;
        style.BorderColor = new Color(teamColor, 0.4f);
        AddThemeStyleboxOverride("panel", style);
    }

    private void UpdatePreview(string className)
    {
        var modelData = CharacterRegistry.Instance.GetModel(className);
        if (modelData == null) return;

        if (ResourceLoader.Exists(modelData.MeleeScenePath))
        {
            var scn = GD.Load<PackedScene>(modelData.MeleeScenePath);
            _currentModel = scn.Instantiate<Node3D>();
            _previewWorld.AddChild(_currentModel);
            _currentModel.Position = Vector3.Zero;
            _currentModel.Scale = modelData.ModelScale;

            var ap = FindAnimationPlayer(_currentModel);
            if (ap != null)
            {
                if (ap.HasAnimation("Idle")) ap.Play("Idle");
                else if (ap.HasAnimation("standing idle 01")) ap.Play("standing idle 01");
                else if (ap.HasAnimation("melee idle")) ap.Play("melee idle");
            }
        }
    }

    private void ClearModel()
    {
        if (_currentModel != null)
        {
            _currentModel.QueueFree();
            _currentModel = null;
        }
    }

    private AnimationPlayer FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer ap) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found != null) return found;
        }
        return null;
    }
}
