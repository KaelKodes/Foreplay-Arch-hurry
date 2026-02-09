using Godot;
using System;
using System.Linq;
using Archery;

public partial class OffsetCalibrator : Node
{
    [Export] public NodePath TargetPath;
    [Export] public float MoveSpeed = 0.2f; // Slower default for precision
    [Export] public float RotateSpeed = 25.0f;

    private Node3D _target;

    public override void _Ready()
    {
        TryFindTarget();
    }

    public override void _Process(double delta)
    {
        // Continuous search if no target
        if (_target == null)
        {
            TryFindTarget();
            if (_target == null) return;
        }

        // If target became invalid (deleted), clear it
        if (!IsInstanceValid(_target))
        {
            _target = null;
            return;
        }

        float dt = (float)delta;
        float moveSpd = MoveSpeed * dt;
        float rotSpd = RotateSpeed * dt;

        bool isPrecision = Input.IsKeyPressed(Key.Shift);
        bool isRotate = Input.IsKeyPressed(Key.Ctrl);

        if (isPrecision)
        {
            moveSpd *= 0.05f; // Extra precise with Shift
            rotSpd *= 0.1f;
        }

        Vector3 cameraRelativeMove = Vector3.Zero;
        Vector3 localRotation = Vector3.Zero;

        // Input Handling
        // Camera-Relative Movement Directions
        // Up/Down Arrow = Up/Down on Screen (Camera Y)
        // Left/Right Arrow = Left/Right on Screen (Camera X)
        // PgUp/PgDn = Forward/Backward/Depth (Camera -Z)

        if (Input.IsKeyPressed(Key.Up)) cameraRelativeMove.Y += 1;
        if (Input.IsKeyPressed(Key.Down)) cameraRelativeMove.Y -= 1;
        if (Input.IsKeyPressed(Key.Left)) cameraRelativeMove.X -= 1;
        if (Input.IsKeyPressed(Key.Right)) cameraRelativeMove.X += 1;
        if (Input.IsKeyPressed(Key.Pageup)) cameraRelativeMove.Z -= 1; // Away/Depth
        if (Input.IsKeyPressed(Key.Pagedown)) cameraRelativeMove.Z += 1; // Toward

        // Rotation Input (Still Local for now, usually clearer for rotation)
        if (isRotate)
        {
            if (Input.IsKeyPressed(Key.Up)) localRotation.X += 1; // Pitch
            if (Input.IsKeyPressed(Key.Down)) localRotation.X -= 1;
            if (Input.IsKeyPressed(Key.Left)) localRotation.Y += 1; // Yaw
            if (Input.IsKeyPressed(Key.Right)) localRotation.Y -= 1;
            if (Input.IsKeyPressed(Key.Pageup)) localRotation.Z += 1; // Roll
            if (Input.IsKeyPressed(Key.Pagedown)) localRotation.Z -= 1;
        }

        // Apply
        if (cameraRelativeMove != Vector3.Zero || localRotation != Vector3.Zero)
        {
            // Special Case: Arrow Calibration via ArcherySystem
            if (_target is ArrowController)
            {
                var archerySys = GetArcherySystem();
                if (archerySys != null && _target.IsInsideTree())
                {
                    Camera3D cam = GetViewport().GetCamera3D();
                    if (cam != null && !isRotate)
                    {
                        // 1. Convert Camera-Relative Input to World Space Vector
                        Vector3 worldMove = Vector3.Zero;
                        worldMove += cam.GlobalBasis.X * cameraRelativeMove.X; // Left/Right
                        worldMove += cam.GlobalBasis.Y * cameraRelativeMove.Y; // Up/Down
                        worldMove += cam.GlobalBasis.Z * cameraRelativeMove.Z; // Depth (Forward/Back)

                        // 2. Convert World Move to Target's Local Space
                        // We want: LocalOffset += LocalBasis.Inverse * WorldMove
                        // Because: GlobalPos = Parent * (Basis * Offset). 
                        // Actually ArcherySystem logic is: t.Origin += t.Basis * Offset.
                        // So world displacement D = t.Basis * deltaOffset
                        // deltaOffset = t.Basis.Inverse * D

                        // Use Current Arrow GlobalBasis because that represents 't.Basis' (mostly)
                        // Or better, use the Arrow's current global rotation.
                        Vector3 deltaOffset = _target.GlobalBasis.Inverse() * (worldMove * moveSpd);
                        archerySys.DebugArrowOffsetPosition += deltaOffset;
                    }
                    else if (isRotate)
                    {
                        // Apply Rotation normally (Local)
                        archerySys.DebugArrowOffsetRotation += localRotation * rotSpd;
                    }
                    else
                    {
                        // Fallback if no camera (rare)
                        archerySys.DebugArrowOffsetPosition += cameraRelativeMove * moveSpd;
                    }
                    return;
                }
            }

            // Standard Object Fallback
            if (isRotate)
            {
                _target.RotateObjectLocal(Vector3.Up, localRotation.Y * rotSpd);
                _target.RotateObjectLocal(Vector3.Right, localRotation.X * rotSpd);
                _target.RotateObjectLocal(Vector3.Back, localRotation.Z * rotSpd);
            }
            else
            {
                // Move standard targets in Global Space for ease? Or Local? 
                // Let's keep Standard targets simple for now.
                _target.TranslateObjectLocal(cameraRelativeMove * moveSpd);
            }
        }

        if (Input.IsKeyPressed(Key.Enter))
        {
            PrintTransform();
        }
    }

    private ArcherySystem GetArcherySystem()
    {
        var sys = GetTree().CurrentScene.FindChild("ArcherySystem", true, false) as ArcherySystem;
        return sys;
    }

    private void TryFindTarget()
    {
        if (TargetPath != null && !TargetPath.IsEmpty)
        {
            _target = GetNodeOrNull<Node3D>(TargetPath);
            if (_target != null) return;
        }

        var arrows = GetTree().GetNodesInGroup("arrows");
        if (arrows.Count > 0)
        {
            foreach (var node in arrows)
            {
                if (node is Node3D n3d)
                {
                    _target = n3d;
                    return;
                }
            }
        }
    }

    private void PrintTransform()
    {
        if (_target == null) return;

        if (_target is ArrowController)
        {
            var sys = GetArcherySystem();
            if (sys != null)
            {
                Vector3 p = sys.DebugArrowOffsetPosition;
                Vector3 r = sys.DebugArrowOffsetRotation;
                GD.Print($"[CALIBRATION OFFSET] Position: new Vector3({p.X:F3}f, {p.Y:F3}f, {p.Z:F3}f); | Rotation: new Vector3({r.X:F3}f, {r.Y:F3}f, {r.Z:F3}f);");
                return;
            }
        }

        Vector3 p2 = _target.Position;
        Vector3 r2 = _target.RotationDegrees;
        GD.Print($"[CALIBRATION] Position: new Vector3({p2.X:F3}f, {p2.Y:F3}f, {p2.Z:F3}f); | Rotation: new Vector3({r2.X:F3}f, {r2.Y:F3}f, {r2.Z:F3}f);");
    }
}
