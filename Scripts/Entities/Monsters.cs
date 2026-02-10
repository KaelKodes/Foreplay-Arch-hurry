using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public enum MonsterBodyType
{
	Biped,
	Blob,
	Flying,
	FlyingArms,
	Unknown
}

public partial class Monsters : InteractableObject
{
	[Export] public float Health = 100.0f;
	[Export] public float MaxHealth = 100.0f;
	[Export] public MonsterBodyType BodyTypeOverride = MonsterBodyType.Unknown;
	[Export] public bool AutoSizeCollision = false; // Toggle for automated hitbox sizing

	private string _species = "";
	[Export]
	public string Species
	{
		get => _species;
		set
		{
			_species = value;
			if (IsInsideTree()) UpdateSpeciesVisuals();
		}
	}

	private AnimationPlayer _animPlayer;
	private bool _isDead = false;
	private double _lastShuffleTime = 0;
	private HealthBar3D _healthBar;
	private MonsterAI _ai;
	protected Node _lastAttacker;

	// --- Status Effects ---
	private float _stunTimer = 0f;
	private float _tauntTimer = 0f;
	private float _debuffTimer = 0f;
	private Node3D _tauntTarget = null;
	public Node3D TauntTarget => _tauntTarget;
	public float DamageModifier { get; private set; } = 1.0f; // 1.0 = 100% damage

	public bool IsStunned => _stunTimer > 0;
	public bool IsTaunted => _tauntTimer > 0;

	public override void _Ready()
	{
		// 1. Initial visual/anim setup must happen BEFORE base._Ready()
		// so that InteractableObject's AddDynamicCollision calculates a correct visible AABB.
        _animPlayer = FindAnimationPlayerRecursive(this);
        if (_animPlayer == null) _animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        if (_animPlayer != null && _animPlayer.GetAnimationList().Length == 0)
        {
            var populatedPlayer = FindPopulatedAnimationPlayerRecursive(this);
            if (populatedPlayer != null && populatedPlayer != _animPlayer) _animPlayer = populatedPlayer;
        }

        UpdateSpeciesVisuals();

        // 2. Now call base which handles secondary init
        base._Ready();

        AddToGroup("interactables");
        AddToGroup("monsters");
        AddToGroup("targetables");

        // JOIN TEAM GROUPS: Critical for MobaTower and MobaMinion discovery
        if (Team != MobaTeam.None) AddToGroup($"team_{Team.ToString().ToLower()}");

        IsTargetable = true;

        if (_animPlayer != null)
        {
            string s = Species.ToLower();
            if (s == "zombie") LoadZombieAnimations();
            else if (s == "crawler") LoadCrawlerAnimations();
            else if (s == "skeleton" || s == "lich" || s == "conjurer") AliasEmbeddedAnimations();

            if (_animPlayer.GetAnimationList().Length > 0) PlayAnimationRobust("Idle");
        }

        _ai = GetNodeOrNull<MonsterAI>("MonsterAI") ?? new MonsterAI { Name = "MonsterAI" };
        if (_ai.GetParent() == null) AddChild(_ai);
    }

    public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal, Node attacker = null)
    {
        if (_isDead) return;
        _lastAttacker = attacker;

        if (Multiplayer.IsServer()) Rpc(nameof(NetOnHit), damage, hitPosition, hitNormal);
        ProcessHit(damage, hitPosition, hitNormal);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void NetOnHit(float damage, Vector3 hitPosition, Vector3 hitNormal)
    {
        if (_isDead || Multiplayer.IsServer()) return;
        ProcessHit(damage, hitPosition, hitNormal);
    }

    private void ProcessHit(float damage, Vector3 hitPosition, Vector3 hitNormal)
    {
        float finalDamage = damage * DamageModifier;
        Health -= finalDamage;
        SpawnDamageNumber(finalDamage, hitPosition);
        UpdateHealthBar();

        if (_lastAttacker is PlayerController pc)
        {
            pc.RegisterDealtDamage(finalDamage);
        }

        if (Health <= 0) Die();
        else PlayHitReaction();
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (_isDead) return;

        // Simple knockback - move the monster slightly or apply to CharacterBody3D velocity if it had a more active controller
		// For now, we'll nudge the position directly as these monsters use simple AI movement
		Vector3 knockback = direction.Normalized() * force;
		knockback.Y = 0; // Keep on ground

		GlobalPosition += knockback;

		GD.Print($"[Monsters] {Species} knocked back by {force} units.");

		// If stunned, maybe increase the nudge
		if (IsStunned) GlobalPosition += knockback * 0.5f;
	}

	private void PlayHitReaction()
	{
		PlayAnimationRobust("Hit");
		if (_animPlayer != null && !_animPlayer.HasAnimation("Hit"))
		{
			_animPlayer.SpeedScale = 2.0f;
			GetTree().CreateTimer(0.5f).Timeout += () => { if (_animPlayer != null) _animPlayer.SpeedScale = 1.0f; };
		}
	}

	protected virtual void Die()
	{
		_isDead = true;
		PlayAnimationRobust("Death");
		var colShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (colShape != null) colShape.SetDeferred("disabled", true);
		RemoveFromGroup("targetables");

		if (_healthBar != null)
		{
			GetTree().CreateTimer(1.0f).Timeout += () =>
			{
				if (_healthBar != null && IsInstanceValid(_healthBar)) { _healthBar.QueueFree(); _healthBar = null; }
			};
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Update Status Timers
		if (_stunTimer > 0) _stunTimer -= dt;

		if (_tauntTimer > 0)
		{
			_tauntTimer -= dt;
			if (_tauntTimer <= 0) _tauntTarget = null;
		}

		if (_debuffTimer > 0)
		{
			_debuffTimer -= dt;
			if (_debuffTimer <= 0) DamageModifier = 1.0f;
		}

#if DEBUG
		if (Input.IsKeyPressed(Key.T)) DebugShuffleAnimation();
#endif
	}

	public void ApplyStun(float duration)
	{
		if (duration > _stunTimer) _stunTimer = duration;
		GD.Print($"[Monsters] {Species} stunned for {duration}s");
	}

	public void ApplyTaunt(Node3D target, float duration)
	{
		_tauntTarget = target;
		if (duration > _tauntTimer) _tauntTimer = duration;
		GD.Print($"[Monsters] {Species} taunted by {target?.Name} for {duration}s");
	}

	public void ApplyDebuff(float reductionPercent, float duration)
	{
		DamageModifier = 1.0f - reductionPercent;
		_debuffTimer = duration;
		GD.Print($"[Monsters] {Species} AP reduced by {reductionPercent * 100}% for {duration}s");
	}

	public virtual void OnPartDestroyed(MonsterPart part)
	{
		GD.Print($"[Monsters] {Species} lost its {part.PartName}!");

		// Example behavior change: Leg hit reduces speed
		if (part.PartName.ToLower().Contains("leg"))
		{
			if (_ai != null)
			{
				_ai.GetType().GetField("_moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_ai, 1.5f); // Slow down
				GD.Print($"[Monsters] {Species} speed reduced due to leg damage.");
			}
		}

		// Placeholder for visuals (e.g. hiding mesh parts or spawning gibs)
	}
}
