using Godot;
using System;

namespace Archery;

public partial class EldritchMissile : RigidBody3D
{
    private float _damage = 10f;
    private Node3D _target;
    private bool _hasHit = false;
    private MobaTeam _team = MobaTeam.None;
    private CollisionObject3D _caster;
    private float _lifetime = 5.0f; // Increased lifetime
    private float _timer = 0.0f;
    private float _homingStrength = 60.0f; // Strongly increased steering (was 35)
    private float _scanRange = 25.0f; // Increased scan range
    private float _activationDelay = 0.25f; // Quicker activation

    public void Launch(Vector3 targetPos, float damage, MobaTeam team, CollisionObject3D caster, Node3D targetNode = null)
    {
        _damage = damage;
        _team = team;
        _caster = caster;
        _target = targetNode;

        if (caster != null) AddCollisionExceptionWith(caster);

        // Find initial target only if none provided (for spread/8-way fire without a lock)
        if (!IsInstanceValid(_target))
        {
            _target = FindNearbyEnemy(targetPos, 8.0f);
        }

        // Calculate initial velocity for arcing path ("High Basketball Shot")
        Vector3 startPos = GlobalPosition;
        Vector3 diff = targetPos - startPos;
        float dist = new Vector2(diff.X, diff.Z).Length();

        // Horizontal direction
        Vector3 dir = new Vector3(diff.X, 0, diff.Z).Normalized();

        // Higher & Faster arc: 
        // We want it to be snappy.
        // Base speed factor increased (was 15.0f)
        float travelTime = 0.6f + (dist / 25.0f);
        travelTime = Mathf.Clamp(travelTime, 0.6f, 1.5f); // Cap at 1.5s max flight time

        // Vx = dist / time
        float vx = dist / travelTime;

        // Vy with higher arc bias
        float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * GravityScale;
        float vy = (diff.Y + 0.5f * g * travelTime * travelTime) / travelTime;

        // Boost Vy more for short distances to ensure it "clears" but keep it snappy
        float boost = Mathf.Lerp(5.0f, 2.0f, Mathf.Clamp(dist / 20.0f, 0, 1));
        vy += boost;

        // Apply impulse
        LinearVelocity = dir * vx + Vector3.Up * vy;

        // Random flair
        float randRange = 1.5f;
        LinearVelocity += new Vector3(GD.Randf() * randRange - randRange / 2, 0, GD.Randf() * randRange - randRange / 2);

        if (LinearVelocity.LengthSquared() > 0.1f)
            LookAt(GlobalPosition + LinearVelocity, Vector3.Up);
    }

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = 1;
        BodyEntered += OnBodyEntered;

        // Enable gravity but we'll assist with homing
        GravityScale = 0.8f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasHit) return;

        _timer += (float)delta;
        if (_timer >= _lifetime) { StopAndCleanup(); return; }

        // Scanning for target if we don't have one (or current one died)
        if (!IsInstanceValid(_target) || TargetingHelper.IsTargetDead(_target))
        {
            _target = FindNearbyEnemy(GlobalPosition, _scanRange);
        }

        // Homing Logic
        if (_timer > _activationDelay && IsInstanceValid(_target))
        {
            // Aim for Upper Body (Chest/Head) to avoid diving into feet
            Vector3 targetCenter = _target.GlobalPosition + Vector3.Up * 1.5f;
            Vector3 toTarget = (targetCenter - GlobalPosition).Normalized();

            // Adjust velocity toward target
            Vector3 currentVel = LinearVelocity;
            float speed = currentVel.Length();

            // Accelerate missiles over time so they can catch runners
            if (speed < 25.0f) speed += (float)delta * 15.0f;

            // Gently steer the velocity vector
            Vector3 desiredVel = toTarget * speed;
            LinearVelocity = currentVel.Lerp(desiredVel, _homingStrength * (float)delta * 0.1f);

            // Maintain minimum speed
            if (speed < 15.0f) LinearVelocity = LinearVelocity.Normalized() * 15.0f;
        }

        // Point toward velocity
        if (LinearVelocity.LengthSquared() > 0.1f)
        {
            LookAt(GlobalPosition + LinearVelocity, Vector3.Up);
        }
    }

    private Node3D FindNearbyEnemy(Vector3 center, float range)
    {
        var targets = TargetingHelper.GetSortedTargets(_caster as PlayerController, _team, false, range);
        if (targets.Count > 0) return targets[0];
        return null;
    }

    private void OnBodyEntered(Node body)
    {
        if (_hasHit) return;

        // Ignore caster and allies
        if (body == _caster) return;
        if (body is PlayerController pc && !TeamSystem.AreEnemies(_team, pc.Team)) return;
        if (body is MobaMinion minion && !TeamSystem.AreEnemies(_team, minion.Team)) return;

        _hasHit = true;

        // Apply damage
        if (body is InteractableObject io)
        {
            io.OnHit(_damage, GlobalPosition, LinearVelocity.Normalized(), _caster);
        }
        else if (body is MobaTower tower)
        {
            tower.TakeDamage(_damage);
        }
        else if (body is MobaNexus nexus)
        {
            nexus.TakeDamage(_damage);
        }

        SpawnExplosion();
        QueueFree();
    }

    private void StopAndCleanup()
    {
        _hasHit = true;
        QueueFree();
    }

    private void SpawnExplosion()
    {
        // Simple purple blast (using existing Shockwave type logic but smaller)
        var scene = GD.Load<PackedScene>("res://Scenes/VFX/Shockwave.tscn");
        if (scene != null)
        {
            var wave = scene.Instantiate<Node3D>();
            GetTree().CurrentScene.AddChild(wave);
            wave.GlobalPosition = GlobalPosition;
            wave.Scale = Vector3.One * 0.5f; // Small blast
            if (wave is Shockwave sw)
            {
                sw.SetColor(new Color(0.7f, 0.1f, 1.0f)); // Purple
            }
        }
    }
}
