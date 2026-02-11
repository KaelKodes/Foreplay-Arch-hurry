using Godot;

namespace Archery;

/// <summary>
/// A quick rising golden/white smoke effect for holy heals.
/// Spawns at target position, rises, fades, then self-destructs.
/// </summary>
public partial class HolySmoke : Node3D
{
    private GpuParticles3D _particles;
    private float _lifetime = 1.5f;
    private float _timer = 0f;

    public override void _Ready()
    {
        // Create the particle emitter programmatically
        _particles = new GpuParticles3D();
        AddChild(_particles);

        _particles.Amount = 12;
        _particles.Lifetime = 1.2f;
        _particles.OneShot = true;
        _particles.Explosiveness = 0.8f;
        _particles.FixedFps = 30;

        // Process material
        var processMat = new ParticleProcessMaterial();
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 15f;
        processMat.InitialVelocityMin = 0.5f;
        processMat.InitialVelocityMax = 1.5f;
        processMat.Gravity = new Vector3(0, -0.2f, 0);
        processMat.ScaleMin = 0.15f;
        processMat.ScaleMax = 0.35f;

        // Golden/holy color gradient
        var colorRamp = new Gradient();
        colorRamp.SetColor(0, new Color(1.0f, 0.95f, 0.6f, 0.9f));  // Warm golden start
        colorRamp.SetColor(1, new Color(1.0f, 1.0f, 0.85f, 0.0f));  // Fades to transparent white
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = colorRamp;
        processMat.ColorRamp = gradientTex;

        _particles.ProcessMaterial = processMat;

        // Draw pass — simple quad mesh
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 0.15f);

        var drawMat = new StandardMaterial3D();
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.AlbedoColor = new Color(1.0f, 0.95f, 0.7f, 0.8f);
        drawMat.EmissionEnabled = true;
        drawMat.Emission = new Color(1.0f, 0.9f, 0.5f);
        drawMat.EmissionEnergyMultiplier = 1.5f;
        drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        quadMesh.Material = drawMat;

        _particles.DrawPass1 = quadMesh;

        // Offset particles slightly upward from feet
        _particles.Position = new Vector3(0, 1.0f, 0);

        _particles.Emitting = true;
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer >= _lifetime)
        {
            QueueFree();
        }
    }
}
