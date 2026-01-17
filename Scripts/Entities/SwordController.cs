using Godot;
using System;

namespace Archery;

/// <summary>
/// Controls the visual state and animations of the sword.
/// Driven by signals from MeleeSystem.
/// </summary>
public partial class SwordController : Node3D
{
    private MeshInstance3D _blade;
    private MeshInstance3D _handle;

    // Calibrated offsets for Erika's RightHand bone
    private Vector3 _idleRotation = new Vector3(-108.33f, -30.83f, -16.67f);
    private Vector3 _windupRotation = new Vector3(-120, 30, 0);
    private Vector3 _strikeRotation = new Vector3(-10, -45, 0);

    private Vector3 _idlePos = new Vector3(0.050f, 0.050f, 0.017f);
    private Vector3 _windupPos = new Vector3(0.05f, 0.2f, -0.1f);
    private Vector3 _strikePos = new Vector3(0.05f, 0.3f, 0.2f);

    private float _lerpSpeed = 18.0f;
    private Vector3 _targetRotation;
    private Vector3 _targetPosition;

    private Color _swordColor = Colors.DodgerBlue;

    public void SetColor(Color color)
    {
        _swordColor = color;

        // Blade
        if (_blade != null && _blade.MaterialOverride is StandardMaterial3D mat)
        {
            mat.AlbedoColor = _swordColor;
            mat.Emission = _swordColor;
        }

        // Slash Trail
        var slash = GetNodeOrNull<GpuParticles3D>("SlashEffect");
        if (slash != null)
        {
            // Tint the particles (Duplicate to ensure unique instance)
            if (slash.ProcessMaterial is ParticleProcessMaterial ppm)
            {
                // check if we need to duplicate (simple way: always duplicate on first set, or just duplicate)
                // To avoid leak, ideally we check if it is already unique, but Duplicate() is safe for small numbers.
                // Better pattern: Assign the new duplicated one back.
                var newPpm = (ParticleProcessMaterial)ppm.Duplicate();
                newPpm.Color = _swordColor;
                slash.ProcessMaterial = newPpm;
            }

            // Tint the mesh material (Duplicate to ensure unique instance)
            // Note: SlashEffect usually uses a DrawPass mesh or MaterialOverride.
            if (slash.MaterialOverride is StandardMaterial3D sm)
            {
                var newSm = (StandardMaterial3D)sm.Duplicate();
                newSm.AlbedoColor = _swordColor;
                newSm.Emission = _swordColor;
                slash.MaterialOverride = newSm;
            }
            else if (slash.DrawPasses > 0 && slash.DrawPass1 is PrimitiveMesh pm && pm.Material is StandardMaterial3D pmMat)
            {
                // Handle case where material is on the mesh itself
                var newMat = (StandardMaterial3D)pmMat.Duplicate();
                newMat.AlbedoColor = _swordColor;
                newMat.Emission = _swordColor;
                pm.Material = newMat;
            }
        }
    }

    public override void _Ready()
    {
        _blade = GetNodeOrNull<MeshInstance3D>("Blade");
        _handle = GetNodeOrNull<MeshInstance3D>("Handle");

        if (_blade != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = _swordColor;
            mat.EmissionEnabled = true;
            mat.Emission = _swordColor;
            mat.EmissionEnergyMultiplier = 1.2f;
            _blade.MaterialOverride = mat;
        }

        if (_handle != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.2f, 0.2f, 0.2f);
            _handle.MaterialOverride = mat;
        }

        // Apply color to everything (including SlashEffect) now that we are Ready
        SetColor(_swordColor);

        _targetRotation = _idleRotation;
        _targetPosition = _idlePos;
        RotationDegrees = _idleRotation;
        Position = _idlePos;
    }

    public void ConnectToMeleeSystem(MeleeSystem system)
    {
        // Procedural animations disabled in favor of FBX animations
        /*
        system.SwingStarted += OnSwingStarted;
        system.SwingPeak += OnSwingPeak;
        system.SwingComplete += OnSwingComplete;
        system.SwingValuesUpdated += OnSwingValuesUpdated;
        */
    }

    public override void _Process(double delta)
    {
        RotationDegrees = RotationDegrees.Lerp(_targetRotation, _lerpSpeed * (float)delta);
        Position = Position.Lerp(_targetPosition, _lerpSpeed * (float)delta);
    }

    private void OnSwingStarted()
    {
        _targetRotation = _windupRotation;
        _targetPosition = _windupPos;
    }

    private void OnSwingPeak(float power)
    {
        _targetRotation = _strikeRotation;
        _targetPosition = _strikePos;

        var slash = GetNodeOrNull<GpuParticles3D>("SlashEffect");
        if (slash != null)
        {
            slash.Emitting = true;
        }
    }

    private void OnSwingComplete(float power, float accuracy, float damage)
    {
        _targetRotation = _idleRotation;
        _targetPosition = _idlePos;
    }

    private void OnSwingValuesUpdated(float barValue, float power, int stateInt)
    {
        var state = (MeleeSystem.SwingState)stateInt;

        if (state == MeleeSystem.SwingState.Drawing || state == MeleeSystem.SwingState.Finishing)
        {
            float ratio = barValue / 100f;
            _targetRotation = _idleRotation.Lerp(_windupRotation, ratio);
            _targetPosition = _idlePos.Lerp(_windupPos, ratio);
        }
        else if (state == MeleeSystem.SwingState.Executing)
        {
            // ratio goes 0.0 -> 1.0 during strike (since barValue goes 100 -> 0)
            float ratio = 1.0f - (barValue / 100f);
            _targetRotation = _windupRotation.Lerp(_strikeRotation, ratio);

            // ARCH LOGIC: Push the sword OUT in an arc instead of a straight line
            Vector3 basePos = _windupPos.Lerp(_strikePos, ratio);

            // Forward arc (+Z is Forward now)
            float arcZ = Mathf.Sin(ratio * Mathf.Pi) * 1.5f;
            basePos.Z += arcZ;

            // Arc Width: Swing from Right (-X) to Left (+X)
            // We want it to bulge slightly Right (-X) to stay clear
            float arcX = Mathf.Sin(ratio * Mathf.Pi) * 0.5f;
            basePos.X -= arcX;

            _targetPosition = basePos;
        }
    }
}
