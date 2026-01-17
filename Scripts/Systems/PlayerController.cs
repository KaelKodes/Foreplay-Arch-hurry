using Godot;
using System;

namespace Archery;

public enum PlayerState
{
    WalkMode,       // Default Exploration
    CombatMelee,    // Melee Combat
    CombatArcher,   // Archery
    BuildMode,      // Town Building / Placement
    DriveMode,      // In a vehicle
    SpectateMode,   // Free-cam
    PlacingObject   // Manipulating objects
}

public partial class PlayerController : CharacterBody3D
{
    [Export] public float RotationSpeed = 1.0f;
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float JumpForce = 5.0f;
    [Export] public float Gravity = 9.8f;
    [Export] public NodePath CameraPath { get; set; } // Now a property for external access
    [Export] public NodePath ArcherySystemPath;
    [Export] public NodePath MeleeSystemPath;
    [Export] public NodePath AnimationTreePath;

    // Physics State
    private Vector3 _velocity = Vector3.Zero;
    private bool _isGrounded = true;

    // Multiplayer Properties
    [Export] public int PlayerIndex { get; set; } = 0;
    private int _lastPlayerIndex = -1;

    // IsLocal is now derived from Authority.
    // If not in a multiplayer session, we default to true (Authority is 1, UniqueId is 1).
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

