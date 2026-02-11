using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Specialized AI for skeletons summoned by the Necromancer.
/// Features: 40u leash to caster, stay-and-defend combat, follow behavior, and help response.
/// </summary>
public partial class SummonedSkeletonAI : Node
{
    [Export] public float LeashRadius = 40.0f;
    [Export] public float DetectionRange = 40.0f;
    [Export] public float AggroRange = 15.0f;
    [Export] public float TurnSpeed = 5.0f;

    public PlayerController Caster { get; set; }
    public float Duration { get; set; } = 150.0f; // 2.5 minutes

    private MobaMinion _minion;
    private Node3D _currentTarget;
    private Node3D _forcedTarget;
    private float _forcedTargetTimer = 0f;
    private float _attackTimer = 0f;
    private float _durationTimer = 0f;
    private float _aiUpdateTimer = 0f;
    private const float AiUpdateInterval = 0.1f;
    private Vector3 _cachedSeparation = Vector3.Zero;
    private bool _isAttacked = false;
    private float _attackResetTimer = 0f;

    public override void _Ready()
    {
        _minion = GetParent() as MobaMinion;
        if (_minion == null)
        {
            GD.PrintErr("[SummonedSkeletonAI] Must be child of MobaMinion!");
            SetPhysicsProcess(false);
            return;
        }

        _durationTimer = Duration;
        AddToGroup("monster_ai"); // So it can respond to call for help

        // Ensure it's in the correct team group based on caster
        if (Caster != null)
        {
            _minion.Team = Caster.Team;
            _minion.AddToGroup($"team_{_minion.Team.ToString().ToLower()}");
        }
    }

    public void OnAttacked()
    {
        _isAttacked = true;
        _attackResetTimer = 3.0f; // Stay in "attacked" state for 3 seconds
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_minion == null || _minion.Health <= 0) return;

        float dt = (float)delta;
        _attackTimer -= dt;
        _durationTimer -= dt;
        _attackResetTimer -= dt;

        if (_attackResetTimer <= 0) _isAttacked = false;
        if (_forcedTargetTimer > 0) _forcedTargetTimer -= dt;

        if (_durationTimer <= 0)
        {
            _minion.OnHit(_minion.MaxHealth * 2, _minion.GlobalPosition, Vector3.Up); // Self-destruct
            return;
        }

        // AI Thinking
        _aiUpdateTimer -= dt;
        if (_aiUpdateTimer <= 0)
        {
            _aiUpdateTimer = AiUpdateInterval;
            UpdateTargeting();
            _cachedSeparation = GetSeparationVector();
        }

