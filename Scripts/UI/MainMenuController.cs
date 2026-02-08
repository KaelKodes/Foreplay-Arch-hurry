using Godot;
using System;

namespace Archery;

public partial class MainMenuController : Control
{
    private MenuPhysicsHelper _physicsHelper;

    private FileDialog _loadDialog;

    public override void _Ready()
    {
        // Setup Physics Helper
        _physicsHelper = new MenuPhysicsHelper();
        AddChild(_physicsHelper);

        // Wiring
        _physicsHelper.BallScene = GD.Load<PackedScene>("res://Scenes/Entities/Arrow.tscn");
        _physicsHelper.BallsContainer = GetNode<Node3D>("BallViewport/SubViewport/MenuStage/BallsContainer");
        _physicsHelper.CollidersContainer = GetNode<Node3D>("BallViewport/SubViewport/MenuStage/CollidersContainer");
        _physicsHelper.StageCamera = GetNode<Camera3D>("BallViewport/SubViewport/MenuStage/Camera3D");

        // Delay collider generation to ensure UI layout is final
        CallDeferred(MethodName.InitPhysics);

        // Setup Load Dialog
        SetupLoadDialog();

        // Auto-connect Load Button if found (Assuming named "LoadBtn")
        var loadBtn = FindChild("LoadBtn", true, false) as Button;
        if (loadBtn != null)
        {
            loadBtn.Pressed += OnLoadPressed;
        }

        // Connect Lobby Buttons
        var hostBtn = FindChild("HostBtn", true, false) as Button;
        if (hostBtn != null) hostBtn.Pressed += OnHostPressed;

        var joinBtn = FindChild("JoinBtn", true, false) as Button;
        if (joinBtn != null) joinBtn.Pressed += OnJoinPressed;

        // Hide Putting Range if found (User request)
        var puttingBtn = FindChild("PuttingRangeBtn", true, false) as Button; // Guessing name
        if (puttingBtn != null) puttingBtn.Visible = false;
    }

    private void OnHostPressed()
    {
        GD.Print("Hosting Game via Lobby...");
        NetworkManager.Instance.HostGame();
    }

    private void OnJoinPressed()
    {
        var ipInput = FindChild("IPInput", true, false) as LineEdit;
        string ip = ipInput?.Text ?? "127.0.0.1";
        GD.Print($"Joining Game at {ip}...");
        NetworkManager.Instance.JoinGame(ip);
    }

    private void SetupLoadDialog()
    {
        _loadDialog = new FileDialog();
        _loadDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _loadDialog.Access = FileDialog.AccessEnum.Userdata;
        _loadDialog.Filters = new string[] { "*.json" };
        _loadDialog.CurrentDir = "user://courses";
        _loadDialog.FileSelected += OnLoadFileSelected;
        // Make sure it's visible on top
        _loadDialog.Title = "Load Course";
        _loadDialog.MinSize = new Vector2I(600, 400);
        AddChild(_loadDialog);
    }

    private void OnLoadPressed()
    {
        _loadDialog.PopupCentered();
    }

    private void OnLoadFileSelected(string path)
    {
        // 1. Set global state
        string filename = System.IO.Path.GetFileNameWithoutExtension(path);
        CoursePersistenceManager.CourseToLoad = filename;

        // 2. Load Game Scene
        // Note: Using FoxHollowHole1 as the generic "Play" scene for now.
        GetTree().ChangeSceneToFile("res://Scenes/Levels/FoxHollowHole1.tscn");
    }

    private void InitPhysics()
    {
        _physicsHelper.RefreshColliders();
    }

    private void OnDrivingRangePressed()
    {
        // Redirecting to MOBA1 for development
        // GetTree().ChangeSceneToFile("res://Scenes/Levels/DrivingRange.tscn");
        GetTree().ChangeSceneToFile("res://Scenes/Levels/MOBA1.tscn");
    }

    private void OnPuttingRangePressed()
    {
        // Placeholder for when PuttingRange scene is created
        GD.Print("Putting Range selected");
        // GetTree().ChangeSceneToFile("res://Scenes/Levels/PuttingRange.tscn");
    }

    private void OnExitPressed()
    {
        GetTree().Quit();
    }

    private void OnCharacterImportPressed()
    {
        GD.Print("[MainMenu] Opening Character Import Wizard...");
        GetTree().ChangeSceneToFile("res://Scenes/UI/CharacterImportWizard.tscn");
    }
}
