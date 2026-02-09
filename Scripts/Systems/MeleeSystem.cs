using Godot;

namespace Archery;

/// <summary>
/// Melee combat system with 3-click swing bar mechanics.
/// Power determines cooldown: overpower = longer cooldown but bonus damage.
/// </summary>
public partial class MeleeSystem : Node
{
	public static MeleeSystem Instance { get; private set; }

	// --- Swing States ---
	public enum SwingState { Idle, Drawing, Finishing, Executing, Cooling }
	public SwingState CurrentState { get; private set; } = SwingState.Idle;

	// --- Swing Values ---
	public float SwingPower { get; private set; } = 0f;      // 0-100
	public float VisualBarValue { get; private set; } = 0f;  // 0-100 for HUD
	public float LockedPower { get; private set; } = 0f;
	public float CooldownRemaining { get; private set; } = 0f;

	// --- Constants ---
	private const float BaseCooldown = 1.0f;       // Reduced base cooldown
	private const float PerfectPowerLine = 94f;   // The "perfect" power level

	// Slam Timing (Adjust these to match animations)
	public float PerfectSlamDelay = 2.1f; // Triple swing timing
	public float PowerSlamDelay = 1.0f;   // Big slam timing

	// Attack Type Helpers (Source of truth for both Logic and Animations)
	public bool IsTripleSwing => LockedPower >= 199f;
	public bool IsPerfectSlam => LockedPower >= 99f && LockedPower < 199f;
	public bool IsAnySlam => LockedPower >= 99f;
	private const float OverpowerPenaltyPerPoint = 0.1f; // Extra cooldown per % over perfect
	private const float SwingSpeed = 150f;        // Faster bar fill
	private const float ExecuteSpeed = 300f;      // Very fast bar depletion for swing

	private PlayerController _currentPlayer;

	// --- Signals ---
	[Signal] public delegate void ModeChangedEventHandler(bool inMeleeMode);
	[Signal] public delegate void SwingValuesUpdatedEventHandler(float barValue, float power, int state);
	[Signal] public delegate void SwingStartedEventHandler();
	[Signal] public delegate void SwingPeakEventHandler(float power);
	[Signal] public delegate void SwingCompleteEventHandler(float power, float accuracy, float damage);
	[Signal] public delegate void CooldownUpdatedEventHandler(float remaining, float total);
	[Signal] public delegate void PowerSlamTriggeredEventHandler(Vector3 position, int playerIndex);

	public override void _Ready()
	{
		// Removed singleton logic - now per-player instance
	}

	public override void _Process(double delta)
	{
		bool isLocal = _currentPlayer != null && _currentPlayer.IsLocal;

		switch (CurrentState)
		{
			case SwingState.Cooling:
				CooldownRemaining -= (float)delta;
				if (isLocal) EmitSignal(SignalName.CooldownUpdated, CooldownRemaining, BaseCooldown);

				if (CooldownRemaining <= 0)
				{
					CooldownRemaining = 0;
					CurrentState = SwingState.Idle;
					VisualBarValue = 0;
					if (isLocal) EmitSignal(SignalName.SwingValuesUpdated, 0f, 0f, (int)SwingState.Idle);
					else EmitSignal(SignalName.SwingValuesUpdated, 0f, 0f, (int)SwingState.Idle);
				}
				break;

			case SwingState.Drawing:
				// Visual bar update handled by PlayerController calling UpdateChargeProgress
				break;

			case SwingState.Finishing:
				VisualBarValue += SwingSpeed * 1.5f * (float)delta;
				if (VisualBarValue >= 100f)
				{
					VisualBarValue = 100f;
					CurrentState = SwingState.Executing;
					// Trigger peak/strike visuals for ALL clients (authority handles)
					EmitSignal(SignalName.SwingPeak, LockedPower);
				}
				EmitSignal(SignalName.SwingValuesUpdated, VisualBarValue, LockedPower, (int)CurrentState);
				break;

			case SwingState.Executing:
				VisualBarValue -= ExecuteSpeed * (float)delta;
				if (VisualBarValue <= 0f)
				{
					VisualBarValue = 0f;
					CompleteSwing();
				}
				EmitSignal(SignalName.SwingValuesUpdated, VisualBarValue, LockedPower, (int)CurrentState);
				break;
		}
	}

