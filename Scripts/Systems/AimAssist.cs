using Godot;
using System;

namespace Archery;

public partial class AimAssist : Node3D
{
    [Export] public NodePath ArcherySystemPath;
    private ArcherySystem _archerySystem;

    private MeshInstance3D _aimLine;
    private MeshInstance3D _landingMarker;
    private MeshInstance3D _trajectoryArc;

    private Camera3D _camera;
    private WindSystem _windSystem;
    private bool _isLocked = false;

    public override void _ExitTree()
    {
        if (_archerySystem != null)
        {
            _archerySystem.ModeChanged -= OnModeChanged;
            _archerySystem.DrawStageChanged -= OnStageChanged;
            _archerySystem.ShotModeChanged -= OnShotModeChanged;
        }
    }

    public override void _Ready()
    {
        _aimLine = GetNodeOrNull<MeshInstance3D>("AimLine");
        _landingMarker = GetNodeOrNull<MeshInstance3D>("LandingMarker");

        _trajectoryArc = GetNodeOrNull<MeshInstance3D>("TrajectoryArc");
        if (_trajectoryArc == null)
        {
            _trajectoryArc = new MeshInstance3D();
            _trajectoryArc.Name = "TrajectoryArc";
            AddChild(_trajectoryArc);

            var mat = new StandardMaterial3D();
            mat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
            mat.AlbedoColor = new Color(1, 1, 1, 0.8f);
            mat.Transparency = StandardMaterial3D.TransparencyEnum.Alpha;
            _trajectoryArc.MaterialOverride = mat;
        }

        if (ArcherySystemPath != null && !ArcherySystemPath.IsEmpty)
            _archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);

        // Per-Player Support: Check sibling
        if (_archerySystem == null)
            _archerySystem = GetNodeOrNull<ArcherySystem>("../ArcherySystem");

        // Fallback: Global/Root search
        if (_archerySystem == null)
        {
            _archerySystem = GetNodeOrNull<ArcherySystem>("/root/TerrainTest/ArcherySystem");
            if (_archerySystem == null) _archerySystem = GetNodeOrNull<ArcherySystem>("/root/DrivingRange/ArcherySystem");
            if (_archerySystem == null) _archerySystem = GetTree().Root.FindChild("ArcherySystem", true, false) as ArcherySystem;
        }

        if (_archerySystem != null && _archerySystem.WindSystemPath != null && !_archerySystem.WindSystemPath.IsEmpty)
        {
            _windSystem = _archerySystem.GetNodeOrNull<WindSystem>(_archerySystem.WindSystemPath);
        }

        if (_archerySystem != null)
        {
            GD.Print($"AimAssist: Linked to ArcherySystem at {_archerySystem.GetPath()}");
            // Unsubscribe first to avoid double subscription if re-entering
            _archerySystem.ModeChanged -= OnModeChanged;
            _archerySystem.ModeChanged += OnModeChanged;

            _archerySystem.DrawStageChanged -= OnStageChanged;
            _archerySystem.DrawStageChanged += OnStageChanged;

            _archerySystem.ShotModeChanged -= OnShotModeChanged;
            _archerySystem.ShotModeChanged += OnShotModeChanged;
        }

