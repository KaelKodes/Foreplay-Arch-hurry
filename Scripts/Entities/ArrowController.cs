using Godot;
using System;

namespace Archery;

public partial class ArrowController : RigidBody3D
{
	[Signal] public delegate void ArrowSettledEventHandler(float distance);
	[Signal] public delegate void ArrowCarriedEventHandler(float distance);
	[Signal] public delegate void ArrowSpeedUpdatedEventHandler(float speed);
	[Signal] public delegate void ArrowCollectedEventHandler();

	public bool HasBeenShot { get; private set; } = false;
	public bool IsCollectible { get; private set; } = false;
	[Export] public MobaTeam Team = MobaTeam.None;

	private Vector3 _windVelocity = Vector3.Zero;
	private Vector3 _startPosition;
	private bool _isFlying = false;
	private bool _isStuck = false;
	private Vector3 _pendingVelocity = Vector3.Zero;
	private Vector3 _spin = Vector3.Zero;
	private CollisionObject3D _playerException;
	private float _launchGraceTimer = 0.0f;
	private Color _syncedColor = Colors.White;
	private bool _colorSet = false;
	private float _maxSpeed = 0.0f;

	public override void _Ready()
	{
		ContactMonitor = true;
		MaxContactsReported = 10;
		ContinuousCd = true; // Enable Continuous Collision Detection
		LinearDampMode = DampMode.Replace;
		LinearDamp = 0.0f;
		AngularDampMode = DampMode.Replace;
		AngularDamp = 0.0f;
		BodyEntered += OnBodyEntered;
		AddToGroup("arrows");

		GD.Print($"ArrowController: Ready. Name: {Name}, Authority: {GetMultiplayerAuthority()}, Peer: {Multiplayer.GetUniqueId()}, Children: {GetChildCount()}");

		// If color was set before Ready (via Spawn data), re-apply it now that children are definitely here
		if (_colorSet)
		{
			SetColor(_syncedColor);
		}

		foreach (var child in GetChildren())
		{
			GD.Print($"  - Child: {child.Name} ({child.GetType().Name})");
		}
	}

	/// <summary>
	/// Called by Server to set initial pose on all clients. Replaces MultiplayerSynchronizer.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void SetInitialPose(Vector3 position, Vector3 rotation)
	{
		GlobalPosition = position;
		GlobalRotation = rotation;
		Freeze = true; // Ensure frozen until launched
		GD.Print($"Arrow: SetInitialPose at {position}");
	}

	/// <summary>
	/// Called by Server to set arrow color on all clients.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void SetOwnerColor(float r, float g, float b)
	{
		GD.Print($"Arrow {Name}: SetOwnerColor RPC received. Color: ({r:F2},{g:F2},{b:F2})");
		SetColor(new Color(r, g, b));
	}

