using Godot;
using Archery;

public partial class RainOfArrowsEffect : Node3D
{
    private PackedScene _arrowScene;
    private float _duration = 1.6f; // Short burst
    private float _radius = 4.0f;
    private int _arrowCount = 12;
    private float _damage; // Currently unused unless applied to arrows manually
    private float _timer = 0f;
    private int _arrowsSpawned = 0;
    private CollisionObject3D _shooter;

    public void Start(PackedScene arrowScene, float damage, CollisionObject3D shooter)
    {
        _arrowScene = arrowScene;
        _damage = damage;
        _shooter = shooter;
        SetProcess(true);

        // Optional: Spawn a decal/indicator on ground?
        GD.Print($"[RainOfArrows] Started at {GlobalPosition}");
    }

    public override void _Ready()
    {
        SetProcess(false); // Wait for Start
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;

        // Spawn arrows based on time progression
        int desiredArrows = (int)((_timer / _duration) * _arrowCount);
        while (_arrowsSpawned < desiredArrows && _arrowsSpawned < _arrowCount)
        {
            SpawnArrow();
            _arrowsSpawned++;
        }

        if (_timer >= _duration + 1.0f) // Wait a bit for arrows to land before destroying spawner
        {
            QueueFree();
        }
    }

    private void SpawnArrow()
    {
        if (_arrowScene == null) return;

        var arrow = _arrowScene.Instantiate<ArrowController>();
        GetTree().CurrentScene.AddChild(arrow);

        // Random position in circle
        float angle = GD.Randf() * Mathf.Pi * 2;
        float dist = Mathf.Sqrt(GD.Randf()) * _radius; // Sqrt for uniform distribution
        Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, 12.0f, Mathf.Sin(angle) * dist); // Start 12m up

        Vector3 spawnPos = GlobalPosition + offset;
        Vector3 spawnRot = new Vector3(-Mathf.Pi / 2, 0, 0); // Point down (-90 x)

        // Randomize Y rotation for variety
        spawnRot.Y = GD.Randf() * Mathf.Pi * 2;

        // Launch
        // Note: ArrowController.Launch takes initial velocity. 
        // We want them to fall fast.
        Vector3 velocity = Vector3.Down * 25.0f;

        // Apply
        arrow.Launch(spawnPos, spawnRot, velocity, Vector3.Zero, false);

        // Set collision exception for shooter so they don't hit themselves if standing in rain
        if (_shooter != null)
        {
            arrow.SetCollisionException(_shooter);
        }
    }
}
