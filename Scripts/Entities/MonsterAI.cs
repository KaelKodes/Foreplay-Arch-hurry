using Godot;

namespace Archery;

/// <summary>
/// Simple wandering AI for monsters.
/// Works with Monsters class which wraps CharacterBody3D functionality.
/// </summary>
public partial class MonsterAI : Node
{
    [Export] public float MoveSpeed = 2.0f;
    [Export] public float WanderDuration = 3.0f;
    [Export] public float IdleDuration = 2.0f;
    [Export] public float ObstacleCheckDistance = 1.5f;
    [Export] public float TurnSpeed = 3.0f;
    [Export] public bool EnableAI = true;

    [Export] public float DetectionRange = 8.0f;
    [Export] public float AttackRange = 2.5f;
    [Export] public float CallForHelpRange = 10.0f;
    [Export] public float AttackCooldown = 1.5f;

    private Monsters _monster;
    private Vector3 _moveDirection = Vector3.Forward;
    private float _stateTimer = 0f;
    private bool _isWalking = false;
    private RandomNumberGenerator _rng = new();

    private Node3D _combatTarget = null;
    private float _attackTimer = 0f;
    private bool _isHelping = false; // Constraint: can't CoH if helping

    // Throttle AI updates for performance
    private float _aiUpdateInterval = 0.1f;
    private float _aiUpdateTimer = 0f;

    public override void _Ready()
    {
        _rng.Randomize();
        _monster = GetParent() as Monsters;

        if (_monster == null)
        {
            GD.PrintErr("[MonsterAI] Must be child of Monsters node!");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }

        AddToGroup("monster_ai");
        _stateTimer = _rng.RandfRange(0, this.IdleDuration);
        _isWalking = false;

        float angle = _rng.RandfRange(0, Mathf.Tau);
        _moveDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!EnableAI || _monster == null) return;
        if (_monster.IsStunned) return;

        float dt = (float)delta;
        _stateTimer -= dt;
        _aiUpdateTimer -= dt;
        if (_attackTimer > 0) _attackTimer -= dt;

        UpdateCombatState(dt);

