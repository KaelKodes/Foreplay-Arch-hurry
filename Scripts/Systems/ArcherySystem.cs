using Godot;
using System;
using System.Collections.Generic;
using Archery;

namespace Archery;

public enum DrawStage
{
	Idle,
	Drawing, // Power phase
	Aiming,  // Accuracy phase
	Executing,
	ShotComplete
}

public enum ArcheryShotMode
{
	Standard, // 5 degrees
	Long,     // 25 degrees
	Max       // 45 degrees
}

public partial class ArcherySystem : Node
{
	[Export] public PackedScene ArrowScene; // Assigned in _Ready or Editor
	public int ArrowCount { get; private set; } = 10;
	private int _shotTicket = 0; // Unique ID for each shot to prevent naming collisions

	[Export] public float DrawSpeed = 1.0f;
	[Export] public NodePath ArrowPath;
	[Export] public NodePath CameraPath;
	[Export] public NodePath WindSystemPath;

	// Calibrated Offsets
	private readonly Vector3 _calibratedPos = new Vector3(0.061f, 0.385f, 0.033f);
	private readonly Vector3 _calibratedRot = new Vector3(99.375f, -7.080f, 27.708f);

	// Debug / Calibration
	public Vector3 DebugArrowOffsetPosition = Vector3.Zero;
	public Vector3 DebugArrowOffsetRotation = Vector3.Zero;

	private ArrowController _arrow;
	private CameraController _camera;
	private WindSystem _windSystem;
	private StatsService _statsService;
	private BoneAttachment3D _handAttachment; // New attachment point

	[Export] public bool CanDashWhileShooting { get; set; } = false;
	[Export] public bool CanJumpWhileShooting { get; set; } = false;
	[Export] public float ShootingMoveMultiplier { get; set; } = 0.3f;

	private BuildManager _buildManager;
	private ObjectPlacer _objectPlacer;

