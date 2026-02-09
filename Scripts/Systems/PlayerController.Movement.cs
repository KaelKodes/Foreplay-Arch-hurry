using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void HandleBodyMovement(double delta)
    {
        if (_camera == null) return;

        // Apply Vault movement
        if (_isVaulting)
        {
            _dashTime -= (float)delta;
            _velocity.X = _dashDir.X * DashSpeed;
            _velocity.Z = _dashDir.Z * DashSpeed;
            _velocity.Y -= Gravity * (float)delta; // Natural falling arc

            Velocity = _velocity;
            MoveAndSlide();
            _velocity = Velocity;

            // End vault when time runs out OR if we hit ground after the initial leap
            if (_dashTime <= 0 || (IsOnFloor() && _dashTime < DashDuration * 0.7f))
            {
                _isVaulting = false;
                CollisionMask = _originalMask;
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

        if (inputDir.LengthSquared() > 0.1f)
        {
            inputDir = inputDir.Normalized();

            // Rotate input to match Camera Y rotation
            Vector3 camRot = _camera.GlobalRotation;
            Vector3 moveDir = inputDir.Rotated(Vector3.Up, camRot.Y);

            float currentSpeed = MoveSpeed;
            if (IsSprinting) currentSpeed *= 1.5f;

            _velocity.X = moveDir.X * currentSpeed;
            _velocity.Z = moveDir.Z * currentSpeed;

            // Face movement direction smoothly
            float targetAngle = Mathf.Atan2(-moveDir.X, -moveDir.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, RotationSpeed * 10.0f * (float)delta), 0);
        }
        else
        {
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, MoveSpeed);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, MoveSpeed);
        }

        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;
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
