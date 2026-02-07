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

    private Monsters _monster;
    private Vector3 _moveDirection = Vector3.Forward;
    private float _stateTimer = 0f;
    private bool _isWalking = false;
    private RandomNumberGenerator _rng = new();

    // Throttle AI updates for performance
    private float _aiUpdateInterval = 0.1f;
    private float _aiUpdateTimer = 0f;

    public override void _Ready()
    {
        _rng.Randomize();

        // Get parent - must be a Monsters instance
        _monster = GetParent() as Monsters;

        if (_monster == null)
        {
            GD.PrintErr("[MonsterAI] Must be child of Monsters node!");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }

        // Start with random delay so all monsters don't sync up
        _stateTimer = _rng.RandfRange(0, IdleDuration);
        _isWalking = false;

        // Pick initial random facing
        float angle = _rng.RandfRange(0, Mathf.Tau);
        _moveDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));

        GD.Print($"[MonsterAI] Initialized for {_monster.ObjectName}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!EnableAI || _monster == null) return;

        float dt = (float)delta;
        _stateTimer -= dt;
        _aiUpdateTimer -= dt;

        // State transitions
        if (_stateTimer <= 0)
        {
            ToggleState();
        }

        // Movement and obstacle checks (throttled)
        if (_isWalking)
        {
            if (_aiUpdateTimer <= 0)
            {
                _aiUpdateTimer = _aiUpdateInterval;
                CheckObstacles();
            }

            Move(dt);
        }
    }

    private void ToggleState()
    {
        _isWalking = !_isWalking;

        if (_isWalking)
        {
            // Start walking
            _stateTimer = _rng.RandfRange(WanderDuration * 0.5f, WanderDuration * 1.5f);
            PickNewDirection();
            _monster.SetAnimation("Walk");
        }
        else
        {
            // Start idling
            _stateTimer = _rng.RandfRange(IdleDuration * 0.5f, IdleDuration * 1.5f);
            _monster.SetAnimation("Idle");
        }
    }

    private void PickNewDirection()
    {
        // Random angle between -90 and +90 degrees from current facing
        float angleOffset = _rng.RandfRange(-Mathf.Pi * 0.5f, Mathf.Pi * 0.5f);
        float currentAngle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z);
        float newAngle = currentAngle + angleOffset;
        _moveDirection = new Vector3(Mathf.Sin(newAngle), 0, Mathf.Cos(newAngle)).Normalized();
    }

    private void CheckObstacles()
    {
        var spaceState = _monster.GetWorld3D().DirectSpaceState;
        var origin = _monster.GlobalPosition + Vector3.Up * 0.5f;

        // Check forward
        var query = PhysicsRayQueryParameters3D.Create(
            origin,
            origin + _moveDirection * ObstacleCheckDistance
        );
        query.CollisionMask = 1 | 2; // World geometry

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            // Hit something - turn away
            var hitNormal = (Vector3)result["normal"];
            _moveDirection = hitNormal.Slide(Vector3.Up).Normalized();

            // Add some randomness to avoid getting stuck
            float jitter = _rng.RandfRange(-0.3f, 0.3f);
            float angle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z) + jitter;
            _moveDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)).Normalized();
        }

        // Check for ground ahead (prevent falling off edges)
        var groundQuery = PhysicsRayQueryParameters3D.Create(
            origin + _moveDirection * 1.0f,
            origin + _moveDirection * 1.0f + Vector3.Down * 3.0f
        );
        groundQuery.CollisionMask = 2; // Terrain

        var groundResult = spaceState.IntersectRay(groundQuery);
        if (groundResult.Count == 0)
        {
            // No ground ahead - turn around
            _moveDirection = -_moveDirection;
        }
    }

    private void Move(float delta)
    {
        // Apply movement through Monsters wrapper
        Vector3 velocity = new Vector3(
            _moveDirection.X * MoveSpeed,
            -9.8f * delta, // Gravity
            _moveDirection.Z * MoveSpeed
        );

        _monster.ApplyMovement(velocity, delta);

        // Rotate to face movement direction
        if (_moveDirection.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.X, _moveDirection.Z);
            float currentAngle = _monster.Rotation.Y;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, TurnSpeed * delta);
            _monster.Rotation = new Vector3(0, newAngle, 0);
        }
    }
}