	/// <summary>
	/// Helper method that can be called via CallDeferred to broadcast color.
	/// This exists because CallDeferred("Rpc", ...) doesn't work in Godot.
    /// </summary>
    public void BroadcastColor(float r, float g, float b)
    {
        Rpc(nameof(SetOwnerColor), r, g, b);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (_pendingVelocity != Vector3.Zero)
        {
            state.LinearVelocity = _pendingVelocity;
            _pendingVelocity = Vector3.Zero;
            _launchGraceTimer = 0.1f; // 100ms grace period
            GD.Print($"Arrow: Launch Velocity Applied to state: {state.LinearVelocity}");
        }

        if (_launchGraceTimer > 0) _launchGraceTimer -= (float)state.Step;

        if (!_isFlying || _isStuck) return;

        // 1. Primitive Drag
        Vector3 velocity = state.LinearVelocity;
        float speed = velocity.Length();
        if (speed > 0.1f)
        {
            Vector3 dragForce = -velocity.Normalized() * speed * speed * ArcheryConstants.DRAG_COEFFICIENT;
            state.ApplyCentralForce(dragForce);

            // 2. Point towards velocity (Arrow orientation)
            if (speed > 1.0f)
            {
                LookAt(GlobalPosition + velocity, Vector3.Up);
            }
        }

        // 3. Wind influence
        state.ApplyCentralForce(_windVelocity * 0.02f);

        // 4. Emit flight progress (Carried distance)
        float currentDist = _startPosition.DistanceTo(GlobalPosition);
        EmitSignal(SignalName.ArrowCarried, currentDist);

        // 5. Track Max Speed
        if (speed > _maxSpeed)
        {
            _maxSpeed = speed;
            EmitSignal(SignalName.ArrowSpeedUpdated, _maxSpeed);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    public void Launch(Vector3 startPosition, Vector3 startRotation, Vector3 velocity, Vector3 spin)
    {
		// Set position/rotation first (critical for remote clients who haven't seen this arrow move)
		GlobalPosition = startPosition;
		GlobalRotation = startRotation;

		HasBeenShot = true;
		GD.Print($"Arrow: Launch() called from {GlobalPosition} with velocity: {velocity}");
		_isFlying = true;
		_isStuck = false;
		Freeze = false;
		Sleeping = false;
		_startPosition = GlobalPosition;
		_pendingVelocity = velocity; // Apply in next IntegrateForces
		_spin = spin;
		_maxSpeed = 0.0f;

		// Force wake the physics engine
		ApplyCentralImpulse(Vector3.Zero);

		var trail = GetNodeOrNull<GpuParticles3D>("Trail");
		if (trail != null) trail.Emitting = true;
	}

	public void SetCollisionException(CollisionObject3D other)
	{
		if (other != null)
		{
			AddCollisionExceptionWith(other);
			_playerException = other; // Track it
			GD.Print($"Arrow: Added collision exception for {other.Name}");
		}
	}

	private void OnBodyEntered(Node body)
	{
		// Collection Logic
		if (IsCollectible)
		{
			if (body == _playerException || (body is CharacterBody3D && body.Name.ToString().Contains("Player")))
			{
				GD.Print("Arrow: Collected by player!");
				EmitSignal(SignalName.ArrowCollected);
				// Networked Destruction
				if (Multiplayer.MultiplayerPeer != null)
				{
					if (Multiplayer.IsServer()) RequestDestroyArrow();
					else RpcId(1, nameof(RequestDestroyArrow));
				}
				else
				{
					QueueFree();
				}
				return;
			}
		}

		if (!_isFlying || _isStuck) return;

		// Ignore the player's collision if we hit them accidentally on spawn (Flight Phase)
        // OR if we are still in the launch grace period
        if (body is PlayerController || body == _playerException || _launchGraceTimer > 0)
        {
            return;
        }

        GD.Print($"Arrow: Hit {body.Name} (Type: {body.GetType().Name}) at {GlobalPosition}, Velocity: {LinearVelocity.Length():F1} m/s");

        // Combat Logic: Check if we hit an interactable that can take damage
        if (body is InteractableObject interactable)
        {
            // Check team affiliation
            MobaTeam otherTeam = MobaTeam.None;
            if (body is MobaMinion minion) otherTeam = minion.Team;
            else if (body is MobaTower tower) otherTeam = tower.Team;
            else if (body is MobaNexus nexus) otherTeam = nexus.Team;
            else if (body is PlayerController pc) otherTeam = pc.Team;

            if (TeamSystem.AreEnemies(Team, otherTeam) || Team == MobaTeam.None)
            {
                float damage = LinearVelocity.Length() * 0.5f; // damage scales with speed
                interactable.OnHit(damage, GlobalPosition, LinearVelocity.Normalized(), _playerException);
            }
            else
            {
                GD.Print($"Arrow: Friendly fire ignored on {body.Name} (Team: {otherTeam})");
            }
        }
        else if (body is MobaTower tower)
        {
            if (TeamSystem.AreEnemies(Team, tower.Team) || Team == MobaTeam.None)
            {
                float damage = LinearVelocity.Length() * 0.5f;
                tower.TakeDamage(damage);
            }
        }
        else if (body is MobaNexus nexus)
        {
            if (TeamSystem.AreEnemies(Team, nexus.Team) || Team == MobaTeam.None)
            {
                float damage = LinearVelocity.Length() * 0.5f;
                nexus.TakeDamage(damage);
            }
        }
        else if (body.GetParent() is InteractableObject parentInteractable)
        {
            // Fallback for hitboxes that are children of the InteractableObject
            float damage = LinearVelocity.Length() * 0.5f;
            parentInteractable.OnHit(damage, GlobalPosition, LinearVelocity.Normalized(), _playerException);
        }

        StickToTarget(body);
    }

    private void StickToTarget(Node target)
    {
        _isStuck = true;
        _isFlying = false;

        // No picking up arrows in MOBA mode
        if (MobaGameManager.Instance == null)
        {
            IsCollectible = true; // Enable collection
        }


        // Pull back slightly to prevent penetration visual
        // Since we just entered, the arrow center is likely inside.
        // Re-positioning to roughly where the tip should be relative to current velocity
        Vector3 backDir = -LinearVelocity.Normalized();
        if (backDir.LengthSquared() > 0.1f)
        {
            GlobalPosition += backDir * 0.2f; // Offset 20cm back
        }

        // Stop physics
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Freeze = true;

        // Re-enable player collision so they can collect it
        if (_playerException != null)
        {
            RemoveCollisionExceptionWith(_playerException);
        }

        float dist = _startPosition.DistanceTo(GlobalPosition);
        GD.Print($"Arrow: Stuck to {target.Name}. Total Distance: {dist:F2}");
        EmitSignal(SignalName.ArrowSettled, dist);

        var trail = GetNodeOrNull<GpuParticles3D>("Trail");
        if (trail != null) trail.Emitting = false;

        // Cleanup timer for MOBA mode: Dissolve/Remove arrow after 2s of being idle
        if (MobaGameManager.Instance != null)
        {
            GD.Print($"Arrow {Name}: MOBA mode detected. Scheduling destruction in 2s.");
            var timer = GetTree().CreateTimer(2.0f);
            timer.Timeout += () =>
            {
                if (IsInstanceValid(this))
                {
                    // Call networked destruction
                    if (Multiplayer.IsServer()) RequestDestroyArrow();
                    else RpcId(1, nameof(RequestDestroyArrow));
                }
            };
        }
    }

    public void SetWind(Vector3 wind) => _windVelocity = wind;

    public void PrepareNextShot()
    {
        _isFlying = false;
        _isStuck = false;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
    }

    public void SetColor(Color color)
    {
        _syncedColor = color;
        _colorSet = true;
        // Apply to Shaft (Main body)
        var shaft = GetNodeOrNull<MeshInstance3D>("Shaft");
        if (shaft != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color.Darkened(0.2f); // Slightly darker for shaft
            mat.EmissionEnabled = true;
            mat.Emission = color.Darkened(0.5f);
            mat.EmissionEnergyMultiplier = 0.5f;
            shaft.MaterialOverride = mat;
        }

        // Apply to Tip (Bright Glow)
        var tip = GetNodeOrNull<MeshInstance3D>("Tip");
        if (tip != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 5.0f; // Very bright tip
            tip.MaterialOverride = mat;
        }

        // Apply to Fletching (Wings)
        var fletching = GetNodeOrNull<MeshInstance3D>("Fletching");
        if (fletching != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color.Lightened(0.3f);
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Double sided
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 1.0f;
            fletching.MaterialOverride = mat;
        }

        // Apply to Trail (Particles)
        var trail = GetNodeOrNull<GpuParticles3D>("Trail");
        if (trail != null)
        {
            // We need to clone the process material to set unique color
            if (trail.ProcessMaterial is ParticleProcessMaterial ppm)
            {
                var newPpm = (ParticleProcessMaterial)ppm.Duplicate();
                newPpm.Color = color;
                newPpm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point; // Ensure point emission
                trail.ProcessMaterial = newPpm;

                // Create a matching draw pass material for the ribbon
                if (trail.DrawPass1 is QuadMesh qm && qm.Material is StandardMaterial3D sm)
                {
                    var newSm = (StandardMaterial3D)sm.Duplicate();
                    newSm.AlbedoColor = color;
                    newSm.Emission = color;
                    trail.DrawPass1 = (Mesh)trail.DrawPass1.Duplicate();
                    ((QuadMesh)trail.DrawPass1).Material = newSm;
                }
                else if (trail.DrawPass1 is RibbonTrailMesh rtm && rtm.Material is StandardMaterial3D rsm)
                {
                    // Handle RibbonTrailMesh similarly
                    var newSm = (StandardMaterial3D)rsm.Duplicate();
                    // Keep transparency!
                    newSm.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    newSm.AlbedoColor = new Color(color.R, color.G, color.B, 0.5f);
                    newSm.Emission = color;
                    newSm.VertexColorUseAsAlbedo = true; // IMPORTANT for Gradient fade

                    trail.DrawPass1 = (Mesh)trail.DrawPass1.Duplicate();
                    ((RibbonTrailMesh)trail.DrawPass1).Material = newSm;
                }
            }
        }
    }

    public void Reset()
    {
        PrepareNextShot();
        GlobalPosition = _startPosition; // Or a designated spawn point
    }

    public string GetInteractionPrompt() => IsCollectible ? "Collect Arrow" : "";

    public void OnInteract(PlayerController player)
    {
        if (IsCollectible)
        {
            GD.Print("Arrow: Collected by interaction!");
            EmitSignal(SignalName.ArrowCollected);
            // Networked Destruction
            if (Multiplayer.MultiplayerPeer != null)
            {
                if (Multiplayer.IsServer()) RequestDestroyArrow();
                else RpcId(1, nameof(RequestDestroyArrow));
            }
            else
            {
                QueueFree();
            }
        }
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RequestDestroyArrow()
    {
        if (!Multiplayer.IsServer()) return;
        QueueFree();
    }
}