        _camera = GetViewport().GetCamera3D();
        Visible = false;
        SetProcess(false);
    }

    private void OnStageChanged(int newStage)
    {
        if (newStage == (int)DrawStage.Executing)
        {
            _isLocked = true;
        }
        else if (newStage == (int)DrawStage.Idle)
        {
            _isLocked = false;
            UpdateVisuals();
        }
    }

    private void OnShotModeChanged(int newMode)
    {
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        if (_archerySystem == null || _camera == null) return;

        if (!_isLocked)
        {
            // Track arrow position, or fall back to player chest
            var arrow = _archerySystem.GetNodeOrNull<ArrowController>("Arrow");
            if (arrow != null)
                GlobalPosition = arrow.GlobalPosition;
            else if (_archerySystem.GetParent() is PlayerController pc)
                GlobalPosition = pc.GlobalPosition + (pc.GlobalBasis * (new Vector3(0, 1.3f, 0) + new Vector3(0, 0, 0.5f)));
            else
                GlobalPosition = Vector3.Zero;

            if (_archerySystem.CurrentTarget != null)
            {
                // Align with Target
                Vector3 targetPos = _archerySystem.CurrentTarget.GlobalPosition;
                if (_archerySystem.CurrentTarget is InteractableObject io) targetPos += new Vector3(0, 1.0f, 0); // Sign offset

                Vector3 dir = (targetPos - GlobalPosition).Normalized();
                // AimAssist forward is -Z.
                // We want -Z to point at 'dir'.
                // Atan2(-dir.X, -dir.Y)
                float angle = Mathf.Atan2(-dir.X, -dir.Z);
                GlobalRotation = new Vector3(0, angle, 0);
            }
            else
            {
                // Align with Camera
                Vector3 camRot = _camera.GlobalRotation;
                GlobalRotation = new Vector3(0, camRot.Y, 0);
            }

            UpdateVisuals();
        }
    }

    private void OnModeChanged(bool isCombat)
    {
        Visible = isCombat;
        SetProcess(isCombat);

        if (isCombat)
        {
            _camera = GetViewport().GetCamera3D();
            _isLocked = false;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (_archerySystem == null) return;

        // Predict MAX power shot
        float baseVelocity = ArcheryConstants.BASE_VELOCITY;

        // Use REAL player stats for prediction, not hardcoded 100
        int playerPower = _archerySystem.PlayerStats.Strength;
        float powerStatMult = playerPower / 10.0f;

        // Use locked power if available, otherwise assume "Perfect" shot (94%)
        float powerFactor = 1.0f;
        if (_archerySystem.CurrentStage == DrawStage.Aiming || _archerySystem.CurrentStage == DrawStage.Executing)
        {
            // Use actually locked power
            // We need a way to get locked power from ArcherySystem.
            // For now, let's just use PERFECT_POWER as the baseline for prediction.
            powerFactor = ArcheryConstants.PERFECT_POWER_VALUE / 100.0f;
        }
        else
        {
            powerFactor = ArcheryConstants.PERFECT_POWER_VALUE / 100.0f;
        }

        float totalLoft = 12.0f;
        if (_archerySystem != null)
        {
            switch (_archerySystem.CurrentMode)
            {
                case ArcheryShotMode.Standard: totalLoft = 12.0f; break;
                case ArcheryShotMode.Long: totalLoft = 25.0f; break;
                case ArcheryShotMode.Max: totalLoft = 45.0f; break;
            }
        }
        float loftRad = Mathf.DegToRad(totalLoft);

        // USE LOCAL FORWARD (AimAssist is already rotated to match camera Y)
        Vector3 launchDir = Vector3.Forward; // (0, 0, -1)
        launchDir.Y = Mathf.Sin(loftRad);
        launchDir = launchDir.Normalized();

        float launchPower = baseVelocity * powerStatMult * powerFactor;
        Vector3 initialVelocity = launchDir * launchPower;

        var points = SimulateFlight(initialVelocity);

        if (points.Count > 0)
        {
            Vector3 landingPoint = points[points.Count - 1];
            // Distance along the horizontal plane relative to center
            float predictedMeters = new Vector2(landingPoint.X, landingPoint.Z).Length();

            if (_aimLine != null)
            {
                _aimLine.Scale = new Vector3(_aimLine.Scale.X, _aimLine.Scale.Y, predictedMeters);
                _aimLine.Position = new Vector3(0, 0, -predictedMeters / 2.0f);
            }

            if (_landingMarker != null)
            {
                _landingMarker.Position = new Vector3(0, 0, -predictedMeters);
            }

            DrawTrajectoryArc(points);
        }
    }

    private System.Collections.Generic.List<Vector3> SimulateFlight(Vector3 velocity)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        Vector3 pos = Vector3.Zero;
        Vector3 currentVelocity = velocity; // Renamed from currentVel for consistency with snippet
        float dt = 1.0f / 60.0f;
        float maxTime = 10f;

        points.Add(pos);

        for (float t = 0; t < maxTime; t += dt)
        {
            float speed = currentVelocity.Length();
            if (speed < 0.1f) break;

            // Drag acceleration = (Force / Mass). match ArrowController logic
            float mass = ArcheryConstants.ARROW_MASS;
            Vector3 dragForce = -currentVelocity.Normalized() * (speed * speed) * ArcheryConstants.DRAG_COEFFICIENT;
            currentVelocity += (dragForce / mass) * dt; // Use dt instead of step

            // Gravity
            currentVelocity += Vector3.Down * ArcheryConstants.GRAVITY * dt; // Use dt instead of step

            // Wind influence (matches ArrowController force application)
            if (_windSystem != null && _windSystem.IsWindEnabled)
            {
                Vector3 windForce = _windSystem.WindDirection * _windSystem.WindSpeedMph * 0.02f;
                currentVelocity += (windForce / mass) * dt; // Use dt instead of step
            }

            pos += currentVelocity * dt;

            points.Add(pos);

            if (pos.Y < -0.05f && t > 0.1f) break;
        }

        return points;
    }

    private void DrawTrajectoryArc(System.Collections.Generic.List<Vector3> points)
    {
        if (_trajectoryArc == null) return;

        var imm = new ImmediateMesh();
        _trajectoryArc.Mesh = imm;

        imm.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var p in points)
        {
            float horizontalDist = new Vector2(p.X, p.Z).Length();
            imm.SurfaceAddVertex(new Vector3(0, p.Y, -horizontalDist));
        }
        imm.SurfaceEnd();
    }
}