	private void UpdateSwingPower(float delta)
	{
		// This method is no longer used in the 2-click system.
		// Its logic has been integrated into _Process.
	}

	private void UpdateSwingAccuracy(float delta)
	{
		// This method is no longer used in the 2-click system.
		// Accuracy is removed.
	}

	public void RegisterPlayer(PlayerController player)
	{
		_currentPlayer = player;
	}

	public void StartCharge()
	{
		if (CurrentState == SwingState.Cooling || CurrentState == SwingState.Finishing || CurrentState == SwingState.Executing) return;

		if (Multiplayer.IsServer()) Rpc(nameof(NetStartSwing));
		else RpcId(1, nameof(RequestStartSwing));
	}

	public void UpdateChargeProgress(float percent)
	{
		VisualBarValue = percent;
		EmitSignal(SignalName.SwingValuesUpdated, VisualBarValue, 0f, (int)CurrentState);
	}

	public void ExecuteAttack(float holdTime)
	{
		if (CurrentState != SwingState.Drawing) return;

		// Map hold duration to 3 tiers:
		// < 1.5s: 50% power (Weak)
		// 1.5s - 2.5s: 100% power (Perfect)
		// >= 2.5s: 200% power (Overcharge)
		float finalPower = 50f;
		if (holdTime >= 2.5f) finalPower = 200f;
		else if (holdTime >= 1.5f) finalPower = 100f;

		if (Multiplayer.IsServer()) Rpc(nameof(NetExecuteAttack), finalPower);
		else RpcId(1, nameof(RequestExecuteAttack), finalPower);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestExecuteAttack(float power)
	{
		if (!Multiplayer.IsServer()) return;
		Rpc(nameof(NetExecuteAttack), power);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void NetExecuteAttack(float power)
	{
		LockedPower = power;
		CurrentState = SwingState.Finishing;
		GD.Print($"[MeleeSystem] Executing attack with {LockedPower:F1}% power (Hold Time Attack)");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestStartSwing()
	{
		if (!Multiplayer.IsServer()) return;
		Rpc(nameof(NetStartSwing));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void NetStartSwing()
	{
		CurrentState = SwingState.Drawing;
		VisualBarValue = 0f;
		LockedPower = 0f;
		EmitSignal(SignalName.SwingStarted);
		EmitSignal(SignalName.SwingValuesUpdated, 0f, 0f, (int)CurrentState);
		GD.Print("[MeleeSystem] Swing started (2-Click Mode)");
	}


	private void LockAccuracy()
	{
		// This method is no longer used in the 2-click system.
		// Accuracy is removed.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestLockAccuracy(float accuracy)
	{
		// This method is no longer used in the 2-click system.
		// Accuracy is removed.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void NetLockAccuracy(float accuracy)
	{
		// This method is no longer used in the 2-click system.
		// Accuracy is removed.
	}

	private void CompleteSwing()
	{
		float damage = CalculateDamage(LockedPower);
		CooldownRemaining = BaseCooldown;
		CurrentState = SwingState.Cooling;

		EmitSignal(SignalName.SwingComplete, LockedPower, 0f, damage);
		EmitSignal(SignalName.CooldownUpdated, CooldownRemaining, BaseCooldown);

		if (_currentPlayer != null && _currentPlayer.IsLocal)
		{
			// Apply Lifesteal if active
			if (_currentPlayer.GetType().GetProperty("LifestealPercent")?.GetValue(_currentPlayer) is float ls && ls > 0)
			{
				float healAmount = damage * ls;
				if (_currentPlayer.GetType().GetProperty("CurrentHealth") != null)
				{
					var stats = _currentPlayer.GetType().GetProperty("PlayerStats")?.GetValue(_currentPlayer);
					if (stats != null)
					{
						var curHealthProp = stats.GetType().GetProperty("CurrentHealth");
						var maxHealthProp = stats.GetType().GetProperty("MaxHealth");
						if (curHealthProp != null && maxHealthProp != null)
						{
							int cur = (int)curHealthProp.GetValue(stats);
							int max = (int)maxHealthProp.GetValue(stats);
							curHealthProp.SetValue(stats, Mathf.Clamp(cur + (int)healAmount, 0, max));
							GD.Print($"[MeleeSystem] Lifesteal heal: {healAmount:F1}");
						}
					}
				}
			}

			if (IsAnySlam)
			{
				// Delay the slam to match animation timing
				// Perfect (100%) now uses Triple Flurry (2.1s)
				// Triple (200%) now uses Spinning Slash (0.5s)
				float delay = IsTripleSwing ? 0.5f : PerfectSlamDelay;
				float slamDamage = damage * 2.0f; // Extra damage for slams
				if (IsTripleSwing) slamDamage = damage * 3.0f; // Triple damage for triple swing

				Vector3 slamPos = _currentPlayer.GlobalPosition;
				int playerIndex = _currentPlayer.PlayerIndex;

				SceneTreeTimer timer = GetTree().CreateTimer(delay);
				timer.Timeout += () =>
				{
					if (_currentPlayer != null)
					{
						slamPos = _currentPlayer.GlobalPosition; // Update to current position at slam time
						PerformAoEHitCheck(slamDamage, 4.0f, slamPos);
						EmitSignal(SignalName.PowerSlamTriggered, slamPos, playerIndex);
					}
				};
			}
			else
			{
				PerformHitCheck(damage);
			}
		}

		GD.Print($"[MeleeSystem] Swing complete! Damage={damage:F1} (Power: {LockedPower:F1})");
	}

	private void PerformAoEHitCheck(float damage, float radius, Vector3 center)
	{
		if (_currentPlayer == null) return;
		var spaceState = _currentPlayer.GetWorld3D().DirectSpaceState;

		var query = new PhysicsShapeQueryParameters3D();
		query.Shape = new SphereShape3D { Radius = radius };
		query.Transform = new Transform3D(Basis.Identity, center + new Vector3(0, 0.5f, 0)); // Slightly above ground
		query.CollisionMask = 1 | 2;
		query.Exclude = new Godot.Collections.Array<Rid> { _currentPlayer.GetRid() };

		var results = spaceState.IntersectShape(query);
		GD.Print($"[MeleeSystem] AoE Hit Check: Found {results.Count} potential targets in {radius}m radius");

		foreach (var result in results)
		{
			var collider = (Node)result["collider"];
			var interactable = collider as InteractableObject ?? collider.GetParent() as InteractableObject;
			var tower = collider as MobaTower ?? collider.GetParent() as MobaTower;
			var nexus = collider as MobaNexus ?? collider.GetParent() as MobaNexus;

			MobaTeam attackerTeam = _currentPlayer?.Team ?? MobaTeam.None;
			MobaTeam targetTeam = MobaTeam.None;

			if (interactable != null) targetTeam = interactable.Team;
			else if (tower != null) targetTeam = tower.Team;
			else if (nexus != null) targetTeam = nexus.Team;

			if (TeamSystem.AreEnemies(attackerTeam, targetTeam) || targetTeam == MobaTeam.None)
			{
				Vector3 hitPos = ((Node3D)collider).GlobalPosition;
				Vector3 dir = (hitPos - center).Normalized();

				// NEW: Check for specialized body part hitbox
				var monsterPart = collider as MonsterPart ?? collider.GetNodeOrNull<MonsterPart>("MonsterPart");
				if (monsterPart != null)
				{
					monsterPart.OnHit(damage, hitPos, dir, _currentPlayer);
				}
				else if (interactable != null)
				{
					interactable.OnHit(damage, hitPos, dir, _currentPlayer);
				}
				else if (tower != null) tower.TakeDamage(damage);
				else if (nexus != null) nexus.TakeDamage(damage);
			}
		}
	}

	private void PerformHitCheck(float damage)
	{
		if (_currentPlayer == null) return;
		var spaceState = _currentPlayer.GetWorld3D().DirectSpaceState;

		Vector3 forward = -_currentPlayer.GlobalTransform.Basis.Z;
		Vector3 startPos = _currentPlayer.GlobalPosition + new Vector3(0, 1.2f, 0) + (forward * 0.5f);
		Vector3 endPos = startPos + (forward * 2.5f); // Increased reach check

		var query = new PhysicsShapeQueryParameters3D();
		query.Shape = new SphereShape3D { Radius = 1.2f }; // Wider hit area
		query.Transform = new Transform3D(Basis.Identity, startPos);
		query.Motion = endPos - startPos;
		query.CollisionMask = 1 | 2;
		query.Exclude = new Godot.Collections.Array<Rid> { _currentPlayer.GetRid() };

		var results = spaceState.IntersectShape(query);
		foreach (var result in results)
		{
			var collider = (Node)result["collider"];
			var interactable = collider as InteractableObject ?? collider.GetParent() as InteractableObject;
			var tower = collider as MobaTower ?? collider.GetParent() as MobaTower;
			var nexus = collider as MobaNexus ?? collider.GetParent() as MobaNexus;

			MobaTeam attackerTeam = _currentPlayer?.Team ?? MobaTeam.None;
			MobaTeam targetTeam = MobaTeam.None;

			if (interactable != null) targetTeam = interactable.Team;
			else if (tower != null) targetTeam = tower.Team;
			else if (nexus != null) targetTeam = nexus.Team;

			if (TeamSystem.AreEnemies(attackerTeam, targetTeam) || targetTeam == MobaTeam.None)
			{
				Vector3 hitPos = Vector3.Zero;
				if (result.ContainsKey("point"))
				{
					hitPos = (Vector3)result["point"];
				}
				else
				{
					hitPos = ((Node3D)collider).GlobalPosition;
				}

				// NEW: Check for specialized body part hitbox
				var monsterPart = collider as MonsterPart ?? collider.GetNodeOrNull<MonsterPart>("MonsterPart");
				if (monsterPart != null)
				{
					monsterPart.OnHit(damage, hitPos, forward, _currentPlayer);
				}
				else if (interactable != null)
				{
					interactable.OnHit(damage, hitPos, forward, _currentPlayer);
				}
				else if (tower != null) tower.TakeDamage(damage);
				else if (nexus != null) nexus.TakeDamage(damage);
			}
		}
	}

	private float CalculateDamage(float power)
	{
		float baseDamage = 15f;
		float powerMultiplier = 0.5f + (power / 100f); // 0.5x to 1.5x damage
		return baseDamage * powerMultiplier;
	}

	private float CalculateCooldown(float power)
	{
		// This method is no longer used in the 2-click system.
		// Cooldown is fixed to BaseCooldown.
		return BaseCooldown;
	}

	public void EnterMeleeMode()
	{
		GD.Print($"[MeleeSystem] EnterMeleeMode called. Player null? {_currentPlayer == null}");
		if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.CombatMelee;
		CurrentState = SwingState.Idle;
		VisualBarValue = 0f;
		LockedPower = 0f;
		CooldownRemaining = 0f;
		GD.Print("[MeleeSystem] Entered Melee Mode");
		EmitSignal(SignalName.ModeChanged, true);
	}

	public void ExitMeleeMode()
	{
		if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
		CurrentState = SwingState.Idle;
		GD.Print("[MeleeSystem] Exited Melee Mode");
		EmitSignal(SignalName.ModeChanged, false);
	}

	public void CancelSwing()
	{
		if (CurrentState == SwingState.Drawing || CurrentState == SwingState.Finishing)
		{
			CurrentState = SwingState.Idle;
			VisualBarValue = 0f;
			LockedPower = 0f;
			EmitSignal(SignalName.SwingValuesUpdated, 0f, 0f, (int)SwingState.Idle);
			GD.Print("[MeleeSystem] Swing cancelled");
		}
	}
}