	public BuildManager BuildManager => _buildManager;
	public ObjectPlacer ObjectPlacer => _objectPlacer;
	public StatsService PlayerStatsService => _statsService;
	public Stats PlayerStats => _statsService?.PlayerStats ?? new Stats { Power = 10, Control = 10, Touch = 10 };
	public Vector3 ChestOffset => new Vector3(0, 1.3f, 0);
	public Vector3 BallPosition
	{
		get
		{
			if (_currentPlayer == null) return (_arrow != null) ? _arrow.GlobalPosition : Vector3.Zero;

			// While not yet launched, follow the player's face/chest
            if (_stage == DrawStage.Idle || _stage == DrawStage.Drawing || _stage == DrawStage.Aiming)
            {
                return _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f)));
            }

            return (_arrow != null) ? _arrow.GlobalPosition : Vector3.Zero;
        }
    }
    public Vector3 TeePosition { get; private set; } = Vector3.Zero;

    private DrawStage _stage = DrawStage.Idle;
    private float _timer = 0.0f;
    private bool _isReturnPhase = false;
    private float _lockedPower = -1.0f;
    private float _lockedAccuracy = -1.0f;
    private Vector2 _spinIntent = Vector2.Zero;
    private float _powerOverride = -1.0f;
    public DrawStage CurrentStage => _stage;
    private float _lastAdvanceTime = 0.0f;
    private long _lastInputFrame = -1;
    private ArcheryShotMode _currentMode = ArcheryShotMode.Standard;
    public ArcheryShotMode CurrentMode => _currentMode;
    private PlayerController _currentPlayer;
    private Node3D _currentTarget;
    public Node3D CurrentTarget => _currentTarget;

    // Bow cooldown
    private const float BowCooldownTime = 2.0f;
    private float _bowCooldownRemaining = 0f;
    public bool IsBowOnCooldown => _bowCooldownRemaining > 0;

    private bool _isNextShotPiercing = false;

    [Signal] public delegate void DrawStageChangedEventHandler(int newStage);
    [Signal] public delegate void ArcheryValuesUpdatedEventHandler(float currentBarValue, float lockedPower, float lockedAccuracy);
    [Signal] public delegate void ShotResultEventHandler(float power, float accuracy);
    [Signal] public delegate void ModeChangedEventHandler(bool isCombat);
    [Signal] public delegate void PromptChangedEventHandler(bool visible, string message);
    [Signal] public delegate void ShotModeChangedEventHandler(int newMode);
    [Signal] public delegate void TargetChangedEventHandler(Node3D target);
    [Signal] public delegate void ArrowInitializedEventHandler(ArrowController arrow);
    [Signal] public delegate void BowCooldownUpdatedEventHandler(float remaining, float total);

    public override void _Ready()
    {
        var placeholder = GetNodeOrNull<PlayerController>("../PlayerPlaceholder");
        if (placeholder != null) RegisterPlayer(placeholder);

        // Load Arrow Scene resource if not assigned
        if (ArrowScene == null) ArrowScene = GD.Load<PackedScene>("res://Scenes/Entities/Arrow.tscn");

        // Remove the scene-resident arrow if it exists (we will spawn our own)
        if (ArrowPath != null)
        {
            var sceneArrow = GetNodeOrNull(ArrowPath);
            if (sceneArrow != null) sceneArrow.QueueFree();
        }

        if (CameraPath != null && !CameraPath.IsEmpty) _camera = GetNodeOrNull<CameraController>(CameraPath);
        GD.Print($"ArcherySystem: Ready. Camera Found: {(_camera != null)}");
        if (WindSystemPath != null && !WindSystemPath.IsEmpty) _windSystem = GetNodeOrNull<WindSystem>(WindSystemPath);

        _statsService = new StatsService();
        _statsService.Name = "StatsService";
        AddChild(_statsService);

        string heroClass = "Ranger";
        if (GetParent() is PlayerController pc)
        {
            heroClass = pc.CurrentModelId;
        }

        _statsService.LoadStats(heroClass);

        _buildManager = new BuildManager();
        _buildManager.Name = "BuildManager";
        AddChild(_buildManager);

        _objectPlacer = new ObjectPlacer();
        _objectPlacer.Name = "ObjectPlacer";
        AddChild(_objectPlacer);

        CallDeferred(MethodName.ExitCombatMode);
        CallDeferred(MethodName.UpdateArrowLabel);

        // [FIX] Initialize TeePosition from Scene (with global search)
        Node3D spawnPoint = GetParent().GetNodeOrNull<Node3D>("SpawnPoint");
        if (spawnPoint == null) spawnPoint = GetParent().FindChild("TeeBox", true, false) as Node3D;
        if (spawnPoint == null) spawnPoint = GetParent().FindChild("VisualTee", true, false) as Node3D;
        if (spawnPoint == null) spawnPoint = GetParent().FindChild("Tee", true, false) as Node3D;

        // Global fallback search
        if (spawnPoint == null)
        {
            var root = GetTree().CurrentScene;
            spawnPoint = root.FindChild("SpawnPoint", true, false) as Node3D;
            if (spawnPoint == null) spawnPoint = root.FindChild("TeeBox", true, false) as Node3D;
            if (spawnPoint == null) spawnPoint = root.FindChild("Tee", true, false) as Node3D;
        }

        bool foundSpawn = false;
        if (spawnPoint != null)
        {
            TeePosition = spawnPoint.GlobalPosition;
            foundSpawn = true;
            GD.Print($"ArcherySystem: TeePosition set to {TeePosition} from {spawnPoint.Name}");
        }

        // MOBA Specific: Team-based spawn positions
        if (MobaGameManager.Instance != null && _currentPlayer != null)
        {
            var teamSpawnName = _currentPlayer.Team == MobaTeam.Red ? "SpawnPoint_Red" : "SpawnPoint_Blue";
            var teamSpawn = GetTree().CurrentScene.FindChild(teamSpawnName, true, false) as Node3D;

            if (teamSpawn != null)
            {
                TeePosition = teamSpawn.GlobalPosition;
                foundSpawn = true;
                GD.Print($"ArcherySystem: Spawning at Team Spawn: {teamSpawn.Name} ({TeePosition})");
            }
            else
            {
                var nexus = GetTree().CurrentScene.FindChild(_currentPlayer.Team.ToString() + "Nexus", true, false) as Node3D;
                if (nexus != null)
                {
                    TeePosition = nexus.GlobalPosition + Vector3.Up;
                    foundSpawn = true;
                    GD.Print($"ArcherySystem: Spawning at Team Nexus: {nexus.Name}");
                }
            }
        }

        if (!foundSpawn && _currentPlayer != null)
        {
            GD.PrintErr("ArcherySystem: No spawn point found (tried Generic, Team, and Nexus fallbacks)! Defaulting to (0,0,0).");
        }

        if (_currentPlayer != null)
        {
            _currentPlayer.TeleportTo(TeePosition, TeePosition + Vector3.Forward * 10.0f);
        }

        // Connect to ProjectileSpawner
        var spawner = GetTree().CurrentScene.FindChild("ProjectileSpawner", true, false) as MultiplayerSpawner;
        if (spawner == null)
        {
            spawner = GetTree().CurrentScene.GetNodeOrNull<MultiplayerSpawner>("ProjectileSpawner");
        }
        if (spawner != null)
        {
            spawner.SpawnFunction = new Callable(this, nameof(SpawnArrowLocally));
            spawner.Spawned += OnArrowSpawned;
            GD.Print("ArcherySystem: Connected to ProjectileSpawner with Custom SpawnFunction.");
        }
        else
        {
            GD.PrintErr("ArcherySystem: ProjectileSpawner not found in scene!");
        }
    }

    // Process loop for bar animation
    public override void _Process(double delta)
    {
        // For remote players, sync the stage from the parent PlayerController
        if (_currentPlayer != null && !_currentPlayer.IsLocal)
        {
            _stage = (DrawStage)_currentPlayer.SynchronizedArcheryStage;
        }

        // Bow cooldown tick
        if (_bowCooldownRemaining > 0)
        {
            _bowCooldownRemaining -= (float)delta;
            if (_currentPlayer != null && _currentPlayer.IsLocal)
            {
                EmitSignal(SignalName.BowCooldownUpdated, _bowCooldownRemaining, BowCooldownTime);
            }
            if (_bowCooldownRemaining <= 0)
            {
                _bowCooldownRemaining = 0;
            }
        }

        if (_stage == DrawStage.Idle || _stage == DrawStage.Drawing)
        {
            UpdateArrowPose();
        }

        // Auto-cycle target if it dies (RPG Mode only)
        if (_currentPlayer != null && _currentPlayer.IsLocal && _currentTarget != null)
        {
            bool isRPG = ToolManager.Instance != null && ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;
            if (isRPG && TargetingHelper.IsTargetDead(_currentTarget))
            {
                GD.Print($"[ArcherySystem] Current Target Dead. Auto-cycling...");
                CycleTarget(false); // Cycle to next enemy
            }
        }
    }
}
