using Godot;
using System;

namespace Archery;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public Vector3 FollowOffset = new Vector3(0, 5, 10);
    [Export] public float SmoothSpeed = 5.0f;
    [Export] public float RotationSmoothSpeed = 20.0f; // Controls "weight" of the camera. Lower = smoother/heavier.

    private Node3D _target;
    private bool _isFollowingBall = false;

    // "Free Look" in this context acts as a "Detach/Debug" toggle.
    // If true, we stop following the target.
    private bool _canFreeLook = false;
    private Node3D _lockedTarget;
    public Node3D LockedTarget => _lockedTarget;
    private float _lookSensitivity = 0.3f;

    // Smoothed Rotation State (Radians)
    private float _targetYaw;
    private float _targetPitch;

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
                             // PhysicsInterpolationMode = PhysicsInterpolationModeEnum.On; // API not available

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

        // Initialize target rotation to prevent snapping (Use Radians)
        _targetYaw = Rotation.Y;
        _targetPitch = Rotation.X;
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

        // Allow Orbit Rotation (Right Click required ONLY if mouse is not captured)
        bool isCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
        if (@event is InputEventMouseMotion motion && (isCaptured || Input.IsMouseButtonPressed(MouseButton.Right)))
        {
            // Update Target Rotation (Accumulate Input converted to Radians)
            float deltaYawDeg = motion.Relative.X * _lookSensitivity;
            float deltaPitchDeg = motion.Relative.Y * _lookSensitivity;

            _targetYaw -= Mathf.DegToRad(deltaYawDeg);
            _targetPitch -= Mathf.DegToRad(deltaPitchDeg);

            // Clamp pitch immediately on the target to prevent "winding up" past limits
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.DegToRad(-80), Mathf.DegToRad(80));
        }
    }

    public override void _Process(double delta)
    {
        // Smooth rotation interpolation (Radians)
        float currentYaw = Rotation.Y;
        float currentPitch = Rotation.X;

        float newYaw = Mathf.LerpAngle(currentYaw, _targetYaw, (float)delta * RotationSmoothSpeed);
        float newPitch = Mathf.LerpAngle(currentPitch, _targetPitch, (float)delta * RotationSmoothSpeed);

        Rotation = new Vector3(newPitch, newYaw, 0);

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
        else
        {
            // Walking Camera (Independent Orbit) with Optional Target Bias
            float zoomT = 1.0f - (_zoomDistance / ZoomMax);
            float height = Mathf.Lerp(FollowOffset.Y, 1.7f, zoomT);
            float shoulderOffset = Mathf.Lerp(0f, 0.5f, Mathf.Clamp(zoomT * 2f, 0f, 1f));

            // Calculate desired orbit position
            Vector3 desiredOffset = new Vector3(shoulderOffset, height, _zoomDistance);
            Vector3 orbitPos = _target.GlobalPosition + (GlobalBasis * desiredOffset);

            // --- Camera Ground/Wall Collision ---
            // Cast from player pivot to desired orbit position.
            // If we hit geometry, shorten the arm so the camera stays in front.
            Vector3 pivotPos = _target.GlobalPosition + new Vector3(0, height, 0);
            Vector3 toCamera = orbitPos - pivotPos;
            float desiredDist = toCamera.Length();

            if (desiredDist > 0.1f)
            {
                var spaceState = GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(
                    pivotPos,
                    orbitPos,
                    collisionMask: 2 // Layer 2 = terrain
                );
                // Exclude the player's own body from the cast
                if (_target is CharacterBody3D cb)
                    query.Exclude = new Godot.Collections.Array<Rid> { cb.GetRid() };

                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    Vector3 hitPos = (Vector3)result["position"];
                    Vector3 hitNormal = (Vector3)result["normal"];
                    // Pull camera back along the hit normal slightly so it doesn't sit ON the surface
                    orbitPos = hitPos + hitNormal * 0.3f;
                }
            }

            // Lerp Position for smoothness
            GlobalPosition = GlobalPosition.Lerp(orbitPos, (float)delta * SmoothSpeed);

            // In first person, attach to head bone (overrides orbit)
            if (_zoomDistance < 0.5f)
            {
                Vector3 headPos = GetHeadPosition();
                Vector3 forwardOffset = -GlobalBasis.Z * 0.3f;
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
    }

    public void SnapBehind(Node3D target)
    {
        if (target == null) return;
        _target = target;

        // Match target horizontal rotation but keep specific pitch
        Vector3 targetRot = target.GlobalRotation;
        GlobalRotation = new Vector3(Mathf.DegToRad(-15), targetRot.Y, 0);

        // Sync smoothing targets to prevent drift (Radians)
        _targetPitch = Rotation.X;
        _targetYaw = Rotation.Y;

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
