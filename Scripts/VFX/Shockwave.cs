using Godot;
using System;

namespace Archery;

public partial class Shockwave : Node3D
{
    private MeshInstance3D _mesh;
    private StandardMaterial3D _material;

    private float _timer = 0.0f;
    private float _lifetime = 0.6f;
    private float _maxRadius = 5.0f;

    public override void _Ready()
    {
        _mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
        if (_mesh != null && _mesh.MaterialOverride is StandardMaterial3D mat)
        {
            _material = (StandardMaterial3D)mat.Duplicate();
            _mesh.MaterialOverride = _material;
        }

        Scale = Vector3.Zero;
    }

    public void SetColor(Color color)
    {
        if (_material != null)
        {
            color.A = 0.8f; // Semi-transparent
            _material.AlbedoColor = color;
            _material.Emission = color;
        }
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        float t = _timer / _lifetime;

        if (t >= 1.0f)
        {
            QueueFree();
            return;
        }

        // Expanding scale
        float curScale = Mathf.Lerp(0.0f, _maxRadius, Mathf.Sqrt(t));
        Scale = new Vector3(curScale, 1.0f, curScale);

        // Fading alpha
        if (_material != null)
        {
            Color c = _material.AlbedoColor;
            c.A = 1.0f - t;
            _material.AlbedoColor = c;

            if (_material.EmissionEnabled)
            {
                _material.EmissionEnergyMultiplier = (1.0f - t) * 2.0f;
            }
        }
    }
}
