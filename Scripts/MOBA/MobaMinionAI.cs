using Godot;

namespace Archery;

/// <summary>
/// Lane-pushing AI for MOBA minions.
/// Priority: Enemy Minions > Towers > Nexus.
/// Moves along Z-axis toward enemy base when no target is in range.
/// </summary>
public partial class MobaMinionAI : Node
{
    [Export] public float AggroRange = 25f;
    [Export] public float TurnSpeed = 5f;

    private MobaMinion _minion;
    private Node3D _currentTarget;
    private float _attackTimer = 0f;

    // Direction toward enemy base: Blue pushes -Z, Red pushes +Z
    private float _laneDirection = -1f;

    public override void _Ready()
    {
        _minion = GetParent() as MobaMinion;
        if (_minion == null)
        {
            GD.PrintErr("[MobaMinionAI] Must be child of MobaMinion!");
            SetPhysicsProcess(false);
            return;
        }

        // Blue team pushes toward -Z (Red base), Red pushes toward +Z (Blue base)
        _laneDirection = _minion.Team == MobaTeam.Blue ? -1f : 1f;

        GD.Print($"[MobaMinionAI] Ready - Minion: {_minion.Name}, Team: {_minion.Team}, Direction: {_laneDirection}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_minion == null || _minion.Health <= 0) return;

        float dt = (float)delta;
        _attackTimer -= dt;

        // Find or validate target
        bool needsRescan = _currentTarget == null || !IsValidTarget(_currentTarget);
        
        // If targeting a structure, check if a minion is now in range
        if (!needsRescan && (_currentTarget is MobaTower || _currentTarget is MobaNexus))
        {
            Node3D potentialMinion = FindBestTarget();
            if (potentialMinion != null && potentialMinion is MobaMinion)
            {
                _currentTarget = potentialMinion;
            }
        }
        else if (needsRescan)
        {
            _currentTarget = FindBestTarget();
        }

        if (_currentTarget != null)
        {
            float dist = _minion.GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

            if (dist <= _minion.AttackRange)
            {
                // In range: stop and attack
                FaceTarget(_currentTarget, dt);
                if (_attackTimer <= 0)
                {
                    Attack();
                    _attackTimer = _minion.AttackCooldown;
                }
                _minion.SetAnimation("Attack");
            }
            else
            {
                // Move toward target
                MoveToward(_currentTarget.GlobalPosition, dt);
                _minion.SetAnimation("Walk");
            }
        }
        else
        {
            // No target: march down the lane
            Vector3 laneTarget = _minion.GlobalPosition + new Vector3(0, 0, _laneDirection * 10f);
            MoveToward(laneTarget, dt);
            _minion.SetAnimation("Walk");
        }
    }

    private Node3D FindBestTarget()
    {
        Node3D bestTarget = null;
        float bestScore = float.MaxValue;

        MobaTeam enemyTeam = TeamSystem.GetEnemyTeam(_minion.Team);
        string enemyGroup = $"team_{enemyTeam.ToString().ToLower()}";

        // ── Search enemy team group ──
        // proven reliable for structures/units
        foreach (var node in GetTree().GetNodesInGroup(enemyGroup))
        {
            if (node == _minion) continue;
            if (node is not Node3D target) continue;
            if (!IsValidTarget(target)) continue;

            float dist = _minion.GlobalPosition.DistanceTo(target.GlobalPosition);
            if (dist > AggroRange) continue;

            // Priority scoring: minions first
            float priorityOffset = 0f;
            if (node is MobaTower) priorityOffset = 100f;
            else if (node is MobaNexus) priorityOffset = 200f;

            float score = dist + priorityOffset;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        // If no enemies in aggro range, look for structures along the lane
        if (bestTarget == null)
        {
            bestTarget = FindNextLaneStructure(enemyTeam);
        }

        return bestTarget;
    }

    /// <summary>
    /// Find the nearest enemy structure along the lane direction.
    /// </summary>
    private Node3D FindNextLaneStructure(MobaTeam enemyTeam)
    {
        Node3D nearest = null;
        float nearestDist = float.MaxValue;

        // Check towers
        foreach (var node in GetTree().GetNodesInGroup("towers"))
        {
            if (node is not MobaTower tower) continue;
            if (tower.Team != enemyTeam) continue;
            if (tower.IsDestroyed) continue;

            float dist = _minion.GlobalPosition.DistanceTo(tower.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = tower;
            }
        }

        // Check nexus
        foreach (var node in GetTree().GetNodesInGroup("nexus"))
        {
            if (node is not MobaNexus nexus) continue;
            if (nexus.Team != enemyTeam) continue;
            if (nexus.Health <= 0) continue;

            float dist = _minion.GlobalPosition.DistanceTo(nexus.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = nexus;
            }
        }

        return nearest;
    }

    private bool IsValidTarget(Node3D target)
    {
        if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return false;
        if (target is MobaMinion minion && minion.Health <= 0) return false;
        if (target is MobaTower tower && tower.IsDestroyed) return false;
        if (target is MobaNexus nexus && nexus.Health <= 0) return false;
        return true;
    }

    private void MoveToward(Vector3 targetPos, float dt)
    {
        Vector3 direction = (targetPos - _minion.GlobalPosition).Normalized();
        direction.Y = 0; // Keep on horizontal plane

        Vector3 velocity = direction * _minion.MoveSpeed;
        _minion.ApplyMovement(velocity, dt);

        // Double check Y-position to prevent drift
        _minion.GlobalPosition = new Vector3(_minion.GlobalPosition.X, 0f, _minion.GlobalPosition.Z);

        // Face movement direction
        if (direction.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.X, direction.Z);
            float currentAngle = _minion.Rotation.Y;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * dt);
            _minion.Rotation = new Vector3(0, newAngle, 0);
        }
    }

    private void FaceTarget(Node3D target, float dt)
    {
        Vector3 dir = (target.GlobalPosition - _minion.GlobalPosition).Normalized();
        dir.Y = 0;
        if (dir.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(dir.X, dir.Z);
            float currentAngle = _minion.Rotation.Y;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * dt);
            _minion.Rotation = new Vector3(0, newAngle, 0);
        }
    }

    private void Attack()
    {
        if (_minion.MinionType == MobaMinionType.Ranged)
        {
            // Spawn a projectile toward the target
            SpawnProjectile();
        }
        else
        {
            // Melee: direct damage
            _minion.PerformAttack(_currentTarget);
        }
    }

    private void SpawnProjectile()
    {
        if (_currentTarget == null) return;

        var projectileScene = GD.Load<PackedScene>("res://Scenes/MOBA/MobaProjectile.tscn");
        if (projectileScene == null)
        {
            // Fallback: deal damage directly if scene missing
            GD.PrintErr("[MobaMinionAI] MobaProjectile.tscn not found, dealing direct damage");
            _minion.PerformAttack(_currentTarget);
            return;
        }

        var projectile = projectileScene.Instantiate<MobaProjectile>();
        projectile.Damage = _minion.AttackDamage;
        projectile.SourceTeam = _minion.Team;
        projectile.Target = _currentTarget;

        // Add to tree FIRST, then set position (avoids "not in tree" error)
        GetTree().CurrentScene.AddChild(projectile);

        // Spawn from the front-top of the minion
        Vector3 forward = -_minion.GlobalTransform.Basis.Z.Normalized();
        projectile.GlobalPosition = _minion.GlobalPosition + Vector3.Up * 1.5f + forward * 0.8f;
        projectile.Initialize();
    }
}