        if (_combatTarget != null)
        {
            ChaseTarget(dt);
        }
        else
        {
            UpdateWanderState(dt);
        }
    }

    private void UpdateCombatState(float dt)
    {
        // 1. Check for Taunt
        if (_monster.IsTaunted && _monster.TauntTarget != null)
        {
            _combatTarget = _monster.TauntTarget;
            return;
        }

        // 2. Proximity Detection (if not already attacking)
        if (_aiUpdateTimer <= 0 && _combatTarget == null)
        {
            _aiUpdateTimer = _aiUpdateInterval;
            DetectPlayers();
        }

        // 3. Clear target if too far
        if (_combatTarget != null && _combatTarget.GlobalPosition.DistanceTo(_monster.GlobalPosition) > DetectionRange * 1.5f)
        {
            _combatTarget = null;
            _isHelping = false;
        }
    }

    private void DetectPlayers()
    {
        var players = GetTree().GetNodesInGroup("player");
        foreach (var node in players)
        {
            if (node is PlayerController p && p.GlobalPosition.DistanceTo(_monster.GlobalPosition) < DetectionRange)
            {
                // FACTION CHECK: Only aggro enemies
                if (TeamSystem.AreEnemies(_monster.Team, p.Team))
                {
                    _combatTarget = p;
                    TriggerCallForHelp();
                    break;
                }
            }
        }

        // Also detect minions if no player target found
        if (_combatTarget == null)
        {
            var minions = GetTree().GetNodesInGroup("minions");
            foreach (var node in minions)
            {
                if (node is MobaMinion minion && minion.Health > 0 && minion.GlobalPosition.DistanceTo(_monster.GlobalPosition) < DetectionRange)
                {
                    if (TeamSystem.AreEnemies(_monster.Team, minion.Team))
                    {
                        _combatTarget = minion;
                        TriggerCallForHelp();
                        break;
                    }
                }
            }
        }
    }

    private void TriggerCallForHelp()
    {
        if (_isHelping) return; // Don't call if we're already helping someone else

        GD.Print($"[MonsterAI] {_monster.Species} calling for help!");

        // Find nearby monsters and tell them to help
        var neighbors = GetTree().GetNodesInGroup("monster_ai");
        foreach (var node in neighbors)
        {
            if (node is MonsterAI other && other != this && !other._isHelping && other._combatTarget == null)
            {
                if (other._monster.GlobalPosition.DistanceTo(_monster.GlobalPosition) < CallForHelpRange)
                {
                    other.RespondToHelp(_combatTarget);
                }
            }
        }
    }

    public void RespondToHelp(Node3D target)
    {
        if (_isHelping || _combatTarget != null) return;

        _combatTarget = target;
        _isHelping = true;

        // Reset state so we start chasing immediately
        _stateTimer = 0;
        _isWalking = true;

        GD.Print($"[MonsterAI] {_monster.Species} responding to help call against {target.Name}");
    }

    private void ChaseTarget(float dt)
    {
        Vector3 diff = _combatTarget.GlobalPosition - _monster.GlobalPosition;
        float dist = diff.Length();
        diff.Y = 0;

        if (dist > AttackRange)
        {
            _moveDirection = diff.Normalized();
            Move(dt, MoveSpeed * 1.2f);
            _monster.SetAnimation("Run");
        }
        else
        {
            // Attack!
            if (_attackTimer <= 0)
            {
                PerformAttack();
            }
            _monster.SetAnimation("Idle");
        }
    }

    private void PerformAttack()
    {
        _attackTimer = AttackCooldown;
        _monster.SetAnimation("Attack");

        // Apply damage to player if they have an OnHit method
        if (_combatTarget is PlayerController pc)
        {
            pc.OnHit(10f, _combatTarget.GlobalPosition, Vector3.Up, _monster);
        }
        else if (_combatTarget is InteractableObject io)
        {
            io.OnHit(10f, _combatTarget.GlobalPosition, Vector3.Up, _monster);
        }

        GD.Print($"[MonsterAI] {_monster.Species} attacked {_combatTarget.Name}");
    }

    private void UpdateWanderState(float dt)
    {
        if (_stateTimer <= 0)
        {
            ToggleState();
        }

        if (_isWalking)
        {
            if (_aiUpdateTimer <= 0)
            {
                _aiUpdateTimer = _aiUpdateInterval;
                CheckObstacles();
            }
            Move(dt, MoveSpeed);
        }
    }

    private void ToggleState()
    {
        _isWalking = !_isWalking;
        _stateTimer = _isWalking ? _rng.RandfRange(WanderDuration * 0.5f, WanderDuration * 1.5f) : _rng.RandfRange(IdleDuration * 0.5f, IdleDuration * 1.5f);

        if (_isWalking)
        {
            PickNewDirection();
            _monster.SetAnimation("Walk");
        }
        else
        {
            _monster.SetAnimation("Idle");
        }
    }

    private void PickNewDirection()
    {
        float angleOffset = _rng.RandfRange(-Mathf.Pi * 0.5f, Mathf.Pi * 0.5f);
        float currentAngle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z);
        float newAngle = currentAngle + angleOffset;
        _moveDirection = new Vector3(Mathf.Sin(newAngle), 0, Mathf.Cos(newAngle)).Normalized();
    }

    private void CheckObstacles()
    {
        var spaceState = _monster.GetWorld3D().DirectSpaceState;
        var origin = _monster.GlobalPosition + Vector3.Up * 0.5f;

        var query = PhysicsRayQueryParameters3D.Create(origin, origin + _moveDirection * ObstacleCheckDistance);
        query.CollisionMask = 1 | 2;
        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var hitNormal = (Vector3)result["normal"];
            _moveDirection = hitNormal.Slide(Vector3.Up).Normalized();
            float jitter = _rng.RandfRange(-0.3f, 0.3f);
            float angle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z) + jitter;
            _moveDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)).Normalized();
        }

        var groundQuery = PhysicsRayQueryParameters3D.Create(origin + _moveDirection * 1.0f, origin + _moveDirection * 1.0f + Vector3.Down * 3.0f);
        groundQuery.CollisionMask = 2;
        var groundResult = spaceState.IntersectRay(groundQuery);
        if (groundResult.Count == 0) _moveDirection = -_moveDirection;
    }

    private void Move(float delta, float speed)
    {
        Vector3 velocity = new Vector3(_moveDirection.X * speed, -9.8f * delta, _moveDirection.Z * speed);
        _monster.ApplyMovement(velocity, delta);

        if (_moveDirection.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z);
            float currentAngle = _monster.Rotation.Y;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * delta);
            _monster.Rotation = new Vector3(0, newAngle, 0);
        }
    }
}
