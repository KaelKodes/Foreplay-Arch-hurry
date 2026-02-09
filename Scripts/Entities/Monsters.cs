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

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("interactables");
		AddToGroup("monsters");

		IsTargetable = true;

		_animPlayer = FindAnimationPlayerRecursive(this);
		if (_animPlayer == null) _animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		if (_animPlayer != null && _animPlayer.GetAnimationList().Length == 0)
		{
			var populatedPlayer = FindPopulatedAnimationPlayerRecursive(this);
			if (populatedPlayer != null && populatedPlayer != _animPlayer) _animPlayer = populatedPlayer;
		}

		UpdateSpeciesVisuals();

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
		Health -= damage;
		SpawnDamageNumber(damage, hitPosition);
		UpdateHealthBar();

		if (Health <= 0) Die();
		else PlayHitReaction();
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
#if DEBUG
		if (Input.IsKeyPressed(Key.T)) DebugShuffleAnimation();
#endif
	}
}
