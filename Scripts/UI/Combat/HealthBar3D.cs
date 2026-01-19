using Godot;

namespace Archery;

/// <summary>
/// 3D health bar that floats above an enemy and shows current health.
/// Uses a simple mesh-based approach with two quads (background and fill).
/// </summary>
public partial class HealthBar3D : Node3D
{
    private MeshInstance3D _background;
    private MeshInstance3D _fill;
    private float _currentPercent = 1.0f;
    private float _targetPercent = 1.0f;
    private float _barWidth = 1.0f;

    public override void _Ready()
    {
        CreateBar();
    }

    private void CreateBar()
    {
        // Background (dark red)
        _background = new MeshInstance3D();
        var bgMesh = new QuadMesh();
        bgMesh.Size = new Vector2(_barWidth, 0.1f);
        _background.Mesh = bgMesh;

        var bgMat = new StandardMaterial3D();
        bgMat.AlbedoColor = new Color(0.3f, 0.1f, 0.1f, 0.8f);
        bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        bgMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        bgMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        bgMat.NoDepthTest = true;
        _background.MaterialOverride = bgMat;
        AddChild(_background);

        // Fill (green)
        _fill = new MeshInstance3D();
        var fillMesh = new QuadMesh();
        fillMesh.Size = new Vector2(_barWidth, 0.1f);
        _fill.Mesh = fillMesh;

        var fillMat = new StandardMaterial3D();
        fillMat.AlbedoColor = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        fillMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        fillMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        fillMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        fillMat.NoDepthTest = true;
        _fill.MaterialOverride = fillMat;
        _fill.Position = new Vector3(0, 0, 0.01f); // Slightly in front of background
        AddChild(_fill);
    }

    public void UpdateHealth(float current, float max)
    {
        _targetPercent = Mathf.Clamp(current / max, 0f, 1f);

        // Update fill color based on health percentage
        if (_fill.MaterialOverride is StandardMaterial3D mat)
        {
            if (_targetPercent > 0.5f)
            {
                mat.AlbedoColor = new Color(0.2f, 0.9f, 0.2f, 0.9f); // Green
            }
            else if (_targetPercent > 0.25f)
            {
                mat.AlbedoColor = new Color(0.9f, 0.9f, 0.2f, 0.9f); // Yellow
            }
            else
            {
                mat.AlbedoColor = new Color(0.9f, 0.2f, 0.2f, 0.9f); // Red
            }
        }
    }

    public override void _Process(double delta)
    {
        // Smooth interpolation to target
        _currentPercent = Mathf.Lerp(_currentPercent, _targetPercent, (float)delta * 10f);

        // Update fill mesh scale and position
        if (_fill != null && _fill.Mesh is QuadMesh fillMesh)
        {
            float width = _barWidth * _currentPercent;
            fillMesh.Size = new Vector2(width, 0.1f);

            // Offset to left-align the fill
            float offset = (_barWidth - width) / 2f;
            _fill.Position = new Vector3(-offset, 0, 0.01f);
        }
    }
}
