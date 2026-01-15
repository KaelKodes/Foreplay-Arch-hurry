using Godot;
using System;

namespace Archery
{
	public partial class ArrowController : RigidBody3D
	{
		[Signal] public delegate void ArrowSettledEventHandler(float distance);
		[Signal] public delegate void ArrowCarriedEventHandler(float distance);
		[Signal] public delegate void ArrowCollectedEventHandler();

		public bool HasBeenShot { get; private set; } = false;
		public bool IsCollectible { get; private set; } = false;

		private Vector3 _windVelocity = Vector3.Zero;
		private Vector3 _startPosition;
		private bool _isFlying = false;
		private bool _isStuck = false;
		private Vector3 _pendingVelocity = Vector3.Zero;
		private Vector3 _spin = Vector3.Zero;
		private CollisionObject3D _playerException;
		private float _launchGraceTimer = 0.0f;

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

			GD.Print($"ArrowController: Ready. Name: {Name}, Authority: {GetMultiplayerAuthority()}, Peer: {Multiplayer.GetUniqueId()}");
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
			SetColor(new Color(r, g, b));
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

            // Force wake the physics engine
            ApplyCentralImpulse(Vector3.Zero);
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
                        RpcId(1, nameof(RequestDestroyArrow));
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
			StickToTarget(body);
		}

		private void StickToTarget(Node target)
		{
			_isStuck = true;
			_isFlying = false;
			IsCollectible = true; // Enable collection

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
			var mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
			if (mesh != null)
			{
				var mat = new StandardMaterial3D();
				mat.AlbedoColor = color;
				mat.EmissionEnabled = true;
				mat.Emission = color;
				mat.EmissionEnergyMultiplier = 2.0f;
				mesh.MaterialOverride = mat;
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
					RpcId(1, nameof(RequestDestroyArrow));
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
}
