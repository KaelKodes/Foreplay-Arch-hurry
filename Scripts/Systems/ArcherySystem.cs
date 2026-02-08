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

    public void SetTeePosition(Vector3 pos) => TeePosition = pos;
    public void UpdateTeePosition(Vector3 pos) => TeePosition = pos;
    public void UpdatePinPosition(Vector3 pos) { /* Placeholder for future target logic */ }
    public BallLie GetCurrentLie() => new BallLie { PowerEfficiency = 1.0f, LaunchAngleBonus = 0.0f, SpinModifier = 1.0f };
    public float GetEstimatedPower() => 100.0f; // Placeholder
    public float AoAOffset => 0.0f; // Placeholder

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
        _statsService.LoadStats();

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
            // MOBA specific spawn positions: BlueTeam spawns at BlueSpawn (near Blue Nexus), RedTeam at RedSpawn
            // Note: MobaGameManager stores these as _redSpawnPos and _blueSpawnPos (inner turret locations)
            // But usually Red is top/left, Blue is bottom/right.
            // Actually, the manager sets:
            // if (tower.Team == MobaTeam.Red) ... _redSpawnPos = tower.GlobalPosition;

            // We need a public cleaner way to get these or just use the logic here.
			// Let's use a dynamic lookup for "SpawnPoint_" + Team

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
				// Fallback to MobaGameManager's registered structure positions
                // RedSpawnPos in manager is Red Inner Tower.
				// We'll need to check if those private fields are accessible or add accessors.
				// For now, let's look for "RedNexus" / "BlueNexus" as fallback spawn points.
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
            // Fallback: look for any MultiplayerSpawner named ProjectileSpawner
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

    /// <summary>
    /// Custom Spawn Function for MultiplayerSpawner. Handles instantiating the arrow
    /// and applying the initial color received in the spawn data.
    /// </summary>
    private Node SpawnArrowLocally(Godot.Collections.Dictionary data)
    {
        if (ArrowScene == null) ArrowScene = GD.Load<PackedScene>("res://Scenes/Entities/Arrow.tscn");
        var arrow = ArrowScene.Instantiate<ArrowController>();

        // Apply name from spawn data to prevent parent->has_node(name) collisions
        if (data != null && data.ContainsKey("name"))
        {
            arrow.Name = (string)data["name"];
        }

        // Apply color from spawn data
        if (data != null && data.ContainsKey("color_r"))
        {
            float r = (float)data["color_r"];
            float g = (float)data["color_g"];
            float b = (float)data["color_b"];
            arrow.SetColor(new Color(r, g, b));
            GD.Print($"ArcherySystem: SpawnArrowLocally applied color ({r},{g},{b}) to {arrow.Name}");
        }

        // Apply team from spawn data
        if (data != null && data.ContainsKey("team"))
        {
            arrow.Team = (MobaTeam)(int)data["team"];
        }

        return arrow;
    }

    private void OnArrowSpawned(Node node)
    {
        if (node is ArrowController arrow)
        {
            string currentPlayerName = _currentPlayer?.Name ?? "NULL";
            GD.Print($"ArcherySystem: Arrow Spawned/Replicated: {arrow.Name}, MyCurrentPlayer: {currentPlayerName}");

            // Parse Name to see if it belongs to us: Arrow_{PlayerID}_{Ticket}
			string[] parts = arrow.Name.ToString().Split('_');
            if (parts.Length >= 2 && long.TryParse(parts[1], out long ownerId))
            {
                // Match by Player Index or Name for robust ownership check
                bool isMine = false;
                if (_currentPlayer != null)
                {
                    if (ownerId.ToString() == _currentPlayer.Name) isMine = true;
                }

                if (isMine)
                {
					GD.Print($"ArcherySystem: It's MY arrow! Taking control. (Owner: {ownerId}, MyPlayer: {_currentPlayer.Name})");
                    _arrow = arrow;
                }
                else
                {
					// Even if it's not "mine" (local), if it belongs to the player this ArcherySystem represents, 
                    // we should track it so we can drive its visibility/pose for remote players.
					if (ownerId.ToString() == (_currentPlayer?.Name ?? ""))
                    {
						GD.Print($"ArcherySystem: Tracking arrow for remote player {ownerId}.");
                        _arrow = arrow;
                    }
                    else
                    {
						GD.Print($"ArcherySystem: It's Player {ownerId}'s arrow. (Not for me: {currentPlayerName})");
                    }
                }

                // SetupArrow will handle connecting signals and initial setup
                PlayerController pc = GetTree().CurrentScene.FindChild(ownerId.ToString(), true, false) as PlayerController;
                SetupArrow(arrow, pc);
            }
        }
    }

    private Color GetPlayerColorByOwnerId(long ownerId)
    {
        PlayerController pc = GetTree().CurrentScene.FindChild(ownerId.ToString(), true, false) as PlayerController;
        if (pc != null) return GetPlayerColor(pc.PlayerIndex);
        return Colors.White;
    }

    public void CollectArrow()
    {
        ArrowCount++;
        UpdateArrowLabel();
        // Play sound?
    }

    private void UpdateArrowLabel()
    {
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }

    public void PrepareNextShot()
    {
        // Only the authority (the player themselves) should trigger a new arrow spawn.
        // Other peers will receive the arrow via replication (OnArrowSpawned).
        if (_currentPlayer != null && !_currentPlayer.IsLocal) return;

        if (ArrowCount <= 0 && MobaGameManager.Instance == null)
        {
			SetPrompt(true, "Out of Arrows!");
            return;
        }

        if (_arrow != null)
        {
            if (!_arrow.HasBeenShot)
            {
                _arrow.QueueFree();
            }
            _arrow = null;
        }

        // Increment ticket for unique naming
        _shotTicket++;

        // Spawn new arrow (Networked)
        if (ArrowScene != null)
        {
            // If Singleplayer, do old logic
            if (Multiplayer.MultiplayerPeer == null || Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
            {
                _arrow = ArrowScene.Instantiate<ArrowController>();
                GetTree().CurrentScene.AddChild(_arrow);
                SetupArrow(_arrow);
            }
            else
            {
                // Multiplayer:
                if (_currentPlayer != null)
                {
                    if (Multiplayer.IsServer())
                    {
                        // If we are the server, just spawn it directly.
                        // RpcId(1) to self is blocked by CallLocal=false.
                        SpawnNetworkedArrow(int.Parse(_currentPlayer.Name.ToString()), _shotTicket);
                    }
                    else
                    {
                        // Client: Request Server to spawn arrow for us
                        RpcId(1, nameof(RequestSpawnArrow), int.Parse(_currentPlayer.Name.ToString()), _shotTicket);
                    }
                }
            }
        }

        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;


        if (_camera != null) { _camera.SetTarget(_currentPlayer, true); _camera.SetFollowing(false); _camera.SetFreeLook(false); }
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);

        UpdateArrowLabel();
    }



    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RequestSpawnArrow(long playerId, int ticket)
    {
        // SERVER ONLY
        if (!Multiplayer.IsServer()) return;
        SpawnNetworkedArrow(playerId, ticket);
    }

    private void SpawnNetworkedArrow(long playerId, int ticket)
    {
		string uniqueName = $"Arrow_{playerId}_{ticket}";

        PlayerController owner = null;
        if (NetworkManager.Instance != null && NetworkManager.Instance.GetPlayer(playerId) is PlayerController pc)
        {
            owner = pc;
        }

        // Use the ProjectileSpawner to spawn the arrow (Godot 4 native way)
		var spawner = GetTree().CurrentScene.GetNodeOrNull<MultiplayerSpawner>("ProjectileSpawner");
        ArrowController arrow = null;

        if (spawner == null)
        {
			GD.PrintErr("ArcherySystem: ProjectileSpawner NOT found! Falling back to AddChild.");
            arrow = ArrowScene.Instantiate<ArrowController>();
            arrow.Name = uniqueName;
			var projectiles = GetTree().CurrentScene.GetNodeOrNull("Projectiles");
            if (projectiles != null) projectiles.AddChild(arrow, true);
            else GetTree().CurrentScene.AddChild(arrow);
        }
        else
        {
            // Passing the color in the spawn data ensures it's available immediately on the client
            Color playerColor = owner != null ? GetPlayerColor(owner.PlayerIndex) : Colors.White;
            var spawnData = new Godot.Collections.Dictionary {
			{ "color_r", playerColor.R },
			{ "color_g", playerColor.G },
			{ "color_b", playerColor.B },
			{ "player_id", playerId },
			{ "name", uniqueName },
			{ "team", (int)(owner?.Team ?? MobaTeam.None) }
        };

            // spawner.Spawn() will handle instantiation on all peers via SpawnArrowLocally
            arrow = spawner.Spawn(spawnData) as ArrowController;
        }

        if (arrow != null)
        {
			GD.Print($"ArcherySystem: Spawned Networked Arrow '{arrow.Name}' for Player {playerId}");

            // Assign ownership if it belongs to the Host (who is also _currentPlayer on the server)
            if (_currentPlayer != null && playerId.ToString() == _currentPlayer.Name)
            {
				GD.Print("ArcherySystem (Server): It's Host's arrow! Taking control.");
                _arrow = arrow;
            }

            SetupArrow(arrow, owner);
        }
    }

    private void SetupArrow(ArrowController arrow, PlayerController owner = null)
    {
        arrow.Connect(ArrowController.SignalName.ArrowSettled, new Callable(this, MethodName.OnArrowSettled));
        arrow.Connect(ArrowController.SignalName.ArrowCollected, new Callable(this, MethodName.CollectArrow));
        EmitSignal(SignalName.ArrowInitialized, arrow);

        // Default owner is _currentPlayer if not specified
        PlayerController actualOwner = owner ?? _currentPlayer;

        // Exclude player
        if (actualOwner != null) arrow.SetCollisionException(actualOwner);

        arrow.Visible = true;

        // If we set Freeze=true here, we might overwrite sync.
        // HOWEVER, SetupArrow is called by SpawnNetworkedArrow (Server) and OnArrowSpawned (Client).
        // On Server: New arrow -> Freeze it.
        // On Client: New spawn -> Freeze it?
        // If Client joins late, OnArrowSpawned fires. If arrow is mid-air, Sync says Freeze=false.
        // If we set Freeze=true here, we stop it.
        // WORKAROUND: Only freeze if we govern it (our arrow) OR if it's brand new (velocity zero).

        if (arrow.LinearVelocity.LengthSquared() < 0.1f)
        {
            arrow.Freeze = true;
        }

        // Apply Player Color
        if (actualOwner != null) arrow.SetColor(GetPlayerColor(actualOwner.PlayerIndex));

        // Initial Pose only if it's OUR arrow and we haven't fired it (handled in UpdateArrowPose)
        if (actualOwner == _currentPlayer)
        {
            UpdateArrowPose();
        }
    }

    private void UpdateArrowPose()
    {
        if (_arrow == null || _currentPlayer == null || _arrow.HasBeenShot) return;

        Transform3D t;

        if (_handAttachment != null)
        {
            // Use Hand Attachment as base
            t = _handAttachment.GlobalTransform;

            // Base orientation correction for the bone (bones often point Y-up or weirdly)
            // We'll start with identity relative to bone and let calibration handle the rest, 
            // but usually we need at least some adjustment. 
            // Let's assume (0,0,0) for now and let the user calibrate.
        }
        else
        {
            // Fallback: Position relative to chest
            Vector3 spawnPos = _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f)));

            t = _currentPlayer.GlobalTransform;
            t.Origin = spawnPos;
            // Rotate 180 (Arrow orientation)
            t.Basis = t.Basis.Rotated(Vector3.Up, Mathf.Pi);
        }

        // Apply Calibrated Offsets + Runtime Debug Offsets
        Vector3 finalPos = _calibratedPos + DebugArrowOffsetPosition;
        t.Origin += t.Basis * finalPos;

        // Apply Calibrated Rotation + Runtime Debug Rotation
        Vector3 finalRot = _calibratedRot + DebugArrowOffsetRotation;

        if (finalRot != Vector3.Zero)
        {
            t.Basis = t.Basis.Rotated(t.Basis.X, Mathf.DegToRad(finalRot.X)); // Pitch
            t.Basis = t.Basis.Rotated(t.Basis.Y, Mathf.DegToRad(finalRot.Y)); // Yaw
            t.Basis = t.Basis.Rotated(t.Basis.Z, Mathf.DegToRad(finalRot.Z)); // Roll
        }

        _arrow.GlobalTransform = t;
        // Note: No network sync here - arrow position syncs at Launch time via RPC
    }

    public void CancelDraw()
    {
        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }

    public void ResetMatch()
    {
        // Refund current/last shot if it was just completed
        if (_stage == DrawStage.ShotComplete || _stage == DrawStage.Executing)
        {
            ArrowCount++;
        }

        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;

        // Clean up previous arrow and prepare a fresh one
        PrepareNextShot();

        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }
    public void RegisterPlayer(PlayerController player)
    {
		GD.Print($"ArcherySystem: RegisterPlayer called for {player.Name}, IsLocal={player.IsLocal}, Authority={player.GetMultiplayerAuthority()}, MyUniqueId={Multiplayer.GetUniqueId()}");

        _currentPlayer = player;
        if (_buildManager != null) _buildManager.Player = player;

        // Shared Setup: Find ErikaBow and setup BoneAttachment for EVERYONE (needed for visual sync)
		var erikaBow = player.GetNodeOrNull<Node3D>("ErikaBow");
        if (erikaBow != null)
        {
			var skeleton = erikaBow.GetNodeOrNull<Skeleton3D>("Skeleton3D");
            if (skeleton != null)
            {
                // Check if already exists (prevent duplicate on re-register)
				_handAttachment = skeleton.GetNodeOrNull<BoneAttachment3D>("RightHandArrowAttachment");

                if (_handAttachment == null)
                {
                    _handAttachment = new BoneAttachment3D();
					_handAttachment.Name = "RightHandArrowAttachment";
					_handAttachment.BoneName = "mixamorig_RightHand";
                    skeleton.AddChild(_handAttachment);
					GD.Print($"ArcherySystem: Created RightHandArrowAttachment on {player.Name}'s ErikaBow skeleton.");
                }
            }
        }

        // Link camera and input only if local
        if (player.IsLocal)
        {
            // Try direct child name first (standard), then property path
            var cam = player.GetNodeOrNull<CameraController>("Camera3D");
            if (cam == null && player.CameraPath != null && !player.CameraPath.IsEmpty)
            {
                cam = player.GetNodeOrNull<CameraController>(player.CameraPath);
            }

            if (cam != null)
            {
                _camera = cam;
                GD.Print($"ArcherySystem: Registered Local Player Camera: {_camera.Name}");
            }
            else
            {
                GD.PrintErr("ArcherySystem: Registered Local Player but could NOT find Camera!");
            }
        }
    }

    public void SetPrompt(bool visible, string message = "")
    {
        EmitSignal(SignalName.PromptChanged, visible, message);
    }

    public void ExitCombatMode()
    {
        _stage = DrawStage.Idle;

        // Hide held arrow for everyone
        if (_arrow != null && !_arrow.HasBeenShot)
        {
            _arrow.Visible = false;
        }

        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
        if (_camera != null)
        {
            _camera.SetTarget(_currentPlayer, true); // Snap to player


        }
        EmitSignal(SignalName.ModeChanged, false);
        SetPrompt(false, "");
    }

    public void EnterCombatMode()
    {
        _stage = DrawStage.Idle;
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.CombatArcher;

        EmitSignal(SignalName.ModeChanged, true);

        // Ensure current arrow is visible (if we were stowed)
        if (_arrow != null && !_arrow.HasBeenShot)
        {
            _arrow.Visible = true;
        }

        // Ensure an arrow is ready as soon as we enter combat
        if (_arrow == null || _arrow.HasBeenShot)
        {
            PrepareNextShot();
        }
    }

    public void EnterBuildMode()
    {
        _stage = DrawStage.Idle;
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.BuildMode;
        EmitSignal(SignalName.ModeChanged, false);
        SetPrompt(false);
    }

    public void ExitBuildMode()
    {
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
        SetPrompt(false);
    }

    public void CycleShotMode()
    {
        _currentMode = (ArcheryShotMode)(((int)_currentMode + 1) % 3);
        GD.Print($"[ArcherySystem] Shot Mode changed to: {_currentMode}");
        EmitSignal(SignalName.ShotModeChanged, (int)_currentMode);
    }

    public void CycleTarget(bool alliesOnly = false)
    {
        if (_currentPlayer == null) return;

        // Simple proximity-based target cycle
        var targets = new System.Collections.Generic.List<Node3D>();

        // Search for targetables with the specified filter
        FindTargetablesRecursive(GetTree().Root, targets, alliesOnly);

        if (targets.Count == 0)
        {
            ClearTarget();
            return;
        }

        // Sort by proximity to player
        targets.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(_currentPlayer.GlobalPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(_currentPlayer.GlobalPosition)));

        int currentIndex = (_currentTarget != null) ? targets.IndexOf(_currentTarget) : -1;
        int nextIndex = (currentIndex + 1) % targets.Count;

        // Deselect old target
        if (_currentTarget is InteractableObject oldIO) oldIO.SetSelected(false);

        _currentTarget = targets[nextIndex];
        GD.Print($"[ArcherySystem] Target Locked: {_currentTarget.Name} (AlliesOnly: {alliesOnly})");

        // Select new target
        if (_currentTarget is InteractableObject newIO) newIO.SetSelected(true);

        EmitSignal(SignalName.TargetChanged, _currentTarget);

        if (_camera != null && _camera is CameraController camCtrl)
        {
            camCtrl.SetLockedTarget(_currentTarget);
        }
    }

    private void FindTargetablesRecursive(Node node, System.Collections.Generic.List<Node3D> results, bool alliesOnly)
    {
        MobaTeam team = _currentPlayer?.Team ?? MobaTeam.None;
        TargetingHelper.FindTargetablesRecursive(node, results, team, alliesOnly);
    }

    public void ClearTarget()
    {
        if (_currentTarget == null) return;
        GD.Print("[ArcherySystem] Target Cleared");

        if (_currentTarget is InteractableObject io) io.SetSelected(false);

        _currentTarget = null;
        EmitSignal(SignalName.TargetChanged, null);

        if (_camera != null && _camera is CameraController camCtrl)
        {
            camCtrl.SetLockedTarget(null);
        }
    }

    private void OnArrowSettled(float distance)
    {
        SetPrompt(true, $"Shot settled: {distance * ArcheryConstants.UNIT_RATIO:F1}y");
    }

    private float CalculateOptimalLoft(Vector3 start, Vector3 target, float velocity)
    {
        return TargetingHelper.CalculateOptimalLoft(start, target, velocity, ArcheryConstants.GRAVITY);
    }

    public void StartCharge()
    {
        if (_bowCooldownRemaining > 0) return;

        _stage = DrawStage.Drawing;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
    }

    public void UpdateChargeProgress(float percent)
    {
        EmitSignal(SignalName.ArcheryValuesUpdated, percent, -1, -1);
    }

    public void ExecuteAttack(float holdTime)
    {
        if (_stage != DrawStage.Drawing) return;

        // Map hold duration to power (0-200 range)
        // < 0.75s: 50% power
        // 0.75s - 1.5s: 100% power
        // 1.5s - 2.5s: 150% power
        // >= 2.5s: 200% power
        float finalPower = 50f;
        if (holdTime >= 2.5f) finalPower = 200f;
        else if (holdTime >= 1.5f) finalPower = 150f;
        else if (holdTime >= 0.75f) finalPower = 100f;

        _lockedPower = finalPower;
        _lockedAccuracy = 100f; // Perfect accuracy for now in this simplified model
        _stage = DrawStage.Executing;

        EmitSignal(SignalName.ArcheryValuesUpdated, _lockedPower, _lockedPower, _lockedAccuracy);
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);

        ExecuteShot();
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
    }

    private void ExecuteShot()
    {
        // 1. Power Calculation (Incorporate Stats + Locked Power)
        float powerFactor = _lockedPower / 100.0f;
        float powerStatMult = PlayerStats.Power / 10.0f;

        // Apply Forgiveness (Snap to perfect)
        if (Mathf.Abs(_lockedPower - ArcheryConstants.PERFECT_POWER_VALUE) < ArcheryConstants.TOLERANCE_POWER)
        {
            _lockedPower = ArcheryConstants.PERFECT_POWER_VALUE;
            powerFactor = _lockedPower / 100.0f;
        }

        float velocityMag = ArcheryConstants.BASE_VELOCITY * powerFactor * powerStatMult;

        // 2. Accuracy Calculation
        // Perfect Target is 25.0 on the return trip.
        float accuracyError = _lockedAccuracy - ArcheryConstants.PERFECT_ACCURACY_VALUE;

        // Apply Forgiveness
        if (Mathf.Abs(accuracyError) < ArcheryConstants.TOLERANCE_ACCURACY)
        {
            accuracyError = 0.0f;
            _lockedAccuracy = ArcheryConstants.PERFECT_ACCURACY_VALUE;
        }

        // Power Multiplier for Error: Going over Perfect Power (94) amplifies accuracy errors
        if (_lockedPower > ArcheryConstants.PERFECT_POWER_VALUE)
        {
            float overPower = _lockedPower - ArcheryConstants.PERFECT_POWER_VALUE;
            accuracyError *= (1.0f + overPower * 0.15f); // 15% more error per point over perfect
        }

        if (_arrow != null)
        {
            Vector3 launchDir;
            if (_currentTarget != null)
            {
                // Snap aiming to target
                Vector3 targetPos = _currentTarget.GlobalPosition;
                if (_currentTarget is InteractableObject io)
                {
                    // Aim for the center of the mesh if possible
                    targetPos = io.GlobalPosition + new Vector3(0, 1.0f, 0); // Offset upwards slightly for signs
                }
                launchDir = (targetPos - _arrow.GlobalPosition).Normalized();
            }
            else
            {
                Vector3 camFwd = -_camera.GlobalBasis.Z;
                launchDir = (_camera != null) ? new Vector3(camFwd.X, 0, camFwd.Z).Normalized() : Vector3.Forward;
            }

            // Apply Loft
            float loftDeg = 12.0f;
            if (_currentTarget != null)
            {
                // SMART AUTO-LOFT: Calculate the best angle to hit the target
                Vector3 targetPos = _currentTarget.GlobalPosition;
                if (_currentTarget is InteractableObject io) targetPos += new Vector3(0, 1.0f, 0);

                loftDeg = CalculateOptimalLoft(_arrow.GlobalPosition, targetPos, velocityMag);
                GD.Print($"[ArcherySystem] Auto-Loft Calculated: {loftDeg:F1} degrees");
            }
            else
            {
                switch (_currentMode)
                {
                    case ArcheryShotMode.Standard: loftDeg = 12.0f; break;
                    case ArcheryShotMode.Long: loftDeg = 25.0f; break;
                    case ArcheryShotMode.Max: loftDeg = 45.0f; break;
                }
            }

            float loftRad = Mathf.DegToRad(loftDeg);
            launchDir.Y = Mathf.Sin(loftRad);
            launchDir = launchDir.Normalized();

            // Apply Accuracy Deviation (Left/Right)
            // User: "over 25 causes a right veering arrow, and after the line (lower than 25) to cause a left veering arrow"
            // accuracyError > 0 means lockedAccuracy > 25.0.
            // Right veer in Godot (with -Z Forward) is a NEGATIVE rotation around Up axis.
            float rotationDeg = -accuracyError * 0.75f; // +/- 0.75 deg per unit error
            launchDir = launchDir.Rotated(Vector3.Up, Mathf.DegToRad(rotationDeg));

            // Apply Wind to Arrow before launch
            if (_windSystem != null && _windSystem.IsWindEnabled)
            {
                Vector3 wind = _windSystem.WindDirection * _windSystem.WindSpeedMph;
                _arrow.SetWind(wind);
            }
            else if (_arrow != null)
            {
                _arrow.SetWind(Vector3.Zero);
            }

            if (Multiplayer.MultiplayerPeer != null && !Multiplayer.IsServer())
            {
                // Client: Request Server to launch our specific arrow (by Name)
                RpcId(1, nameof(RequestLaunchArrow), _arrow.Name, _arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero);
            }
            else
            {
                // Server / Singleplayer
                if (Multiplayer.MultiplayerPeer != null)
                {
                    // Broadcast Launch to all clients (including self via CallLocal)
                    _arrow.Rpc(nameof(ArrowController.Launch), _arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero);
                }
                else
                {
                    // Singleplayer local call
                    _arrow.Launch(_arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero);
                }
            }

            if (_camera != null)
            {
                // Camera recoil or follow logic here if needed
            }
        }

        EmitSignal(SignalName.ShotResult, _lockedPower, _lockedAccuracy);
        if (MobaGameManager.Instance == null)
        {
            ArrowCount--;
            UpdateArrowLabel();
        }
        _stage = DrawStage.ShotComplete;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);

        // Start bow cooldown
        _bowCooldownRemaining = BowCooldownTime;
        EmitSignal(SignalName.BowCooldownUpdated, _bowCooldownRemaining, BowCooldownTime);

        GD.Print($"[ArcherySystem] Shot Executed. Power: {_lockedPower:F1}, Accuracy: {_lockedAccuracy:F1}, Error: {accuracyError:F2}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RequestLaunchArrow(string arrowName, Vector3 startPosition, Vector3 startRotation, Vector3 velocity, Vector3 spin)
    {
        // Received on Server from Client
        // Find the specific arrow instance
        var projectiles = GetTree().CurrentScene.GetNodeOrNull("Projectiles");
        var arrow = projectiles?.GetNodeOrNull<ArrowController>(arrowName);

        if (arrow != null)
        {
            // Broadcast execution to all (Syncs physics/visuals)
            arrow.Rpc(nameof(ArrowController.Launch), startPosition, startRotation, velocity, spin);
            GD.Print($"[ArcherySystem] Server launching Client arrow: {arrowName}");
        }
        else
        {
			GD.PrintErr($"[ArcherySystem] RequestLaunchArrow failed: Could not find arrow '{arrowName}'");
        }
    }


    private Color GetPlayerColor(int index)
    {
        return TargetingHelper.GetPlayerColor(index);
    }
}
