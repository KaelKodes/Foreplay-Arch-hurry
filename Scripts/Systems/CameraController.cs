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

    // Zoom settings
    private float _zoomDistance = 10f;      // Current zoom distance
    private float _targetZoomDistance = 10f; // Target for smooth interpolation
    private const float ZoomMin = 0f;       // First person
    private const float ZoomMax = 15f;      // Far third person
    private const float ZoomSpeed = 2f;     // Scroll sensitivity
    private const float ZoomSmoothSpeed = 8f;

    // Head bone tracking for first person
    private Skeleton3D _skeleton;
    private int _headBoneIdx = -1;
    private const string HeadBoneName = "mixamorig_Head";

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
        // Guard: Only the active camera should process input
        if (!Current) return;

        // Mouse scroll for zoom
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                _targetZoomDistance = Mathf.Clamp(_targetZoomDistance - ZoomSpeed, ZoomMin, ZoomMax);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                _targetZoomDistance = Mathf.Clamp(_targetZoomDistance + ZoomSpeed, ZoomMin, ZoomMax);
            }
        }

        // Allow Orbit Rotation (Right Click)
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
        // Smooth zoom interpolation
        _zoomDistance = Mathf.Lerp(_zoomDistance, _targetZoomDistance, (float)delta * ZoomSmoothSpeed);

        if (_target == null)
        {
            return;
        }

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
            // Walking Camera (Independent Orbit) with Zoom
            // Interpolate height and shoulder offset based on zoom distance
            float zoomT = 1.0f - (_zoomDistance / ZoomMax); // 0 = far, 1 = close/first person

            // Height: From high (far) to eye level (close)
            float height = Mathf.Lerp(FollowOffset.Y, 1.7f, zoomT);

            // Shoulder offset: None when far, shifts right when close for over-the-shoulder
            float shoulderOffset = Mathf.Lerp(0f, 0.5f, Mathf.Clamp(zoomT * 2f, 0f, 1f));

            // Calculate position
            Vector3 desiredOffset = new Vector3(shoulderOffset, height, _zoomDistance);
            Vector3 orbitPos = _target.GlobalPosition + (GlobalBasis * desiredOffset);

            // Lerp Position for smoothness
            GlobalPosition = GlobalPosition.Lerp(orbitPos, (float)delta * SmoothSpeed);

            // In first person, attach to head bone
            if (_zoomDistance < 0.5f)
            {
                Vector3 headPos = GetHeadPosition();
                // Offset forward (in camera's look direction) so we're in front of the face
                Vector3 forwardOffset = -GlobalBasis.Z * 0.3f; // 0.3m forward
                GlobalPosition = headPos + new Vector3(0, 0.1f, 0) + forwardOffset;
            }
        }
    }

    private Vector3 GetHeadPosition()
    {
        // Try to find skeleton if we haven't yet
        if (_skeleton == null && _target != null)
        {
            var erikaNode = _target.GetNodeOrNull<Node3D>("Erika");
            if (erikaNode != null)
            {
                _skeleton = erikaNode.GetNodeOrNull<Skeleton3D>("Skeleton3D");
                if (_skeleton != null)
                {
                    _headBoneIdx = _skeleton.FindBone(HeadBoneName);
                    GD.Print($"[CameraController] Found head bone '{HeadBoneName}' at index {_headBoneIdx}");
                }
            }
        }

        // Get head bone world position
        if (_skeleton != null && _headBoneIdx >= 0)
        {
            Transform3D boneGlobalPose = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIdx);
            return boneGlobalPose.Origin;
        }

        // Fallback to fixed height
        return _target.GlobalPosition + new Vector3(0, 1.7f, 0);
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
