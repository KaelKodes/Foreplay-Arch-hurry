using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void HandleBodyMovement(double delta)
    {
        if (_camera == null) return;

        // Apply Vault movement
        if (_isVaulting || _isIntercepting)
        {
            float currentDashTime = _isVaulting ? _dashTime : _interceptTime;
            Vector3 currentDashDir = _isVaulting ? _dashDir : _interceptDir;

            if (_isVaulting) _dashTime -= (float)delta;
            else _interceptTime -= (float)delta;

            _velocity.X = currentDashDir.X * DashSpeed;
            _velocity.Z = currentDashDir.Z * DashSpeed;

            if (_isVaulting) _velocity.Y -= Gravity * (float)delta; // Natural falling arc for vault
            else _velocity.Y = 0; // Flat dash for intercept

            Velocity = _velocity;
            MoveAndSlide();
            _velocity = Velocity;

            // End dash when time runs out
            if (_isVaulting && (_dashTime <= 0 || (IsOnFloor() && _dashTime < DashDuration * 0.7f)))
            {
                _isVaulting = false;
                CollisionMask = _originalMask;
                _isJumping = false;
            }
            else if (_isIntercepting && _interceptTime <= 0)
            {
                _isIntercepting = false;
                _isJumping = false;
            }
            return;
        }

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

        // Use stat-derived speed if available, otherwise export default
        var stats = _archerySystem?.PlayerStats;
        float baseSpeed = stats?.DerivedMoveSpeed ?? MoveSpeed;

        if (inputDir.LengthSquared() > 0.1f)
        {
            inputDir = inputDir.Normalized();

            // Rotate input to match Camera Y rotation
            Vector3 camRot = _camera.GlobalRotation;
            Vector3 moveDir = inputDir.Rotated(Vector3.Up, camRot.Y);

            float currentSpeed = baseSpeed * _moveSpeedMultiplier;
            if (IsSprinting) currentSpeed *= 1.5f;

            _velocity.X = moveDir.X * currentSpeed;
            _velocity.Z = moveDir.Z * currentSpeed;

            // Face movement direction smoothly
            float targetAngle = Mathf.Atan2(-moveDir.X, -moveDir.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, RotationSpeed * 10.0f * (float)delta), 0);
        }
        else
        {
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, baseSpeed);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, baseSpeed);
        }

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
    }

    /// <summary>
    /// Movement for bot-controlled players. Uses BotInputProvider.MoveDirection
    /// instead of keyboard input. Includes raycast obstacle avoidance.
    /// </summary>
    private void HandleBotMovement(double delta)
    {
        // Vault/intercept still works the same for bots
        if (_isVaulting || _isIntercepting)
        {
            float currentDashTime = _isVaulting ? _dashTime : _interceptTime;
            Vector3 currentDashDir = _isVaulting ? _dashDir : _interceptDir;

            if (_isVaulting) _dashTime -= (float)delta;
            else _interceptTime -= (float)delta;

            _velocity.X = currentDashDir.X * DashSpeed;
            _velocity.Z = currentDashDir.Z * DashSpeed;

            if (_isVaulting) _velocity.Y -= Gravity * (float)delta;
            else _velocity.Y = 0;

            Velocity = _velocity;
            MoveAndSlide();
            _velocity = Velocity;

            if (_isVaulting && (_dashTime <= 0 || (IsOnFloor() && _dashTime < DashDuration * 0.7f)))
            {
                _isVaulting = false;
                CollisionMask = _originalMask;
                _isJumping = false;
            }
            else if (_isIntercepting && _interceptTime <= 0)
            {
                _isIntercepting = false;
                _isJumping = false;
            }
            return;
        }

        // Gravity
        if (!IsOnFloor())
            _velocity.Y -= Gravity * (float)delta;
        else
            _velocity.Y = 0;

        // Jump (from BotInput)
        if (IsOnFloor() && BotInput != null && BotInput.WantJump)
        {
            _velocity.Y = JumpForce;
            _isJumping = true;
        }

        if (IsOnFloor() && _velocity.Y <= 0)
            _isJumping = false;

        // Movement from BotInputProvider (already world-space, no camera rotation needed)
        Vector3 inputDir = BotInput?.MoveDirection ?? Vector3.Zero;

        var stats = _archerySystem?.PlayerStats;
        float baseSpeed = stats?.DerivedMoveSpeed ?? MoveSpeed;

        if (inputDir.LengthSquared() > 0.1f)
        {
            inputDir = inputDir.Normalized();

            // ── Obstacle Avoidance via Raycasts ──────────────────
            inputDir = ApplyBotObstacleAvoidance(inputDir);

            // ── Stuck Detection ──────────────────────────────────
            inputDir = ApplyBotStuckRecovery(inputDir, (float)delta);

            float currentSpeed = baseSpeed * _moveSpeedMultiplier;
            if (BotInput?.WantSprint == true) currentSpeed *= 1.5f;

            _velocity.X = inputDir.X * currentSpeed;
            _velocity.Z = inputDir.Z * currentSpeed;

            // Face movement (or look) direction smoothly
            Vector3 lookDir = BotInput?.LookDirection ?? inputDir;
            float targetAngle = Mathf.Atan2(-lookDir.X, -lookDir.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, RotationSpeed * 10.0f * (float)delta), 0);
        }
        else
        {
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, baseSpeed);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, baseSpeed);
            _botStuckTimer = 0f; // Reset stuck timer when not moving
        }

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
    }

    // ── Bot Obstacle Avoidance ────────────────────────────────
    private const float BotRaycastDistance = 3.5f;
    private const float BotRaycastAngle = 35f; // Degrees offset for side rays
    private float _botStuckTimer = 0f;
    private Vector3 _botLastPosition = Vector3.Zero;

    /// <summary>
    /// Cast 3 rays (left, center, right) in the desired movement direction.
    /// If center is blocked, steer toward the clearer side.
    /// </summary>
    private Vector3 ApplyBotObstacleAvoidance(Vector3 desiredDir)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 origin = GlobalPosition + Vector3.Up * 0.5f; // Raycast from waist height

        // Center ray
        bool centerHit = BotRaycast(spaceState, origin, desiredDir, BotRaycastDistance);

        if (!centerHit) return desiredDir; // Path is clear

        // Side rays — check which side is clearer
        float angleRad = Mathf.DegToRad(BotRaycastAngle);
        Vector3 leftDir = desiredDir.Rotated(Vector3.Up, angleRad).Normalized();
        Vector3 rightDir = desiredDir.Rotated(Vector3.Up, -angleRad).Normalized();

        bool leftHit = BotRaycast(spaceState, origin, leftDir, BotRaycastDistance);
        bool rightHit = BotRaycast(spaceState, origin, rightDir, BotRaycastDistance);

        if (!leftHit && !rightHit)
        {
            // Both sides clear — pick one randomly (prevents oscillation)
            return GD.Randf() > 0.5f ? leftDir : rightDir;
        }
        else if (!leftHit)
        {
            return leftDir;
        }
        else if (!rightHit)
        {
            return rightDir;
        }
        else
        {
            // All 3 blocked — try perpendicular (hard dodge)
            Vector3 perpendicular = new Vector3(-desiredDir.Z, 0, desiredDir.X).Normalized();
            return GD.Randf() > 0.5f ? perpendicular : -perpendicular;
        }
    }

    private bool BotRaycast(PhysicsDirectSpaceState3D spaceState, Vector3 origin, Vector3 direction, float distance)
    {
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * distance);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() }; // Don't hit self

        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }

    /// <summary>
    /// If the bot has barely moved for over 0.5s while wanting to move,
    /// apply a strong perpendicular dodge to escape.
    /// </summary>
    private Vector3 ApplyBotStuckRecovery(Vector3 desiredDir, float dt)
    {
        float movedDist = GlobalPosition.DistanceTo(_botLastPosition);

        if (movedDist < 0.05f) // Barely moved this frame
        {
            _botStuckTimer += dt;
        }
        else
        {
            _botStuckTimer = Mathf.Max(0, _botStuckTimer - dt * 2f); // Decay if moving
        }

        _botLastPosition = GlobalPosition;

        if (_botStuckTimer > 0.5f)
        {
            // Hard perpendicular dodge
            Vector3 perpendicular = new Vector3(-desiredDir.Z, 0, desiredDir.X).Normalized();
            _botStuckTimer = 0f; // Reset so we don't keep dodging
            return perpendicular;
        }

        return desiredDir;
    }

    public void TeleportTo(Vector3 position, Vector3 rotation)
    {
        GlobalPosition = position;
        RotationDegrees = rotation;
        _velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NetTeleport(Vector3 position, Vector3 rotation)
    {
        GD.Print($"[PlayerController] NetTeleport received for {Name}: {position}");
        GlobalPosition = position;
        RotationDegrees = rotation;
        _velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
    }
}
