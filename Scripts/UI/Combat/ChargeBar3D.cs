using Godot;
using System;

namespace Archery;

/// <summary>
/// Mini 3D charge bar that appears under the player when charging an attack.
/// </summary>
public partial class ChargeBar3D : Node3D
{
    private MeshInstance3D _background;
    private MeshInstance3D _fill;

    private float _barWidth = 0.8f;
    private float _barHeight = 0.08f;

    private Color _colorNormal = new Color(1, 1, 1, 0.8f);     // White
    private Color _colorFull = new Color(1, 1, 0, 1.0f);       // Yellow for flash
    private Color _colorOvercharge = new Color(1, 0, 0, 1.0f); // Red for pulse

    private bool _hasFlashed = false;
    private float _pulseTimer = 0f;

    public override void _Ready()
    {
        CreateBar();
        Visible = false;
    }

    private void CreateBar()
    {
        // Background
        _background = new MeshInstance3D();
        var bgMesh = new QuadMesh { Size = new Vector2(_barWidth, _barHeight) };
        _background.Mesh = bgMesh;

        var bgMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0, 0, 0, 0.5f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
        _background.MaterialOverride = bgMat;
        AddChild(_background);

        // Fill
        _fill = new MeshInstance3D();
        var fillMesh = new QuadMesh { Size = new Vector2(0, _barHeight) };
        _fill.Mesh = fillMesh;

        var fillMat = new StandardMaterial3D
        {
            AlbedoColor = _colorNormal,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
        _fill.MaterialOverride = fillMat;
        _fill.Position = new Vector3(0, 0, 0.01f);
        AddChild(_fill);
    }

    public void Reset()
    {
        Visible = false;
        _hasFlashed = false;
        _pulseTimer = 0f;
        UpdateValue(0f);
        if (_fill.MaterialOverride is StandardMaterial3D mat) mat.AlbedoColor = _colorNormal;
    }

    public void UpdateValue(float holdTime)
    {
        if (holdTime < 0.05f)
        {
            Visible = false;
            return;
        }

        Visible = true;

        // Progress (0 to 1.5s is 0% to 100%)
        float percent = Mathf.Clamp(holdTime / 1.5f, 0f, 1f);
        float width = _barWidth * percent;

        if (_fill.Mesh is QuadMesh fillMesh)
        {
            fillMesh.Size = new Vector2(width, _barHeight);
            _fill.Position = new Vector3(-(_barWidth - width) / 2f, 0, 0.01f);
        }

        // Color Logic
        if (_fill.MaterialOverride is StandardMaterial3D mat)
        {
            if (holdTime >= 2.5f)
            {
                // Pulse Red
                _pulseTimer += (float)GetProcessDeltaTime() * 10f;
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) / 2f;
                mat.AlbedoColor = _colorOvercharge.Lerp(new Color(0.5f, 0, 0, 1.0f), pulse);
            }
            else if (holdTime >= 1.5f)
            {
                if (!_hasFlashed)
                {
                    FlashEffect();
                    _hasFlashed = true;
                }
                mat.AlbedoColor = _colorFull;
            }
            else
            {
                mat.AlbedoColor = _colorNormal;
            }
        }
    }

    private async void FlashEffect()
    {
        if (_fill.MaterialOverride is StandardMaterial3D mat)
        {
            var original = mat.AlbedoColor;
            mat.AlbedoColor = Colors.White;
            await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
            if (IsInsideTree()) mat.AlbedoColor = _colorFull;
        }
    }
}