        ExecuteBehavior(dt);
    }

    public void SetForcedTarget(Node3D target, float duration = 10.0f)
    {
        _forcedTarget = target;
        _forcedTargetTimer = duration;
        _currentTarget = target;
        GD.Print($"[SummonedSkeletonAI] Forced target set to {target?.Name} for {duration}s");
    }

    private void UpdateTargeting()
    {
        if (Caster == null) return;

        // 1. Forced Target Priority
        if (_forcedTargetTimer > 0 && IsValidTarget(_forcedTarget))
        {
            _currentTarget = _forcedTarget;
            return;
        }
        else
        {
            _forcedTarget = null;
            _forcedTargetTimer = 0;
        }

        // 2. Validate current target
        if (_currentTarget != null && !IsValidTarget(_currentTarget))
        {
            _currentTarget = null;
        }

        // 2. Leash Priority: If we have a target but we're too far from caster AND not being attacked
        float distToCaster = _minion.GlobalPosition.DistanceTo(Caster.GlobalPosition);
        bool outsideLeash = distToCaster > LeashRadius;

        if (outsideLeash && !_isAttacked)
        {
            _currentTarget = null; // Prioritize returning to caster
            return;
        }

        // 3. Find new target if we don't have one (or if we're searching while following)
        if (_currentTarget == null)
        {
            _currentTarget = FindBestTarget();
        }
    }

    private void ExecuteBehavior(float dt)
    {
        if (Caster == null) return;

        float distToCaster = _minion.GlobalPosition.DistanceTo(Caster.GlobalPosition);

        if (_currentTarget != null)
        {
            // Combat behavior
            float distToTarget = _minion.GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
            if (distToTarget <= _minion.AttackRange)
            {
                FaceTarget(_currentTarget, dt);
                if (_attackTimer <= 0)
                {
                    _minion.PerformAttack(_currentTarget);
                    _attackTimer = _minion.AttackCooldown;
                }
                _minion.SetAnimation("Attack");
            }
            else
            {
                float speed = _minion.MoveSpeed * 1.2f;
                if (Caster.IsSprinting)
                {
                    speed *= 2.0f; // Match necro running
                    _minion.SetAnimation("Run");
                }
                else
                {
                    _minion.SetAnimation("Walk");
                }
                MoveToward(_currentTarget.GlobalPosition, dt, speed);
            }
        }
        else
        {
            // Follow behavior
            float targetFollowDist = 5.0f;
            if (distToCaster > targetFollowDist)
            {
                float speed = _minion.MoveSpeed;
                if (Caster.IsSprinting)
                {
                    speed *= 2.0f; // Match necro running
                    _minion.SetAnimation("Run");
                }
                else
                {
                    _minion.SetAnimation("Walk");
                }
                MoveToward(Caster.GlobalPosition, dt, speed);
            }
            else
            {
                _minion.SetAnimation("Idle");
            }
        }
    }

    private Node3D FindBestTarget()
    {
        Node3D bestTarget = null;
        float bestDist = DetectionRange;

        MobaTeam enemyTeam = TeamSystem.GetEnemyTeam(_minion.Team);
        string enemyGroup = $"team_{enemyTeam.ToString().ToLower()}";

        foreach (var node in GetTree().GetNodesInGroup(enemyGroup))
        {
            if (node is not Node3D target || !IsValidTarget(target)) continue;

            float distToTarget = _minion.GlobalPosition.DistanceTo(target.GlobalPosition);
            if (distToTarget < bestDist)
            {
                // Ensure target is within caster zone OR we are being attacked by it
                float distToCaster = target.GlobalPosition.DistanceTo(Caster.GlobalPosition);
                if (distToCaster <= LeashRadius || _isAttacked)
                {
                    bestDist = distToTarget;
                    bestTarget = target;
                }
            }
        }
        return bestTarget;
    }

    private bool IsValidTarget(Node3D target)
    {
        if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return false;
        if (target is MobaMinion minion && minion.Health <= 0) return false;
        if (target is Monsters m && m.Health <= 0) return false;

        if (target is PlayerController pc)
        {
            return !IsPlayerDead(pc);
        }

        return true;
    }

    private bool IsPlayerDead(PlayerController pc)
    {
        var archery = pc.GetNodeOrNull<ArcherySystem>("ArcherySystem")
                   ?? pc.FindChild("ArcherySystem", true, false) as ArcherySystem;
        var stats = archery?.GetNodeOrNull<StatsService>("StatsService");
        return stats != null && stats.PlayerStats.CurrentHealth <= 0;
    }

    private void MoveToward(Vector3 pos, float dt, float speed)
    {
        Vector3 dir = (pos - _minion.GlobalPosition).Normalized();
        dir.Y = 0;
        Vector3 moveDir = (dir + _cachedSeparation * 1.5f).Normalized();
        _minion.ApplyMovement(moveDir * speed, dt);

        if (moveDir.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(moveDir.X, moveDir.Z);
            float currentAngle = _minion.Rotation.Y;
            _minion.Rotation = new Vector3(0, Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * dt), 0);
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
            _minion.Rotation = new Vector3(0, Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * dt), 0);
        }
    }

    private Vector3 GetSeparationVector()
    {
        Vector3 separation = Vector3.Zero;
        float radius = 1.5f;
        foreach (var node in GetTree().GetNodesInGroup("minions"))
        {
            if (node is MobaMinion other && other != _minion && other.Health > 0)
            {
                float dist = _minion.GlobalPosition.DistanceTo(other.GlobalPosition);
                if (dist < radius && dist > 0.1f)
                {
                    Vector3 diff = _minion.GlobalPosition - other.GlobalPosition;
                    diff.Y = 0;
                    separation += diff.Normalized() * (1.0f - dist / radius);
                }
            }
        }
        return separation;
    }

    // Response to Call for Help (from MonsterAI.cs pattern)
    public void RespondToHelp(Node3D target)
    {
        if (_currentTarget != null || Caster == null) return;

        // Check if the target is within the necros zone
        if (target.GlobalPosition.DistanceTo(Caster.GlobalPosition) <= LeashRadius)
        {
            _currentTarget = target;
        }
    }
}
