using Godot;
using System;

namespace Archery;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public Vector3 FollowOffset = new Vector3(0, 5, 10);
    [Export] public float SmoothSpeed = 5.0f;

    private Node3D _target;
    private bool _isFollowingBall = false;

    // "Free Look" in this context acts as a "Detach/Debug" toggle.
    // If true, we stop following the target.
    private bool _canFreeLook = false;
    private Node3D _lockedTarget;
    public Node3D LockedTarget => _lockedTarget;
    private float _lookSensitivity = 0.3f;

    public override void _Ready()
    {
        SetAsTopLevel(true); // Detach from parent transform to prevent spin
        if (TargetPath != null && !TargetPath.IsEmpty)
        {
            _target = GetNodeOrNull<Node3D>(TargetPath); // Use GetNodeOrNull for safety
            GD.Print($"CameraController: Ready. Initial Target Path: {TargetPath}, Resolved Target: {(_target != null ? _target.Name : "null")}");
            if (_target != null) SetTarget(_target, true);
        }
        else
        {
            GD.Print("CameraController: Ready. No TargetPath assigned.");
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Allow Orbit Rotation (Right Click) regardless of "Free Look" mode
        // unless we strictly want to block it.
        // For "Walking Mode", we want Orbit.

        // Guard: Only the active camera should process mouse input
        if (!Current) return;

        if (@event is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            // Rotate camera based on mouse motion
            RotationDegrees -= new Vector3(motion.Relative.Y, motion.Relative.X, 0) * _lookSensitivity;

            // Clamp pitch to prevent flipping
            Vector3 rot = RotationDegrees;
            rot.X = Mathf.Clamp(rot.X, -80, 80);
            RotationDegrees = rot;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null)
        {
            // Debug print once periodically?
            return;
        }

        // if (Engine.GetFramesDrawn() % 60 == 0) GD.Print($"Cam Target: {_target.Name} @ {_target.GlobalPosition}, CamPos: {GlobalPosition}");

        // If Debug/Free Look is enabled, skip automatic following logic
        if (_canFreeLook) return;

        if (_isFollowingBall)
        {
            // Ball Camera (High Angle Chase) - Logic remains "Chase" style for Ball
            Vector3 targetPos = _target.GlobalPosition + new Vector3(0, 10, -10);
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)delta * SmoothSpeed);
            LookAt(_target.GlobalPosition, Vector3.Up);
        }
        else if (_lockedTarget != null)
        {
            // Z-Targeting Mode: Frame Player and Locked Target
            // Ideal position: Behind the player, slightly higher, looking towards target
            Vector3 playerPos = _target.GlobalPosition + new Vector3(0, 1.3f, 0); // Chest height
            Vector3 targetPos = _lockedTarget.GlobalPosition;

            Vector3 dirToTarget = (targetPos - playerPos).Normalized();
            Vector3 camRight = dirToTarget.Cross(Vector3.Up).Normalized();

            // Position camera behind player relative to target direction
            // and slightly to the side for a "shoulder" view
            float dist = FollowOffset.Z;
            float height = FollowOffset.Y;
            Vector3 desiredPos = playerPos - dirToTarget * dist + Vector3.Up * height + camRight * 1.5f;

            GlobalPosition = GlobalPosition.Lerp(desiredPos, (float)delta * SmoothSpeed);

            // Look at a point between player and target, or just at target
            // Framing: Target should be roughly central, player on the left/right
            LookAt(targetPos, Vector3.Up);
        }
        else
        {
            // Walking Camera (Independent Orbit)
            // Follow Target Position, but respect Camera's OWN Rotation.

            float dist = FollowOffset.Z;
            float height = FollowOffset.Y;

            // Calculate position offset from Camera's current Basis
            // This decouples us from the Player's rotation
            Vector3 desiredOffset = new Vector3(0, height, dist);
            Vector3 orbitPos = _target.GlobalPosition + (GlobalBasis * desiredOffset);

            // Lerp Position for smoothness
            GlobalPosition = GlobalPosition.Lerp(orbitPos, (float)delta * SmoothSpeed);
        }
    }

    public void SetTarget(Node3D newTarget, bool snap = false)
    {
        GD.Print($"CameraController: SetTarget called with {newTarget?.Name}, snap={snap}");
        _target = newTarget;
        if (snap && _target != null)
        {
            // Instantly snap to valid orbit position
            float dist = FollowOffset.Z;
            float height = FollowOffset.Y;
            Vector3 desiredOffset = new Vector3(0, height, dist);
            // Use current rotation basis
            GlobalPosition = _target.GlobalPosition + (GlobalBasis * desiredOffset);
        }
    }

    public void SetLockedTarget(Node3D target)
    {
        _lockedTarget = target;
        GD.Print($"CameraController: Locked onto {target?.Name}");
    }

    public void SnapBehind(Node3D target)
    {
        if (target == null) return;
        _target = target;

        // Match target horizontal rotation but keep specific pitch
        Vector3 targetRot = target.GlobalRotation;
        GlobalRotation = new Vector3(Mathf.DegToRad(-15), targetRot.Y, 0);

        float dist = FollowOffset.Z;
        float height = FollowOffset.Y;
        Vector3 desiredOffset = new Vector3(0, height, dist);

        // Position behind the target based on its orientation
        GlobalPosition = target.GlobalPosition + (target.GlobalBasis * desiredOffset);
    }

    public void SetFollowing(bool following)
    {
        _isFollowingBall = following;
        if (_isFollowingBall) _canFreeLook = false; // Ensure we move!
    }

    public void SetFreeLook(bool enabled)
    {
        _canFreeLook = enabled;
    }
}
