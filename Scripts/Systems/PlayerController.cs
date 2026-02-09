using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float RotationSpeed = 1.0f;
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float JumpForce = 5.0f;
    [Export] public float LookSensitivity = 0.5f;
    [Export] public MobaTeam Team = MobaTeam.None;
    [Export] public float Gravity = 9.8f;
    [Export] public NodePath CameraPath { get; set; }
    [Export] public NodePath ArcherySystemPath;
    [Export] public NodePath MeleeSystemPath;
    [Export] public NodePath AnimationTreePath;

    // Physics State
    private Vector3 _velocity = Vector3.Zero;
    private float _attackHoldTimer = 0f;
    private bool _isChargingAttack = false;
    private ChargeBar3D _chargeBar;
    private bool _isGrounded = true;

    // Dash / Vault State
    [Export] public float DashSpeed = 15.0f;
    [Export] public float DashDuration = 0.3f;
    private float _dashTime = 0.0f;
    private Vector3 _dashDir = Vector3.Zero;
    private uint _originalMask;
    private bool _isVaulting = false;

    // Multiplayer Properties
    [Export] public int PlayerIndex { get; set; } = 0;
    private int _lastPlayerIndex = -1;

    public bool IsLocal
    {
        get
        {
            if (Multiplayer == null || Multiplayer.MultiplayerPeer == null ||
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
            {
                return true;
            }
            return IsMultiplayerAuthority();
        }
    }

    private PlayerState _currentState = PlayerState.WalkMode;
    public PlayerState CurrentState
    {
        get => _currentState;
        set
        {
            GD.Print($"[PlayerController] State changing from {_currentState} to {value}");
            _currentState = value;
        }
    }

    private CameraController _camera;
    private MeshInstance3D _avatarMesh;
    private ArcherySystem _archerySystem;


    private InteractableObject _selectedObject;
    public InteractableObject SelectedObject => _selectedObject;
    private InteractableObject _lastHoveredObject;
    private MainHUDController _hud;
    private MeleeSystem _meleeSystem;
    private AnimationTree _animTree;
    private AnimationPlayer _animPlayer;
    private Dictionary<int, HeroAbilityBase> _abilities = new();
    private AnimationPlayer _meleeAnimPlayer;
    private AnimationPlayer _archeryAnimPlayer;
    private Node3D _meleeModel;
    private Node3D _archeryModel;
    private bool _isSprinting = false;
    public bool IsSprinting
    {
        get => _isSprinting;
        set => _isSprinting = value;
    }

    public string SynchronizedModel
    {
        get => _modelManager?.CurrentModelId ?? _currentModelId;
        set
        {
            _currentModelId = value;
            if (_modelManager != null && _modelManager.CurrentModelId != value && !string.IsNullOrEmpty(value))
            {
                _modelManager.SetCharacterModel(value);
            }
        }
    }

    private bool _isJumping = false;

    public virtual void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal, Node attacker = null)
    {
        // Server handles authoritative state; Local provides immediate feedback
        if (Multiplayer.IsServer() || IsLocal)
        {
            GD.Print($"[Player {PlayerIndex}] Hit for {damage} by {attacker?.Name ?? "Unknown"}");

            var stats = _archerySystem?.PlayerStats;
            if (stats != null)
            {
                stats.CurrentHealth -= (int)damage;
                if (stats.CurrentHealth < 0) stats.CurrentHealth = 0;

                GD.Print($"[Player {PlayerIndex}] HP: {stats.CurrentHealth}/{stats.MaxHealth}");

                // Trigger death if HP reaches zero (placeholder for Respawn logic)
                if (stats.CurrentHealth <= 0)
                {
                    GD.Print($"[Player {PlayerIndex}] DIED!");
                }
            }
        }
    }
    private bool _isIntercepting = false;
    private float _interceptTime = 0.0f;
    private Vector3 _interceptDir = Vector3.Forward;

    // Buffs
    public bool IsCCImmune { get; private set; } = false;
    public float LifestealPercent { get; private set; } = 0f;
    private float _ccImmunityTimer = 0f;
    private float _lifestealTimer = 0f;

    public int SynchronizedTool
    {
        get => (int)_currentTool;
        set
        {
            var newTool = (ToolType)value;
            if (_currentTool != newTool)
            {
                ApplyToolChange(newTool);
            }
        }
    }

    private ToolType _currentTool = ToolType.None;
    private SwordController _sword;
    private BowController _standaloneBow;
    private float _inputCooldown = 0.0f;
    private Archery.DrawStage _lastArcheryStage = Archery.DrawStage.Idle;

    private string _currentModelId = "Ranger";
    public string CurrentModelId => _modelManager?.CurrentModelId ?? _currentModelId;
    private CharacterModelManager _modelManager;
    private int _remoteArcheryStage = 0;
    private int _remoteMeleeStage = 0;

    public int SynchronizedArcheryStage
    {
        get => IsLocal ? (_archerySystem != null ? (int)_archerySystem.CurrentStage : 0) : _remoteArcheryStage;
        set => _remoteArcheryStage = value;
    }

    public int SynchronizedMeleeStage
    {
        get => IsLocal ? (_meleeSystem != null ? (int)_meleeSystem.CurrentState : 0) : _remoteMeleeStage;
        set => _remoteMeleeStage = value;
    }

    [Export] public float HeadXRotation { get; set; }

    // Hybrid Targeting State
    private Node3D _hardLockTarget;
    private Node3D _fluidTarget;
    private Node3D _lastTarget;
    private bool _isMouseCaptured = false;
    private bool _isAltPressed = false;

    public Node3D CurrentTarget => (_hardLockTarget != null && TargetingHelper.IsTargetDead(_hardLockTarget)) ? null : (_hardLockTarget ?? _fluidTarget);
    public Node3D HardLockTarget => _hardLockTarget;
    public Node3D FluidTarget => _fluidTarget;

    [Signal] public delegate void HitScoredEventHandler();

    public override void _EnterTree()
    {
        if (long.TryParse(Name, out long id))
        {
            SetMultiplayerAuthority((int)id);
        }
    }

    public override void _Ready()
    {
        GD.Print($"[PlayerController] _Ready starting for {Name}. Authority: {GetMultiplayerAuthority()}, IsLocal: {IsLocal}");

        if (!CameraPath.IsEmpty)
        {
            _camera = GetNodeOrNull<CameraController>(CameraPath);
        }

        if (_camera != null)
        {
            _camera.Current = IsLocal;
        }

        _archerySystem = GetNodeOrNull<ArcherySystem>("ArcherySystem");
        _meleeSystem = GetNodeOrNull<MeleeSystem>("MeleeSystem");

        if (_archerySystem == null && !ArcherySystemPath.IsEmpty)
            _archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);

        if (_meleeSystem == null && !MeleeSystemPath.IsEmpty)
            _meleeSystem = GetNodeOrNull<MeleeSystem>(MeleeSystemPath);

        _avatarMesh = GetNodeOrNull<MeshInstance3D>("AvatarMesh");

        // Determine if this hero uses a custom skeleton (own model/anims)
        var registry = CharacterRegistry.Instance;
        var heroModel = registry?.GetModel(_currentModelId);
        bool isCustomSkeleton = heroModel?.IsCustomSkeleton ?? false;

        GD.Print($"[PlayerController] Hero: {_currentModelId}, CustomSkeleton: {isCustomSkeleton}");

        if (!isCustomSkeleton)
        {
            // Shared skeleton (Ranger, Cleric) — use Erika rig
            if (!AnimationTreePath.IsEmpty)
                _animTree = GetNodeOrNull<AnimationTree>(AnimationTreePath);

            if (_animTree == null)
                _animTree = GetNodeOrNull<AnimationTree>("AnimationTree");

            _meleeModel = GetNodeOrNull<Node3D>("Erika");
            _archeryModel = GetNodeOrNull<Node3D>("ErikaBow");

            _meleeAnimPlayer = GetNodeOrNull<AnimationPlayer>("Erika/AnimationPlayer");
            _archeryAnimPlayer = GetNodeOrNull<AnimationPlayer>("ErikaBow/AnimationPlayer");
        }
        else
        {
            // Custom skeleton (Warrior, Necromancer) — skip Erika entirely
            var erika = GetNodeOrNull<Node3D>("Erika");
            var erikaBow = GetNodeOrNull<Node3D>("ErikaBow");
            if (erika != null) erika.Visible = false;
            if (erikaBow != null) erikaBow.Visible = false;

            // Disable Erika's AnimationTree — custom models use their own AnimationPlayer
            var animTreeNode = GetNodeOrNull<AnimationTree>("AnimationTree");
            if (animTreeNode != null) animTreeNode.Active = false;

            // Null out Erika refs — not needed
            _meleeModel = null;
            _archeryModel = null;
            _meleeAnimPlayer = null;
            _archeryAnimPlayer = null;
            _animTree = null;
        }

        if (_modelManager == null)
            _modelManager = GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        if (_modelManager == null)
        {
            _modelManager = new CharacterModelManager();
            _modelManager.Name = "ModelManager";
            AddChild(_modelManager);
        }

        _chargeBar = new ChargeBar3D();
        _chargeBar.Name = "ChargeBar3D";
        AddChild(_chargeBar);
        _chargeBar.Position = new Vector3(0, 0.15f, 0);

        _modelManager.Initialize(this, _meleeModel, _archeryModel, _animTree, _meleeAnimPlayer, _archeryAnimPlayer);
        InitializeAbilities(_currentModelId);
        _modelManager.SetCharacterModel(_currentModelId);

        if (IsLocal)
        {
            AddToGroup("local_player");
            AddToGroup("player"); // Core group for Monster AI detection

            // JOIN TEAM GROUPS: Critical for MobaTower and MobaMinion discovery
            if (Team != MobaTeam.None) AddToGroup($"team_{Team.ToString().ToLower()}");

            _hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;
            if (_hud != null) _hud.RegisterPlayer(this);

            if (_camera != null)
            {
                _camera.SnapBehind(this);
                _camera.MakeCurrent();
            }

            if (ToolManager.Instance != null)
            {
                ToolManager.Instance.ToolChanged += OnToolChanged;
                ToolManager.Instance.HotbarModeChanged += OnHotbarModeChanged;
                ToolManager.Instance.AbilityTriggered += OnAbilityTriggered;
            }

            _archerySystem?.PlayerStatsService?.Connect("PerkSelected", new Callable(this, nameof(OnPerkSelected)));
        }
        else
        {
            if (_camera != null) { _camera.QueueFree(); _camera = null; }
            var aimAssist = GetNodeOrNull<Node3D>("AimAssist");
            if (aimAssist != null) aimAssist.QueueFree();
        }

        if (_meleeSystem != null)
        {
            _meleeSystem.RegisterPlayer(this);
            _meleeSystem.PowerSlamTriggered += OnPowerSlamTriggered;
        }
        if (_archerySystem != null) _archerySystem.RegisterPlayer(this);

        SetupVisualSword();
        UpdatePlayerColor();
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessBuffs((float)delta);

        if (_lastPlayerIndex != PlayerIndex)
        {
            _lastPlayerIndex = PlayerIndex;
            UpdatePlayerColor();
        }

        UpdateAnimations(delta);

        if (!IsLocal) return;

        UpdateSyncProperties(delta);

        if (_inputCooldown > 0) _inputCooldown -= (float)delta;

        // Mouse capture logic
        UpdateMouseCapture();

        // Essential: Run charge logic even in WalkMode for RPG combat
        HandleCombatCharge(delta);

        // Fluid Smart Targeting update
        UpdateFluidTargeting();

        if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher)
        {
            HandleBodyMovement(delta);
            HandleTargetingHotkeys();
        }

        switch (CurrentState)
        {
            case PlayerState.CombatMelee:
            case PlayerState.CombatArcher:
                HandleCombatInput(delta);
                break;
            case PlayerState.WalkMode:
                PlayerInteraction.HandleProximityPrompts(this, _archerySystem);
                break;
            case PlayerState.BuildMode:
                HandleBuildModeInput(delta);
                break;
        }
    }

    private void UpdateAnimations(double delta)
    {
        PlayerAnimations.UpdateAnimations(_animTree, this, _meleeSystem, _archerySystem, _modelManager, MoveSpeed, _isJumping, Velocity, ref _lastArcheryStage);
    }

    private void OnAbilityTriggered(int index)
    {
        if (!IsLocal) return;
        TriggerAbility(index);
    }

    public void PerformVault()
    {
        if (_isVaulting) return;
        _isVaulting = true;
        _dashTime = DashDuration;
        _dashDir = GlobalBasis.Z; // Forward leap
        _dashDir.Y = 0;
        _dashDir = _dashDir.Normalized();
        _originalMask = CollisionMask;
        CollisionMask = 3;
        _velocity.Y = JumpForce * 0.7f;
        _isJumping = true;
        _modelManager?.UpdateCustomAnimations(false, false, true, false, false);
    }

    public void OnPerkSelected(string perkId)
    {
        GD.Print($"[PlayerController] Perk Selected: {perkId}. Applying effects...");
        foreach (var ability in _abilities.Values)
        {
            if (perkId.Contains("dmg")) ability.DamageMultiplier *= 1.2f;
            if (perkId.Contains("cdr")) ability.CooldownReduction += 0.5f;
            if (perkId.Contains("radius")) ability.RadiusBonus += 1.0f;
        }
    }

    public void PerformIntercept()
    {
        if (_isIntercepting) return;
        _isIntercepting = true;
        _interceptTime = 0.4f; // Flat duration for consistency
        _interceptDir = GlobalBasis.Z; // REVERTED: Direction as per user preference
        _interceptDir.Y = 0;
        _interceptDir = _interceptDir.Normalized();

        // Also apply a small jump/hop for flair
        _velocity.Y = JumpForce * 0.5f;
        _isJumping = true;
    }

    public void ApplyCCImmunity(float duration)
    {
        _ccImmunityTimer = duration;
        IsCCImmune = true;
        GD.Print($"[PlayerController] CC Immunity active for {duration}s");
    }

    public void ApplyLifesteal(float percent, float duration)
    {
        LifestealPercent = percent;
        _lifestealTimer = duration;
        GD.Print($"[PlayerController] Lifesteal ({percent * 100}%) active for {duration}s");
    }

    private void ProcessBuffs(float dt)
    {
        if (_ccImmunityTimer > 0)
        {
            _ccImmunityTimer -= dt;
            if (_ccImmunityTimer <= 0) IsCCImmune = false;
        }

        if (_lifestealTimer > 0)
        {
            _lifestealTimer -= dt;
            if (_lifestealTimer <= 0) LifestealPercent = 0f;
        }
    }
}