    private GolfCart _currentCart;
    private InteractableObject _selectedObject;
    public InteractableObject SelectedObject => _selectedObject;
    private InteractableObject _lastHoveredObject;
    private MainHUDController _hud;
    private MeleeSystem _meleeSystem;
    private AnimationTree _animTree;
    private AnimationPlayer _animPlayer; // Current
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
            if (_modelManager != null && _modelManager.CurrentModelId != value && !string.IsNullOrEmpty(value))
            {
                GD.Print($"[PlayerController] SyncModel changing to {value}");
                _modelManager.SetCharacterModel(value);
            }
        }
    }

    private bool _isJumping = false;

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
    private BowController _standaloneBow; // Universal bow for non-Erika characters
    private float _inputCooldown = 0.0f;
    private Archery.DrawStage _lastArcheryStage = Archery.DrawStage.Idle;

    // Character Model Selection
    private string _currentModelId = "erika";
    public string CurrentModelId => _modelManager?.CurrentModelId ?? _currentModelId;
    private CharacterModelManager _modelManager;
    private Mesh _cachedBowMesh; // Legacy - now managed by CharacterModelManager
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



    public override void _EnterTree()
    {
        // 1. Establish Authority based on Node Name (which we expect to be the Peer ID)
        if (long.TryParse(Name, out long id))
        {
            SetMultiplayerAuthority((int)id);
        }
    }

    public override void _Ready()
    {

        // SetupReplication(); // Moved to Scene

        GD.Print($"[PlayerController] _Ready starting for {Name}. Authority: {GetMultiplayerAuthority()}, IsLocal: {IsLocal}");

        _camera = GetNodeOrNull<CameraController>(CameraPath);
        GD.Print($"[PlayerController] Camera resolve: {(_camera != null ? "SUCCESS" : "FAILED")} (Path: {CameraPath})");

        // Find systems as children
        _archerySystem = GetNodeOrNull<ArcherySystem>("ArcherySystem");
        _meleeSystem = GetNodeOrNull<MeleeSystem>("MeleeSystem");

        if (_archerySystem == null && !ArcherySystemPath.IsEmpty)
            _archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);

        if (_meleeSystem == null && !MeleeSystemPath.IsEmpty)
            _meleeSystem = GetNodeOrNull<MeleeSystem>(MeleeSystemPath);

        GD.Print($"[PlayerController] Systems: Archery={(_archerySystem != null)}, Melee={(_meleeSystem != null)}");

        // Attempt to find the visual avatar
        _avatarMesh = GetNodeOrNull<MeshInstance3D>("AvatarMesh");
        _animTree = GetNodeOrNull<AnimationTree>(AnimationTreePath);
        if (_animTree == null) _animTree = GetNodeOrNull<AnimationTree>("AnimationTree");

        _meleeModel = GetNodeOrNull<Node3D>("Erika");
        _archeryModel = GetNodeOrNull<Node3D>("ErikaBow");

        _meleeAnimPlayer = GetNodeOrNull<AnimationPlayer>("Erika/AnimationPlayer");
        _archeryAnimPlayer = GetNodeOrNull<AnimationPlayer>("ErikaBow/AnimationPlayer");

        // Initialize CharacterModelManager
        _modelManager = new CharacterModelManager();
        AddChild(_modelManager);
        _modelManager.Initialize(this, _meleeModel, _archeryModel, _animTree, _meleeAnimPlayer, _archeryAnimPlayer);

        // Default to Melee
        _animPlayer = _meleeAnimPlayer;

        GD.Print($"[PlayerController] Anim Resolve: Player={(_animPlayer != null)}, Tree={(_animTree != null)}");

        if (_animPlayer != null)
        {
            if (_animTree != null && _animTree.TreeRoot != null)
            {
                _animTree.Active = true;
                GD.Print("[PlayerController] AnimationTree Activated.");
            }
            else
            {
                if (_animTree != null) _animTree.Active = false;
                var anims = _animPlayer.GetAnimationList();
                if (anims.Length > 0)
                {
                    _animPlayer.Play(anims[0]);
                    GD.Print($"[PlayerController] Autoplaying first animation: {anims[0]}");
                }
            }
        }

        // Color based on Index
        UpdatePlayerColor();



        _hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;
        GD.Print($"[PlayerController] HUD resolve: {(_hud != null ? "SUCCESS" : "FAILED")}");

        if (_meleeSystem != null) _meleeSystem.RegisterPlayer(this);
        if (_archerySystem != null) _archerySystem.RegisterPlayer(this);

        if (IsLocal)
        {
            GD.Print("[PlayerController] Initializing Local Player...");
            if (_hud != null) _hud.RegisterPlayer(this);

            // Camera Target
            if (_camera != null)
            {
                GD.Print("[PlayerController] Activating Camera... Current: " + _camera.Current);
                _camera.SetTarget(this, true); // Snap initially
                _camera.MakeCurrent();
                GD.Print("[PlayerController] Camera set to Current: " + _camera.Current);
            }
            else
            {
                GD.PrintErr("[PlayerController] CRITICAL: Camera is NULL on local player!");
            }

            // Subscribe to ToolManager for mode switching
            if (ToolManager.Instance != null)
            {
                ToolManager.Instance.ToolChanged += OnToolChanged;
            }
        }
        else
        {
            GD.Print($"[PlayerController] Initializing Remote Player: {Name}");
            // Remote Player: Destroy Camera to prevent view hijacking
            if (_camera != null)
            {
                GD.Print("[PlayerController] Removing Remote Camera.");
                _camera.QueueFree();
                _camera = null;
            }

            // Also destroy AimAssist for remote players so we don't see their lines
            var aimAssist = GetNodeOrNull<Node3D>("AimAssist");
            if (aimAssist != null)
            {
                aimAssist.QueueFree();
            }
        }

        // Find MeleeSystem for future melee support
        // _meleeSystem = GetTree().CurrentScene.FindChild("MeleeSystem", true, false) as MeleeSystem; // Removed, handled above

        // Visual Sword Setup
        SetupVisualSword();
    }

    private void OnToolChanged(int toolInt)
    {
        if (!IsLocal) return;

        // Sync to remote players via property
        SynchronizedTool = toolInt;
    }

    private void ApplyToolChange(ToolType newTool)
    {
        _currentTool = newTool;
        GD.Print($"[PlayerController] ApplyToolChange: {newTool}. ArcherySystem: {_archerySystem != null}, HUD: {_hud != null}, State: {CurrentState}");

        // Reset old modes
        _archerySystem?.ExitCombatMode();
        _meleeSystem?.ExitMeleeMode();

        // Entered new mode
        switch (_currentTool)
        {
            case ToolType.Bow:
                _archerySystem?.EnterCombatMode();
                SetModelMode(true);
                break;
            case ToolType.Sword:
                _meleeSystem?.EnterMeleeMode();
                SetModelMode(false);
                break;
            case ToolType.Hammer:
                _archerySystem?.EnterBuildMode();
                _hud?.SetBuildTool(MainHUDController.BuildTool.Selection);
                SetModelMode(false);
                break;
            case ToolType.Shovel:
                _archerySystem?.EnterBuildMode();
                _hud?.SetBuildTool(MainHUDController.BuildTool.Survey);
                SetModelMode(false);
                break;
            case ToolType.None:
                CurrentState = PlayerState.WalkMode;
                SetModelMode(false);
                break;
        }

        // Visual Sword Toggle
        if (_sword != null)
        {
            _sword.Visible = (_currentTool == ToolType.Sword);
        }
    }

    private void SetModelMode(bool archery)
    {
        // Delegate visual switching to CharacterModelManager
        _modelManager?.SetModelMode(archery);

        // Keep local reference to current animation player
        _animPlayer = archery ? _archeryAnimPlayer : _meleeAnimPlayer;
    }

    /// <summary>
    /// Cycles to the next available character model (delegates to CharacterModelManager).
    /// </summary>
    private void CycleCharacterModel()
    {
        _modelManager?.CycleCharacterModel();
        // Sync to other players
        if (_modelManager != null)
        {
            Rpc(nameof(NetSetCharacterModel), _modelManager.CurrentModelId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSetCharacterModel(string modelId)
    {
        _modelManager?.SetCharacterModel(modelId);
        GD.Print($"[PlayerController] Received model change from network: {modelId}");
    }

    private void SetupVisualSword()
    {
        var swordScene = GD.Load<PackedScene>("res://Scenes/Entities/Sword.tscn");
        if (swordScene == null) return;

        _sword = swordScene.Instantiate<SwordController>();
        _sword.Visible = false;

        // Find Erika's skeleton and attach sword to hand bone
        var erikaNode = GetNodeOrNull<Node3D>("Erika");
        if (erikaNode != null)
        {
            var skeleton = erikaNode.GetNodeOrNull<Skeleton3D>("Skeleton3D");
            if (skeleton != null)
            {
                // Create a BoneAttachment3D for the right hand
                var boneAttachment = new BoneAttachment3D();
                boneAttachment.Name = "RightHandAttachment";
                boneAttachment.BoneName = "mixamorig_RightHand";
                skeleton.AddChild(boneAttachment);

                // Attach sword to the bone attachment
                boneAttachment.AddChild(_sword);

                GD.Print("[PlayerController] Sword attached to Erika's right hand bone!");
            }
            else
            {
                // Fallback: attach directly to player
                AddChild(_sword);
                _sword.Position = new Vector3(-0.45f, 1.1f, 0.1f);
                GD.PrintErr("[PlayerController] Could not find Erika's skeleton, using fallback position");
            }
        }
        else
        {
            // Fallback: attach directly to player
            AddChild(_sword);
            _sword.Position = new Vector3(-0.45f, 1.1f, 0.1f);
            GD.PrintErr("[PlayerController] Could not find Erika node, using fallback position");
        }

        if (_meleeSystem != null)
        {
            _sword.ConnectToMeleeSystem(_meleeSystem);
        }
    }

    // Sync Property for looking up/down
    [Export] public float HeadXRotation { get; set; }

    public void SetPlayerIndex(int index)
    {
        GD.Print($"[PlayerController] SetPlayerIndex called: {index} (Old: {PlayerIndex}) for {Name}");
        PlayerIndex = index;
        UpdatePlayerColor();
    }

    private void UpdatePlayerColor()
    {
        Color c = TargetingHelper.GetPlayerColor(PlayerIndex);
        GD.Print($"[PlayerController] UpdatePlayerColor: Index {PlayerIndex} -> {c}");

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = c;
        if (_avatarMesh != null) _avatarMesh.MaterialOverride = mat;

        // Sync Sword Color
        if (_sword != null)
        {
            _sword.SetColor(c);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // 1. Common Updates (Runs for EVERYONE)
        if (_lastPlayerIndex != PlayerIndex)
        {
            _lastPlayerIndex = PlayerIndex;
            UpdatePlayerColor();
        }

        UpdateAnimations(delta);

        // 2. Authority Check
        if (!IsLocal)
        {
            return;
        }

        // Update Sync Properties (State -> Property)
        if (_camera != null)
        {
            HeadXRotation = _camera.Rotation.X;
        }

        if (_inputCooldown > 0) _inputCooldown -= (float)delta;

        // 3. Movement & Targeting (Authority Only)
        if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher)
        {
            HandleBodyMovement(delta);
            HandleTargetingHotkeys();
        }

        // 4. State Check (Authority Only)
        switch (CurrentState)
        {
            case PlayerState.CombatMelee:
            case PlayerState.CombatArcher:
                HandleCombatInput(delta);
                break;
            case PlayerState.WalkMode:
                HandleProximityPrompts(delta);
                break;
            case PlayerState.DriveMode:
                HandleDrivingInput(delta);
                break;
            case PlayerState.BuildMode:
                HandleBuildModeInput(delta);
                break;
        }
    }

    private void HandleCombatInput(double delta)
    {
        if (_camera == null || _archerySystem == null) return;

        // Draw Stage Check
        if (_archerySystem.CurrentStage == DrawStage.Executing) return;

        // Continuously sync player position/rotation to camera's horizontal heading.
        // In Archery, we stand towards the target
        if (_archerySystem.CurrentTarget != null)
        {
            // Face the locked target
            Vector3 targetPos = _archerySystem.CurrentTarget.GlobalPosition;
            Vector3 dirToTarget = (targetPos - GlobalPosition).Normalized();
            dirToTarget.Y = 0;
            float targetAngle = Mathf.Atan2(-dirToTarget.X, -dirToTarget.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, 10.0f * (float)delta), 0);
        }
        else
        {
            // Continuously sync player position/rotation to camera's horizontal heading.
            Vector3 camForward = -_camera.GlobalTransform.Basis.Z;
            camForward.Y = 0;

            if (camForward.LengthSquared() < 0.01f) return;
            camForward = camForward.Normalized();

            float targetAngle = Mathf.Atan2(-camForward.X, -camForward.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, 10.0f * (float)delta), 0);
        }

        HandleProximityPrompts(delta);
    }

    private void HandleTargetingHotkeys()
    {
        if (_archerySystem == null) return;

        // Toggle Combat - LEGACY: Now handled by ToolManager
        // if (Input.IsKeyPressed(Key.R) && _inputCooldown <= 0)
        // {
        //     _inputCooldown = 0.5f;
        //     if (CurrentState == PlayerState.CombatMode) _archerySystem.ExitCombatMode();
        //     else if (CurrentState == PlayerState.WalkMode) _archerySystem.EnterCombatMode();
        // }

        // Mode Cycle
        if (Input.IsKeyPressed(Key.Q) && _inputCooldown <= 0 && (CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher))
        {
            _inputCooldown = 0.2f;
            _archerySystem.CycleShotMode();
        }

        // Cycle Target
        if (Input.IsActionJustPressed("ui_focus_next") || Input.IsKeyPressed(Key.Tab))
        {
            if (_inputCooldown <= 0)
            {
                _inputCooldown = 0.2f;
                _archerySystem.CycleTarget();
            }
        }

        // Clear Target
        if (Input.IsKeyPressed(Key.Quoteleft) || Input.IsKeyPressed(Key.Escape))
        {
            if (_inputCooldown <= 0)
            {
                _inputCooldown = 0.2f;
                _archerySystem.ClearTarget();
            }
        }
    }

    private void HandleBodyMovement(double delta)
    {
        if (_camera == null) return;

        // Gravity
        if (!IsOnFloor())
        {
            _velocity.Y -= Gravity * (float)delta;
        }
        else
        {
            _velocity.Y = 0;
        }

        // Jump
        bool isShooting = _archerySystem != null && (_archerySystem.CurrentStage == DrawStage.Drawing || _archerySystem.CurrentStage == DrawStage.Aiming);

        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
        {
            bool canJump = !isShooting || (_archerySystem != null && _archerySystem.CanJumpWhileShooting);
            if (canJump)
            {
                _velocity.Y = JumpForce;
                _isJumping = true;
            }
        }

        // Reset jump flag when landed
        if (IsOnFloor() && _velocity.Y <= 0)
        {
            _isJumping = false;
        }

        // Movement
        Vector3 inputDir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) inputDir.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) inputDir.Z += 1;
        if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
        if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;

        if (inputDir.LengthSquared() > 0.1f)
        {
            inputDir = inputDir.Normalized();

            // Rotate input to match Camera Y rotation
            Vector3 camRot = _camera.GlobalRotation;
            Vector3 moveDir = inputDir.Rotated(Vector3.Up, camRot.Y);

            // Sprint Logic
            float speedMult = Input.IsKeyPressed(Key.Shift) ? 2.0f : 1.0f;

            // Shooting Throttling
            if (isShooting)
            {
                speedMult *= _archerySystem.ShootingMoveMultiplier;
            }

            _velocity.X = moveDir.X * MoveSpeed * speedMult;
            _velocity.Z = moveDir.Z * MoveSpeed * speedMult;

            // Only auto-rotate body if NOT in combat mode (where we face target/camera)
            if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.BuildMode)
            {
                float targetAngle = Mathf.Atan2(moveDir.X, moveDir.Z);
                float currentAngle = Rotation.Y;
                Rotation = new Vector3(0, Mathf.LerpAngle(currentAngle, targetAngle, 10.0f * (float)delta), 0);
            }
        }
        else
        {
            // Decelerate X/Z
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, MoveSpeed * 5.0f * (float)delta);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, MoveSpeed * 5.0f * (float)delta);
        }

        // Apply Velocity
        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
        _isGrounded = IsOnFloor();

        HandleVehicleDetection();
    }

    private void UpdateAnimations(double delta)
    {
        // Use Velocity property (synced) instead of _velocity field (local only) for correct remote player animations
        PlayerAnimations.UpdateAnimations(_animTree, this, _meleeSystem, _archerySystem, MoveSpeed, _isJumping, Velocity, ref _lastArcheryStage);
    }

    private void HandleProximityPrompts(double delta)
    {
        if (_archerySystem == null) return;

        // 1. Proximity Check for Arrows (Easier for player)
        ArrowController nearestArrow = FindNearestCollectibleArrow(3.0f);
        if (nearestArrow != null && nearestArrow.IsCollectible)
        {
            string prompt = nearestArrow.GetInteractionPrompt();
            if (!string.IsNullOrEmpty(prompt))
            {
                _archerySystem.SetPrompt(true, prompt);
                if (Input.IsKeyPressed(Key.E))
                {
                    nearestArrow.OnInteract(this);
                }
                return;
            }
        }

        // 2. Generic Interaction Check (Raycast based for precise objects)
        Node hitNode = CheckInteractionForwardRaycast();
        if (hitNode != null)
        {
            float dist = GlobalPosition.DistanceTo((hitNode is Node3D n3d ? n3d.GlobalPosition : GlobalPosition));

            if (dist < 5.0f)
            {
                string prompt = (hitNode is InteractableObject io) ? io.GetInteractionPrompt() : ((hitNode is ArrowController ac) ? ac.GetInteractionPrompt() : "");
                if (!string.IsNullOrEmpty(prompt))
                {
                    _archerySystem.SetPrompt(true, prompt);
                    if (Input.IsKeyPressed(Key.E))
                    {
                        if (hitNode is InteractableObject io2) io2.OnInteract(this); else if (hitNode is ArrowController ac2) ac2.OnInteract(this);
                    }
                    return; // Priority over clearing
                }
            }
        }

        _archerySystem.SetPrompt(false);
    }

    private ArrowController FindNearestCollectibleArrow(float maxDistance)
    {
        var arrows = GetTree().GetNodesInGroup("arrows");
        ArrowController nearest = null;
        float minDist = maxDistance;

        foreach (Node node in arrows)
        {
            if (node is ArrowController arrow && arrow.IsCollectible)
            {
                float d = GlobalPosition.DistanceTo(arrow.GlobalPosition);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = arrow;
                }
            }
        }
        return nearest;
    }

    private Node CheckInteractionForwardRaycast()
    {
        // Cast from Player Body forward, not Camera
        var spaceState = GetWorld3D().DirectSpaceState;

        // Origin: Approx Head Height
        var from = GlobalPosition + new Vector3(0, 1.5f, 0);
        // Direction: Player Forward (+Z because Basis.Z seems to be Forward for this mesh?)
        var to = from + (-GlobalTransform.Basis.Z) * 3.0f; // 3m reach

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 3; // Layers 1 and 2
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitObj = (Node)result["collider"];
            // GD.Print($"Hit: {((Node)hitObj).Name}");

            if (hitObj is Node colliderNode)
            {
                Node n = colliderNode;
                while (n != null)
                {
                    if (n is InteractableObject io) return io;
                    if (n is ArrowController ac) return ac;
                    n = n.GetParent();
                }
            }
        }
        return null;
    }

    private void HandleVehicleDetection()
    {
        GolfCart nearestCart = PlayerInteraction.FindNearestCart(GetTree(), GlobalPosition, 3.0f);

        if (nearestCart != null)
        {
            if (_archerySystem != null) _archerySystem.SetPrompt(true, "PRESS E TO DRIVE");
            if (Input.IsKeyPressed(Key.E))
            {
                EnterVehicle(nearestCart);
            }
        }
    }

    private void HandleDrivingInput(double delta)
    {
        if (_currentCart == null)
        {
            CurrentState = PlayerState.WalkMode;
            return;
        }

        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.Y * 0.5f;
        Rotation = _currentCart.Rotation;

        if (Input.IsKeyPressed(Key.E))
        {
            ExitVehicle();
        }
    }

    private void HandleBuildModeInput(double delta)
    {
        // Standard movement allowed in build mode
        HandleBodyMovement(delta);

        // Tool-specific behavior
        if (_hud != null && _hud.CurrentTool != MainHUDController.BuildTool.Selection)
        {
            // If not in selection tool, don't allow selecting/editing objects
            if (_selectedObject != null) _selectedObject.SetSelected(false);
            _selectedObject = null;
            return;
        }

        // Selection feedback (Highlight on hover)
        InteractableObject hoverObj = CheckInteractableRaycast();

        if (hoverObj != _lastHoveredObject)
        {
            if (_lastHoveredObject != null && _lastHoveredObject != _selectedObject)
            {
                _lastHoveredObject.OnHover(false);
            }
            _lastHoveredObject = hoverObj;
        }

        if (hoverObj != null && hoverObj != _selectedObject)
        {
            hoverObj.OnHover(true);
        }

        // Handle Selected Object Actions
        if (_selectedObject != null)
        {
            if (Input.IsKeyPressed(Key.X) && _selectedObject.IsDeletable)
            {
                if (NetworkManager.Instance != null && Multiplayer.MultiplayerPeer != null)
                {
                    NetworkManager.Instance.RpcId(1, nameof(NetworkManager.RequestDeleteObject), _selectedObject.Name);
                }
                else
                {
                    _selectedObject.QueueFree();
                }
                _selectedObject = null;
                _archerySystem.SetPrompt(false);
            }
            else if (Input.IsKeyPressed(Key.C) && _selectedObject.IsMovable)
            {
                if (_archerySystem.ObjectPlacer != null)
                {
                    var objToMove = _selectedObject;
                    _selectedObject.SetSelected(false);
                    _selectedObject = null;
                    _archerySystem.ObjectPlacer.StartPlacing(objToMove);
                }
            }

            if (_selectedObject != null)
            {
                _archerySystem.SetPrompt(true, $"SELECTED: {_selectedObject.ObjectName} | X: DELETE | C: REPOSITION");
            }
        }
        else if (hoverObj != null)
        {
            _archerySystem.SetPrompt(true, $"CLICK TO SELECT {hoverObj.ObjectName}");
        }
        else
        {
            // Only clear if we aren't displaying something else from BuildManager
            // (Note: BuildManager might be setting prompts too, so we need to be careful)
        }
    }

    private void EnterVehicle(GolfCart cart)
    {
        _currentCart = cart;
        _currentCart.Enter(this);
        CurrentState = PlayerState.DriveMode;
        Visible = false;

        if (_camera != null)
        {
            _camera.SetTarget(_currentCart, true);
        }
        if (_archerySystem != null) _archerySystem.SetPrompt(false);
    }

    private void ExitVehicle()
    {
        if (_currentCart == null) return;

        _currentCart.Exit();
        CurrentState = PlayerState.WalkMode;
        Visible = true;

        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.X * 2.0f;

        if (_camera != null)
        {
            _camera.SetTarget(this, true);
        }
        _currentCart = null;
    }

    public void TeleportTo(Vector3 position, Vector3 lookAtTarget)
    {
        GlobalPosition = position;
        LookAt(new Vector3(lookAtTarget.X, GlobalPosition.Y, lookAtTarget.Z), Vector3.Up);
        RotationDegrees = new Vector3(0, RotationDegrees.Y, 0);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void NetTeleport(Vector3 position, Vector3 rotationDegrees)
    {
        GD.Print($"[PlayerController] NetTeleport received: {position}");
        GlobalPosition = position;
        RotationDegrees = rotationDegrees;

        // Reset physics
        _velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void NetSetPlayerIndex(int index)
    {
        GD.Print($"[PlayerController] NetSetPlayerIndex received: {index} (Old: {PlayerIndex})");
        SetPlayerIndex(index);
    }

    private InteractableObject CheckInteractableRaycast()
    {
        if (_camera == null) return null;

        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var to = from + _camera.ProjectRayNormal(mousePos) * 100.0f;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = (Node)result["collider"];
            var interactable = collider.GetNodeOrNull<InteractableObject>(".") ?? collider.GetParentOrNull<InteractableObject>();
            if (interactable == null && collider.GetParent() != null)
            {
                interactable = collider.GetParent().GetParentOrNull<InteractableObject>();
            }
            return interactable;
        }
        return null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsLocal) return;

        // LEGACY: Build mode toggle via V - Now handled by ToolManager
        // if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.V)
        // {
        //     if (CurrentState == PlayerState.WalkMode)
        //     {
        //         _archerySystem.EnterBuildMode();
        //     }
        //     else if (CurrentState == PlayerState.BuildMode)
        //     {
        //         if (_selectedObject != null) _selectedObject.SetSelected(false);
        //         _selectedObject = null;
        //         _archerySystem.ExitBuildMode();
        //     }
        // }

        if (@event is InputEventKey homeKey && homeKey.Pressed && !homeKey.Echo && homeKey.Keycode == Key.Home)
        {
            if (_archerySystem != null)
            {
                Vector3 teePos = _archerySystem.TeePosition;
                // Teleport slightly behind and above the tee
                Vector3 offset = new Vector3(0, 0, -1.0f); // Face down-range (+Z)
                TeleportTo(teePos + offset, teePos + Vector3.Forward * 10.0f);
                GD.Print("PlayerController: Home teleport to Tee.");
            }
        }

        // M key: Cycle character model
        if (@event is InputEventKey modelKey && modelKey.Pressed && !modelKey.Echo && modelKey.Keycode == Key.M)
        {
            CycleCharacterModel();
        }
        // Selection logic in Build Mode
        if (CurrentState == PlayerState.BuildMode)
        {
            // Mouse Wheel actions
            if (_selectedObject != null && @event is InputEventMouseButton mbScroll && mbScroll.Pressed)
            {
                bool isShift = mbScroll.ShiftPressed;
                bool isCtrl = mbScroll.CtrlPressed;

                if (mbScroll.ButtonIndex == MouseButton.WheelUp)
                {
                    if (isCtrl)
                        _selectedObject.Scale *= 1.1f;
                    else if (isShift)
                        _selectedObject.GlobalPosition += new Vector3(0, 0.25f, 0);
                    else
                        _selectedObject.RotateY(Mathf.DegToRad(15.0f));

                    GetViewport().SetInputAsHandled();
                }
                else if (mbScroll.ButtonIndex == MouseButton.WheelDown)
                {
                    if (isCtrl)
                        _selectedObject.Scale *= 0.9f;
                    else if (isShift)
                        _selectedObject.GlobalPosition -= new Vector3(0, 0.25f, 0);
                    else
                        _selectedObject.RotateY(Mathf.DegToRad(-15.0f));

                    GetViewport().SetInputAsHandled();
                }
            }

            // Mouse Drag Rotation
            if (_selectedObject != null && @event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                // Rotate around Y axis based on horizontal mouse movement
                float rotSpeed = 0.5f;
                _selectedObject.RotateY(Mathf.DegToRad(mm.Relative.X * rotSpeed));
            }

            // Select on Click
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                if (_hud == null || _hud.CurrentTool == MainHUDController.BuildTool.Selection)
                {
                    InteractableObject clickedObj = CheckInteractableRaycast();
                    if (clickedObj != _selectedObject)
                    {
                        if (_selectedObject != null) _selectedObject.SetSelected(false);
                        _selectedObject = clickedObj;
                        if (_selectedObject != null) _selectedObject.SetSelected(true);
                    }
                }
            }
        }
    }
}
