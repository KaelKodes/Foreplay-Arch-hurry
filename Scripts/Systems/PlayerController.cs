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
    public bool SynchronizedVaulting
    {
        get => _isVaulting;
        set => _isVaulting = value;
    }
    public float CurrentDashTime => _dashTime;

    // Multiplayer Properties
    [Export] public int PlayerIndex { get; set; } = 0;
    private int _lastPlayerIndex = -1;

    // ── Bot Support ───────────────────────────────────
    public bool IsBot { get; private set; } = false;
    public BotInputProvider BotInput { get; private set; }

    public bool IsLocal
    {
        get
        {
            if (IsBot) return false; // Bots are never "local" — driven by HeroBrain
            if (Multiplayer == null || Multiplayer.MultiplayerPeer == null ||
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
            {
                return true;
            }
            return IsMultiplayerAuthority();
        }
    }

    /// <summary>
    /// Initialize this PlayerController as a bot. Called by NetworkManager after spawning.
    /// </summary>
    public void InitializeAsBot(LobbyPlayerData data)
    {
        IsBot = true;
        BotInput = new BotInputProvider();
        GD.Print($"[PlayerController] Initialized as bot: {data.Name} ({data.ClassName}) on {data.Team}");
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
    private HealthBar3D _heroHealthBar;
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

    private float _moveSpeedMultiplier = 1.0f;
    private float _moveSpeedTimer = 0f;

    // Haste Buff (from Rapid Fire etc.)
    private int _hasteBuffAmount = 0;
    private float _hasteBuffTimer = 0f;
    private float _hastePulseTimer = 0f;

    // Avatar of War red pulse
    private float _avatarPulseTimer = 0f;

    // Speed Buff (Celestial Buff: Haste + Concentration + Agility)
    private int _speedBuffHaste = 0;
    private int _speedBuffConc = 0;
    private int _speedBuffAgi = 0;
    private float _speedBuffTimer = 0f;
    private float _speedPulseTimer = 0f;

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
                float remainingDamage = damage;

                // 1. Consume Shield first
                if (stats.CurrentShield > 0)
                {
                    if (stats.CurrentShield >= remainingDamage)
                    {
                        stats.CurrentShield -= (int)remainingDamage;
                        remainingDamage = 0;
                    }
                    else
                    {
                        remainingDamage -= stats.CurrentShield;
                        stats.CurrentShield = 0;
                    }
                }

                // 2. Consume Health
                if (remainingDamage > 0)
                {
                    stats.CurrentHealth -= (int)remainingDamage;
                    if (stats.CurrentHealth < 0) stats.CurrentHealth = 0;
                }

                GD.Print($"[Player {PlayerIndex}] HP: {stats.CurrentHealth}/{stats.MaxHealth}, Shield: {stats.CurrentShield}");

                // Update floating HP bar
                UpdateHeroHealthBar();

                // Trigger death if HP reaches zero
                if (stats.CurrentHealth <= 0 && CurrentState != PlayerState.Dead)
                {
                    Die();
                }
            }
        }
    }

    private void DeferredCreateHeroHealthBar()
    {
        if (IsLocal) return; // Local player sees their HP in the HUD
        var scene = GD.Load<PackedScene>("res://Scenes/UI/Combat/HealthBar3D.tscn");
        if (scene != null)
        {
            _heroHealthBar = scene.Instantiate<HealthBar3D>();
            AddChild(_heroHealthBar);
            _heroHealthBar.Position = new Vector3(0, 2.5f, 0);
        }
    }

    private void UpdateHeroHealthBar()
    {
        if (_heroHealthBar == null) return;
        var stats = _archerySystem?.PlayerStats;
        if (stats == null) return;
        _heroHealthBar.UpdateHealth(stats.CurrentHealth, stats.MaxHealth, stats.CurrentShield);
        _heroHealthBar.Visible = CurrentState != PlayerState.Dead;
    }
    private bool _isIntercepting = false;
    private float _interceptTime = 0.0f;
    private Vector3 _interceptDir = Vector3.Forward;

    // Buffs
    public bool IsCCImmune { get; private set; } = false;
    public float LifestealPercent { get; private set; } = 0f;
    private float _ccImmunityTimer = 0f;
    private float _lifestealTimer = 0f;
    private float _shieldTimer = 0f;

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
    private float _abilityBusyTimer = 0f;
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
    private Node3D _lastLockTarget;
    private bool _isMouseCaptured = false;
    private bool _isAltPressed = false;

    // Performance Optimization: Phase 2
    private float _targetPingTimer = 0f;
    private const float TargetPingInterval = 0.2f;
    private List<Node3D> _cachedPotentialTargets = new List<Node3D>();

    public Node3D CurrentTarget => (_hardLockTarget != null && TargetingHelper.IsTargetDead(_hardLockTarget)) ? null : (_hardLockTarget ?? _fluidTarget);
    public Node3D HardLockTarget => _hardLockTarget;
    public Node3D FluidTarget => _fluidTarget;

    [Signal] public delegate void HitScoredEventHandler();
    [Signal] public delegate void AbilityUsedEventHandler(int slotIndex, float cooldownDuration);
    [Signal] public delegate void RespawnTimerUpdatedEventHandler(float remaining);
    [Signal] public delegate void PlayerDiedEventHandler();
    [Signal] public delegate void PlayerRespawnedEventHandler();

    // Recall
    private float _recallCooldown = 0f;
    private const float RecallCooldownDuration = 60f;
    private bool _isDead = false;
    private float _respawnTimer = 0f;
    private const float RespawnDuration = 5f;

    public override void _EnterTree()
    {
        if (long.TryParse(Name, out long id))
        {
            SetMultiplayerAuthority((int)id);
        }
    }

    public override void _Ready()
    {
        // PhysicsInterpolationMode = PhysicsInterpolationModeEnum.On; // API not available
        AddToGroup("Players");
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
        AddToGroup("player");
        AddToGroup("targetables");

        // Floating HP bar for non-local heroes (so you can see enemy/ally HP)
        CallDeferred(nameof(DeferredCreateHeroHealthBar));
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
        else if (IsBot)
        {
            // Bots need team groups and combat systems, but no camera/HUD/input
            AddToGroup("player");
            if (Team != MobaTeam.None) AddToGroup($"team_{Team.ToString().ToLower()}");
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

        // ── Bot processing ───────────────────────────────────────
        if (IsBot)
        {
            ProcessBotPhysics(delta);
            return;
        }

        if (!IsLocal) return;

        // ── Tick recall cooldown ──────────────────────────────────
        if (_recallCooldown > 0f)
        {
            _recallCooldown -= (float)delta;
            if (_recallCooldown < 0f) _recallCooldown = 0f;
        }

        // ── Dead state: only tick respawn timer, no input ─────────
        if (CurrentState == PlayerState.Dead)
        {
            _respawnTimer -= (float)delta;
            EmitSignal(SignalName.RespawnTimerUpdated, _respawnTimer);
            if (_respawnTimer <= 0f)
            {
                Respawn();
            }
            return; // Block ALL other input while dead
        }

        // ── Tick ability cooldowns ────────────────────────────────
        if (_abilityBusyTimer > 0f)
        {
            _abilityBusyTimer -= (float)delta;
            if (_abilityBusyTimer < 0f) _abilityBusyTimer = 0f;
        }
        foreach (var ability in _abilities.Values)
            ability.UpdateCooldown((float)delta);

        if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher)
        {
            HandleBodyMovement(delta);
        }

        UpdateSyncProperties(delta);

        if (_inputCooldown > 0) _inputCooldown -= (float)delta;

        // Mouse capture logic
        UpdateMouseCapture();

        // Essential: Run charge logic even in WalkMode for RPG combat
        HandleCombatCharge(delta);

        // Fluid Smart Targeting update
        UpdateFluidTargeting((float)delta);

        if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher)
        {
            HandleTargetingHotkeys();
        }

        switch (CurrentState)
        {
            case PlayerState.CombatMelee:
            case PlayerState.CombatArcher:
                HandleCombatInput(delta);
                break;
            case PlayerState.WalkMode:
                // No proximity prompts anymore (User Request)
                _archerySystem?.SetPrompt(false);
                break;
            case PlayerState.BuildMode:
                HandleBuildModeInput(delta);
                break;
        }
    }

    /// <summary>
    /// Physics processing for bot-controlled players.
    /// Reads from BotInputProvider instead of Godot Input.
    /// </summary>
    private void ProcessBotPhysics(double delta)
    {
        float dt = (float)delta;

        // ── Tick recall cooldown ─────────────────────────────────
        if (_recallCooldown > 0f)
        {
            _recallCooldown -= dt;
            if (_recallCooldown < 0f) _recallCooldown = 0f;
        }

        // ── Dead state ───────────────────────────────────────────
        if (CurrentState == PlayerState.Dead)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0f) Respawn();
            return;
        }

        // ── Tick ability cooldowns ───────────────────────────────
        if (_abilityBusyTimer > 0f)
        {
            _abilityBusyTimer -= dt;
            if (_abilityBusyTimer < 0f) _abilityBusyTimer = 0f;
        }
        foreach (var ability in _abilities.Values)
            ability.UpdateCooldown(dt);

        if (BotInput == null) return;

        // ── Movement from BotInputProvider ───────────────────────
        HandleBotMovement(delta);

        // ── Recall ───────────────────────────────────────────────
        if (BotInput.WantRecall && _recallCooldown <= 0f)
        {
            TriggerRecall();
        }

        // ── Target locking ───────────────────────────────────────
        if (BotInput.DesiredTarget != null)
        {
            _hardLockTarget = BotInput.DesiredTarget;
            _archerySystem?.SetTarget(_hardLockTarget);
        }

        // ── Abilities ────────────────────────────────────────────
        if (BotInput.WantAbility >= 0 && BotInput.WantAbility < 3)
        {
            TriggerAbility(BotInput.WantAbility);
        }

        // ── Attack ───────────────────────────────────────────────
        if (BotInput.WantAttackPress)
        {
            // Uses melee or ranged based on class (same as human players)
            PerformBasicAttack();
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

        GD.Print("[PlayerController] Vault started");
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
            if (_ccImmunityTimer <= 0)
            {
                IsCCImmune = false;
                GD.Print("[PlayerController] CC Immunity expired.");
            }
        }

        if (_moveSpeedTimer > 0)
        {
            _moveSpeedTimer -= dt;
            if (_moveSpeedTimer <= 0)
            {
                _moveSpeedMultiplier = 1.0f;
                GD.Print("[PlayerController] Move Speed buff expired.");
            }
        }

        if (_lifestealTimer > 0)
        {
            _lifestealTimer -= dt;
            _avatarPulseTimer += dt;

            // Red pulse while Avatar of War is active
            float pulse = (Mathf.Sin(_avatarPulseTimer * 6f) + 1f) * 0.5f; // 0..1
            ApplyEmissionToAllMeshes(new Color(pulse * 0.8f, 0.05f, 0.05f), pulse * 2.5f);

            if (_lifestealTimer <= 0)
            {
                LifestealPercent = 0f;
                _avatarPulseTimer = 0f;
                ApplyEmissionToAllMeshes(Colors.Black, 0f, clearEmission: true);
                GD.Print("[PlayerController] Avatar of War expired.");
            }
        }

        if (_shieldTimer > 0)
        {
            _shieldTimer -= dt;
            if (_shieldTimer <= 0)
            {
                var stats = _archerySystem?.PlayerStats;
                if (stats != null) stats.CurrentShield = 0;
                GD.Print("[PlayerController] Shield expired.");
            }
        }

        // Haste Buff countdown + green pulse
        if (_hasteBuffTimer > 0)
        {
            _hasteBuffTimer -= dt;
            _hastePulseTimer += dt;

            // Pulse green on model (sine wave for smooth pulsing)
            float pulse = (Mathf.Sin(_hastePulseTimer * 6f) + 1f) * 0.5f; // 0..1
            ApplyEmissionToAllMeshes(new Color(0.0f, pulse * 0.6f, 0.0f), pulse * 2.0f);

            if (_hasteBuffTimer <= 0)
            {
                // Remove buff
                var statsB = _archerySystem?.PlayerStats;
                if (statsB != null && _hasteBuffAmount > 0)
                {
                    statsB.Haste -= _hasteBuffAmount;
                    _hasteBuffAmount = 0;
                    GD.Print($"[PlayerController] Haste buff expired. Haste: {statsB.Haste}");
                }

                // Clear green pulse
                ApplyEmissionToAllMeshes(Colors.Black, 0f, clearEmission: true);
            }
        }

        // Speed Buff countdown + gold pulse (Celestial Buff)
        if (_speedBuffTimer > 0)
        {
            _speedBuffTimer -= dt;
            _speedPulseTimer += dt;

            // Pulse gold/yellow while Speed buffed
            float pulse = (Mathf.Sin(_speedPulseTimer * 4f) + 1f) * 0.5f;
            ApplyEmissionToAllMeshes(new Color(pulse * 0.8f, pulse * 0.6f, 0.05f), pulse * 1.5f);

            if (_speedBuffTimer <= 0)
            {
                var statsS = _archerySystem?.PlayerStats;
                if (statsS != null)
                {
                    statsS.Haste -= _speedBuffHaste;
                    statsS.Concentration -= _speedBuffConc;
                    statsS.Agility -= _speedBuffAgi;
                    GD.Print($"[PlayerController] Speed buff expired. Haste:{statsS.Haste} Conc:{statsS.Concentration} Agi:{statsS.Agility}");
                }
                _speedBuffHaste = 0;
                _speedBuffConc = 0;
                _speedBuffAgi = 0;
                _speedPulseTimer = 0f;
                ApplyEmissionToAllMeshes(Colors.Black, 0f, clearEmission: true);
            }
        }
    }

    /// <summary>
    /// Recursively applies emission to all MeshInstance3D nodes on the active character model.
    /// </summary>
    private void ApplyEmissionToAllMeshes(Color emissionColor, float energy, bool clearEmission = false)
    {
        var meshes = new System.Collections.Generic.List<MeshInstance3D>();
        Node3D modelRoot = _meleeModel ?? _modelManager?.ActiveModelRoot;
        if (modelRoot != null) CollectMeshes(modelRoot, meshes);

        foreach (var mesh in meshes)
        {
            var mat = mesh.GetActiveMaterial(0);
            if (mat is StandardMaterial3D stdMat)
            {
                if (clearEmission)
                {
                    stdMat.EmissionEnabled = false;
                    stdMat.Emission = Colors.Black;
                    stdMat.EmissionEnergyMultiplier = 0f;
                }
                else
                {
                    stdMat.EmissionEnabled = true;
                    stdMat.Emission = emissionColor;
                    stdMat.EmissionEnergyMultiplier = energy;
                }
            }
        }
    }

    private void CollectMeshes(Node node, System.Collections.Generic.List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (Node child in node.GetChildren()) CollectMeshes(child, meshes);
    }

    public void Heal(float amount)
    {
        if (amount <= 0) return;

        var stats = _archerySystem?.PlayerStats;
        if (stats != null)
        {
            stats.CurrentHealth = Mathf.Clamp(stats.CurrentHealth + (int)amount, 0, stats.MaxHealth);
            SpawnHealNumber(amount);
            GD.Print($"[Player {PlayerIndex}] Healed for {amount}. HP: {stats.CurrentHealth}/{stats.MaxHealth}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  DEATH / RESPAWN
    // ══════════════════════════════════════════════════════════════

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        CurrentState = PlayerState.Dead;
        _respawnTimer = RespawnDuration;

        // Play death animation
        var modelMgr = GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? GetNodeOrNull<CharacterModelManager>("CharacterModelManager");
        modelMgr?.PlayAnimation("Death");

        EmitSignal(SignalName.PlayerDied);
        GD.Print($"[Player {PlayerIndex}] DIED! Respawning in {RespawnDuration}s...");
    }

    private void Respawn()
    {
        _isDead = false;
        CurrentState = PlayerState.WalkMode;

        // Restore HP
        var stats = _archerySystem?.PlayerStats;
        if (stats != null)
        {
            stats.CurrentHealth = stats.MaxHealth;
            stats.CurrentShield = 0;
        }

        // Teleport to team nexus
        Vector3 spawnPos = GetTeamSpawnPosition();
        TeleportTo(spawnPos, spawnPos + Vector3.Forward * 10f);

        // Bots: offset past the nexus so they don't get stuck on it
        if (IsBot && MobaGameManager.Instance != null)
        {
            bool isRed = Team == MobaTeam.Red;
            Vector3 ownNexus = isRed ? MobaGameManager.Instance.RedSpawnPos : MobaGameManager.Instance.BlueSpawnPos;
            Vector3 enemyNexus = isRed ? MobaGameManager.Instance.BlueSpawnPos : MobaGameManager.Instance.RedSpawnPos;
            Vector3 laneDir = (enemyNexus - ownNexus).Normalized();
            laneDir.Y = 0;
            float lateralOffset = (float)GD.RandRange(-2.0, 2.0);
            Vector3 lateral = new Vector3(-laneDir.Z, 0, laneDir.X) * lateralOffset;
            GlobalPosition = ownNexus + laneDir * 10f + lateral + Vector3.Up * 1f;
        }

        // Play idle anim to reset from death
        var modelMgr = GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? GetNodeOrNull<CharacterModelManager>("CharacterModelManager");
        modelMgr?.PlayAnimation("Idle");

        EmitSignal(SignalName.PlayerRespawned);
        GD.Print($"[Player {PlayerIndex}] Respawned at {GlobalPosition} with full HP ({stats?.MaxHealth})");
    }

    // ══════════════════════════════════════════════════════════════
    //  RECALL
    // ══════════════════════════════════════════════════════════════

    public void TriggerRecall()
    {
        if (CurrentState == PlayerState.Dead) return;

        if (_recallCooldown > 0f)
        {
            GD.Print($"[Player {PlayerIndex}] Recall on cooldown ({_recallCooldown:F1}s remaining)");
            return;
        }

        // Teleport to team nexus
        Vector3 spawnPos = GetTeamSpawnPosition();
        TeleportTo(spawnPos, spawnPos + Vector3.Forward * 10f);

        // Start cooldown
        _recallCooldown = RecallCooldownDuration;

        // Notify UI (slot 4 = Recall)
        EmitSignal(SignalName.AbilityUsed, 4, RecallCooldownDuration);

        GD.Print($"[Player {PlayerIndex}] Recalled to nexus at {spawnPos}. Cooldown: {RecallCooldownDuration}s");
    }

    /// <summary>
    /// Resolves the team's spawn point (near nexus) for respawn/recall.
    /// </summary>
    private Vector3 GetTeamSpawnPosition()
    {
        string teamSpawnName = $"SpawnPoint_{Team}";
        Node3D spawnPoint = GetTree().CurrentScene.FindChild(teamSpawnName, true, false) as Node3D;

        if (spawnPoint != null)
        {
            float rngX = (float)GD.RandRange(-1.5, 1.5);
            float rngZ = (float)GD.RandRange(-1.5, 1.5);
            return spawnPoint.GlobalPosition + new Vector3(rngX, 2f, rngZ);
        }

        // Fallback: MobaGameManager positions
        if (MobaGameManager.Instance != null)
        {
            MobaTeam finalTeam = (Team == MobaTeam.None) ? MobaTeam.Red : Team;
            return finalTeam == MobaTeam.Red
                ? MobaGameManager.Instance.RedSpawnPos
                : MobaGameManager.Instance.BlueSpawnPos;
        }

        return GlobalPosition; // Last resort: stay put
    }

    public void ApplyMoveSpeedBuff(float multiplier, float duration)
    {
        _moveSpeedMultiplier = multiplier;
        _moveSpeedTimer = duration;
        GD.Print($"[PlayerController] Move Speed set to {multiplier}x for {duration}s");
    }

    /// <summary>
    /// Grants a temporary Haste bonus (e.g. +25 Haste from Rapid Fire).
    /// While active, the player pulses green.
    /// </summary>
    public void ApplyHasteBuff(int hasteAmount, float duration)
    {
        var stats = _archerySystem?.PlayerStats;
        if (stats != null)
        {
            // Remove old buff if re-applying
            if (_hasteBuffAmount > 0) stats.Haste -= _hasteBuffAmount;

            _hasteBuffAmount = hasteAmount;
            stats.Haste += _hasteBuffAmount;
            _hasteBuffTimer = duration;
            _hastePulseTimer = 0f;
            GD.Print($"[PlayerController] Haste buff +{hasteAmount} active for {duration}s (total Haste: {stats.Haste})");
        }
    }

    /// <summary>
    /// Grants a temporary Speed buff (+Haste, +Concentration, +Agility).
    /// While active, the player pulses gold.
    /// </summary>
    public void ApplySpeedBuff(int hasteBonus, int concBonus, int agiBonus, float duration)
    {
        var stats = _archerySystem?.PlayerStats;
        if (stats != null)
        {
            // Remove old buff if re-applying
            if (_speedBuffHaste > 0) stats.Haste -= _speedBuffHaste;
            if (_speedBuffConc > 0) stats.Concentration -= _speedBuffConc;
            if (_speedBuffAgi > 0) stats.Agility -= _speedBuffAgi;

            _speedBuffHaste = hasteBonus;
            _speedBuffConc = concBonus;
            _speedBuffAgi = agiBonus;
            stats.Haste += hasteBonus;
            stats.Concentration += concBonus;
            stats.Agility += agiBonus;
            _speedBuffTimer = duration;
            _speedPulseTimer = 0f;
            GD.Print($"[PlayerController] Speed buff active for {duration}s — Haste+{hasteBonus} Conc+{concBonus} Agi+{agiBonus}");
        }
    }

    private void SpawnHealNumber(float amount)
    {
        if (!GameSettings.ShowDamageNumbers) return;

        var scene = GD.Load<PackedScene>("res://Scenes/VFX/DamageNumber.tscn");
        if (scene != null)
        {
            var dmgNum = scene.Instantiate<Node3D>();
            GetTree().CurrentScene.AddChild(dmgNum);
            dmgNum.GlobalPosition = GlobalPosition + new Vector3(0, 2.0f, 0);

            if (dmgNum is DamageNumber dn)
            {
                dn.SetHeal(amount);
            }
        }
    }

    public void ApplyShield(int amount, float duration)
    {
        var stats = _archerySystem?.PlayerStats;
        if (stats != null)
        {
            stats.CurrentShield += amount;
            if (duration > _shieldTimer) _shieldTimer = duration;
            GD.Print($"[Player {PlayerIndex}] Shield +{amount} applied. Total: {stats.CurrentShield}, Duration: {_shieldTimer}s");
        }
    }

    public void RegisterDealtDamage(float damage)
    {
        if (LifestealPercent > 0)
        {
            float healAmount = damage * LifestealPercent;
            Heal(healAmount);
        }
    }

    private float _flashTimer = 0f;
    public void FlashRed(float duration = 0.2f)
    {
        _flashTimer = duration;
        ApplyFlashVisuals(Colors.Red);
    }

    public override void _Process(double delta)
    {
        if (_flashTimer > 0)
        {
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0)
            {
                _flashTimer = 0;
                ApplyFlashVisuals(Colors.White, true); // Restore
            }
        }
    }

    private void ApplyFlashVisuals(Color color, bool restore = false)
    {
        // Recursively find and flash all meshes in the player visuals
        void FlashRecursive(Node node)
        {
            if (node is MeshInstance3D mesh)
            {
                if (restore)
                {
                    mesh.MaterialOverride = null;
                }
                else
                {
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = color;
                    mat.EmissionEnabled = true;
                    mat.Emission = color;
                    mat.EmissionEnergyMultiplier = 2.0f;
                    mesh.MaterialOverride = mat;
                }
            }
            foreach (Node child in node.GetChildren()) FlashRecursive(child);
        }

        // Search Erika or custom models
        var visuals = GetNodeOrNull("Visuals") ?? GetNodeOrNull("Erika") ?? (Node)this;
        FlashRecursive(visuals);
    }
}
