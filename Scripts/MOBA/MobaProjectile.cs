using Godot;

namespace Archery;

/// <summary>
/// A projectile that arcs toward a target and deals damage on arrival.
/// Used by Crawlers (spike) and Towers (energy bolt).
/// </summary>
public partial class MobaProjectile : Node3D
{
    [Export] public float Speed = 15f;
    [Export] public float Damage = 15f;
    [Export] public float MaxLifetime = 5f;
    [Export] public float ArcHeight = 4f;

    public MobaTeam SourceTeam = MobaTeam.None;
    public Node3D Target;

    private float _lifetime = 0f;
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _totalTravelTime;
    private float _elapsedTravelTime = 0f;
    private bool _initialized = false;

    public override void _Ready()
    {
        // Add minimal delay or just wait for explicit Initialize() call
    }

    /// <summary>
    /// Explicitly start the projectile travel. 
    /// Should be called AFTER setting the initial GlobalPosition.
    /// </summary>
    public void Initialize()
    {
        // Store start position now that it has been set by the caller
        _startPos = GlobalPosition;

        // Calculate target
        if (Target != null && IsInstanceValid(Target) && Target.IsInsideTree())
        {
            _targetPos = Target.GlobalPosition + Vector3.Up * 0.5f;
        }
        else
        {
            _targetPos = _startPos + Vector3.Forward * 10f;
        }

        float distance = _startPos.DistanceTo(_targetPos);
        _totalTravelTime = distance / Speed;
        if (_totalTravelTime < 0.1f) _totalTravelTime = 0.1f;

        _initialized = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized) return;

        float dt = (float)delta;
        _lifetime += dt;
        _elapsedTravelTime += dt;

        if (_lifetime > MaxLifetime)
        {
            QueueFree();
            return;
        }

        // Update target position if target is still alive (track moving targets)
        if (Target != null && IsInstanceValid(Target) && Target.IsInsideTree())
        {
            _targetPos = Target.GlobalPosition + Vector3.Up * 0.5f;
            float distance = _startPos.DistanceTo(_targetPos);
            _totalTravelTime = distance / Speed;
            if (_totalTravelTime < 0.1f) _totalTravelTime = 0.1f;
        }

        // Progress along the arc (0 to 1)
        float t = Mathf.Clamp(_elapsedTravelTime / _totalTravelTime, 0f, 1f);

        // Lerp horizontal position
        Vector3 linearPos = _startPos.Lerp(_targetPos, t);

        // Add parabolic arc: peaks at t=0.5
        float arcOffset = 4f * ArcHeight * t * (1f - t);
        linearPos.Y += arcOffset;

        GlobalPosition = linearPos;

        // Face direction of travel
        Vector3 nextPos = _startPos.Lerp(_targetPos, Mathf.Min(t + 0.05f, 1f));
        float nextArc = 4f * ArcHeight * Mathf.Min(t + 0.05f, 1f) * (1f - Mathf.Min(t + 0.05f, 1f));
        nextPos.Y += nextArc;
        Vector3 lookDir = (nextPos - GlobalPosition).Normalized();
        if (lookDir.LengthSquared() > 0.001f)
        {
            LookAt(GlobalPosition + lookDir, Vector3.Up);
        }

        // Check if arrived
        if (t >= 1f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        if (Target != null && IsInstanceValid(Target) && Target.IsInsideTree())
        {
            if (Target is MobaMinion minion)
            {
                minion.OnHit(Damage, minion.GlobalPosition, Vector3.Up);
            }
            else if (Target is Monsters monster)
            {
                monster.OnHit(Damage, monster.GlobalPosition, Vector3.Up);
            }
            else if (Target is MobaTower tower)
            {
                tower.TakeDamage(Damage);
            }
            else if (Target is MobaNexus nexus)
            {
                nexus.TakeDamage(Damage);
            }
            else if (Target is PlayerController player)
            {
                GD.Print($"[MobaProjectile] Hit player {player.Name} for {Damage}");
            }
        }

        QueueFree();
    }
}
