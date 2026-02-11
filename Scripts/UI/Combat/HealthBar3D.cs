using Godot;

namespace Archery;

/// <summary>
/// 3D health bar that floats above an enemy and shows current health.
/// Uses a simple mesh-based approach with two quads (background and fill).
/// </summary>
public partial class HealthBar3D : Node3D
{
    private MeshInstance3D _fill;
    private MeshInstance3D _shieldFill;
    private float _currentPercent = 1.0f;
    private float _shieldPercent = 0.0f;
    private float _targetPercent = 1.0f;
    private float _targetShieldPercent = 0.0f;
    private float _barWidth = 1.0f;

    public override void _Ready()
    {
        CreateBar();
    }

    private void CreateBar()
    {
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
        _fill.Position = Vector3.Zero; // Center the bar
        AddChild(_fill);

        // Shield (blue) overlay
        _shieldFill = new MeshInstance3D();
        var shieldMesh = new QuadMesh();
        shieldMesh.Size = new Vector2(0, 0.1f);
        _shieldFill.Mesh = shieldMesh;

        var shieldMat = new StandardMaterial3D();
        shieldMat.AlbedoColor = new Color(0.3f, 0.6f, 1.0f, 0.9f); // Blue
        shieldMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        shieldMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        shieldMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        shieldMat.NoDepthTest = true;
        _shieldFill.MaterialOverride = shieldMat;
        _shieldFill.Position = new Vector3(0, 0, 0.01f); // Slightly ahead of green bar
        AddChild(_shieldFill);
    }

    public void UpdateHealth(float current, float max, float shield = 0)
    {
        _targetPercent = Mathf.Clamp(current / max, 0f, 1f);
        _targetShieldPercent = Mathf.Clamp(shield / max, 0f, 1f);

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
        _shieldPercent = Mathf.Lerp(_shieldPercent, _targetShieldPercent, (float)delta * 10f);

        // Update health fill
        if (_fill != null && _fill.Mesh is QuadMesh fillMesh)
        {
            float width = _barWidth * _currentPercent;
            fillMesh.Size = new Vector2(width, 0.1f);

            float offset = (_barWidth - width) / 2f;
            _fill.Position = new Vector3(-offset, 0, 0f);
        }

        // Update shield fill
        if (_shieldFill != null && _shieldFill.Mesh is QuadMesh shieldMesh)
        {
            float width = _barWidth * _shieldPercent;
            shieldMesh.Size = new Vector2(width, 0.1f);

            float offset = (_barWidth - width) / 2f;
            _shieldFill.Position = new Vector3(-offset, 0, 0.005f);
            _shieldFill.Visible = _shieldPercent > 0.01f;
        }
    }
}
