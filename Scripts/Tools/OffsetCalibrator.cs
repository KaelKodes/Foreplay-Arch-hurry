using Godot;
using System;
using System.Linq;
using Archery;

public partial class OffsetCalibrator : Node
{
    [Export] public NodePath TargetPath;
    [Export] public float MoveSpeed = 0.2f;
    [Export] public float RotateSpeed = 25.0f;

    private Node3D _target;

    public override void _Ready()
    {
        TryFindTarget();
    }

    public override void _Process(double delta)
    {
        // Only run for the local player to avoid multiple instances fighting for input
        var parent = GetParent();
        if (parent is PlayerController pc && !pc.IsLocal) return;

        // Continuous search if no target
        if (_target == null)
        {
            TryFindTarget();
            if (_target == null)
            {
                if (Input.IsAnythingPressed() && Time.GetTicksMsec() % 1000 < 20)
                {
                    GD.Print("[OffsetCalibrator] Keys pressed but NO TARGET found.");
                }
                return;
            }
        }

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
            moveSpd *= 0.05f;
            rotSpd *= 0.1f;
        }

        Vector3 cameraRelativeMove = Vector3.Zero;
        Vector3 localRotation = Vector3.Zero;

        if (Input.IsKeyPressed(Key.Up)) cameraRelativeMove.Y += 1;
        if (Input.IsKeyPressed(Key.Down)) cameraRelativeMove.Y -= 1;
        if (Input.IsKeyPressed(Key.Left)) cameraRelativeMove.X -= 1;
        if (Input.IsKeyPressed(Key.Right)) cameraRelativeMove.X += 1;
        if (Input.IsKeyPressed(Key.Pageup)) cameraRelativeMove.Z -= 1;
        if (Input.IsKeyPressed(Key.Pagedown)) cameraRelativeMove.Z += 1;

        if (isRotate)
        {
            if (Input.IsKeyPressed(Key.Up)) localRotation.X += 1;
            if (Input.IsKeyPressed(Key.Down)) localRotation.X -= 1;
            if (Input.IsKeyPressed(Key.Left)) localRotation.Y += 1;
            if (Input.IsKeyPressed(Key.Right)) localRotation.Y -= 1;
            if (Input.IsKeyPressed(Key.Pageup)) localRotation.Z += 1;
            if (Input.IsKeyPressed(Key.Pagedown)) localRotation.Z -= 1;
        }

        // Reset Key
        if (Input.IsKeyPressed(Key.Delete) || Input.IsKeyPressed(Key.Backspace))
        {
            _target.Position = Vector3.Zero;
            _target.RotationDegrees = Vector3.Zero;
            GD.Print("[OffsetCalibrator] Reset target transform to zero.");
            return;
        }

        // Apply
        if (cameraRelativeMove != Vector3.Zero || localRotation != Vector3.Zero)
        {
            Camera3D cam = GetViewport().GetCamera3D();

            if (isRotate && localRotation != Vector3.Zero)
            {
                float rotRad = Mathf.DegToRad(rotSpd);
                if (cam != null)
                {
                    if (localRotation.Y != 0) _target.GlobalRotate(cam.GlobalBasis.Y, localRotation.Y * rotRad);
                    if (localRotation.X != 0) _target.GlobalRotate(cam.GlobalBasis.X, localRotation.X * rotRad);
                    if (localRotation.Z != 0) _target.GlobalRotate(cam.GlobalBasis.Z, localRotation.Z * rotRad);
                }
                else
                {
                    _target.RotateObjectLocal(Vector3.Up, localRotation.Y * rotRad);
                    _target.RotateObjectLocal(Vector3.Right, localRotation.X * rotRad);
                    _target.RotateObjectLocal(Vector3.Back, localRotation.Z * rotRad);
                }
            }
            else if (cameraRelativeMove != Vector3.Zero)
            {
                if (cam != null)
                {
                    Vector3 worldMove = Vector3.Zero;
                    worldMove += cam.GlobalBasis.X * cameraRelativeMove.X;
                    worldMove += cam.GlobalBasis.Y * cameraRelativeMove.Y;
                    worldMove += cam.GlobalBasis.Z * cameraRelativeMove.Z;

                    if (_target is ArrowController)
                    {
                        var archerySys = GetArcherySystem();
                        if (archerySys != null)
                        {
                            Vector3 deltaOffset = _target.GlobalBasis.Inverse() * (worldMove * moveSpd);
                            archerySys.DebugArrowOffsetPosition += deltaOffset;
                        }
                    }
                    else
                    {
                        _target.GlobalPosition += worldMove * moveSpd;
                    }
                }
                else
                {
                    _target.TranslateObjectLocal(cameraRelativeMove * moveSpd);
                }
            }
        }

        if (Input.IsActionJustPressed("ui_accept") || Input.IsKeyPressed(Key.Enter))
        {
            PrintTransform();
        }
    }

    private ArcherySystem GetArcherySystem()
    {
        return GetTree().CurrentScene.FindChild("ArcherySystem", true, false) as ArcherySystem;
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
            _target = arrows[0] as Node3D;
            if (_target != null) return;
        }

        var stolen = GetTree().GetNodesInGroup("stolen_weapons");
        if (stolen.Count > 0)
        {
            foreach (var node in stolen)
            {
                if (node is Node3D n3d && n3d.IsInsideTree())
                {
                    _target = n3d;
                    GD.Print($"[OffsetCalibrator] Targeting stolen weapon: {_target.Name}");
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
            }
        }
        else
        {
            Vector3 p2 = _target.Position;
            Vector3 r2 = _target.RotationDegrees;
            GD.Print($"[CALIBRATION] Position: new Vector3({p2.X:F3}f, {p2.Y:F3}f, {p2.Z:F3}f); | Rotation: new Vector3({r2.X:F3}f, {r2.Y:F3}f, {r2.Z:F3}f);");
        }
    }
}
