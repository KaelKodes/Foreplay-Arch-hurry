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

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("interactables");
		AddToGroup("monsters");

		IsTargetable = true;

		_animPlayer = FindAnimationPlayerRecursive(this);

		if (_animPlayer == null)
		{
			_animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		}

		if (_animPlayer != null)
		{
			var anims = _animPlayer.GetAnimationList();
			if (anims.Length == 0)
			{
				GD.Print($"[Monsters] Found empty AnimationPlayer at {_animPlayer.GetPath()}. Will try to load animations dynamically.");
				// Keep the empty player - we may need it for species that load animations dynamically
				// But also check if there's a populated one deeper in the hierarchy
                var populatedPlayer = FindPopulatedAnimationPlayerRecursive(this);
                if (populatedPlayer != null && populatedPlayer != _animPlayer)
                {
                    GD.Print($"[Monsters] Found populated AnimationPlayer deeper in hierarchy, using that instead.");
                    _animPlayer = populatedPlayer;
                }
                // Otherwise keep the empty one for dynamic loading
            }
        }

        UpdateSpeciesVisuals();

        // Load species-specific animations from external FBX files
        // This works even if _animPlayer started empty
        if (_animPlayer != null)
        {
            string s = Species.ToLower();
            if (s == "zombie") LoadZombieAnimations();
            else if (s == "crawler") LoadCrawlerAnimations();
            else if (s == "skeleton") AliasEmbeddedAnimations();
            else if (s == "lich") AliasEmbeddedAnimations();
            else if (s == "conjurer") AliasEmbeddedAnimations();
        }

        if (_animPlayer != null)
        {
            int animCount = _animPlayer.GetAnimationList().Length;
            if (animCount > 0)
            {
                GD.Print($"[Monsters] Using AnimationPlayer: {_animPlayer.GetPath()} ({animCount} animations)");
                PlayAnimationRobust("Idle");
            }
            else
            {
                GD.PrintErr($"[Monsters] AnimationPlayer at {_animPlayer.GetPath()} has no animations after loading!");
            }
        }
        else
        {
            GD.PrintErr("[Monsters] No AnimationPlayer found anywhere in hierarchy!");
        }

        // Add AI component if not already present
        _ai = GetNodeOrNull<MonsterAI>("MonsterAI");
        if (_ai == null)
        {
            _ai = new MonsterAI();
            _ai.Name = "MonsterAI";
            AddChild(_ai);
            GD.Print($"[Monsters] Added MonsterAI to {ObjectName}");
        }
    }

    private void LoadZombieAnimations()
    {
        // Map animation names to FBX files in the Monsters/Zombie folder
        var zombieAnimFiles = new Dictionary<string, string>
        {
            { "Idle", "res://Assets/Monsters/Zombie/zombie idle.fbx" },
            { "Walk", "res://Assets/Monsters/Zombie/zombie walk.fbx" },
            { "Run", "res://Assets/Monsters/Zombie/zombie run.fbx" },
            { "Attack", "res://Assets/Monsters/Zombie/zombie attack.fbx" },
            { "Death", "res://Assets/Monsters/Zombie/zombie death.fbx" },
            { "Hit", "res://Assets/Monsters/Zombie/zombie scream.fbx" },
            { "Crawl", "res://Assets/Monsters/Zombie/zombie crawl.fbx" }
        };

        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null)
        {
            lib = new AnimationLibrary();
            _animPlayer.AddAnimationLibrary("", lib);
        }

        foreach (var kvp in zombieAnimFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            if (!ResourceLoader.Exists(fbxPath))
            {
                GD.PrintErr($"[Monsters] Zombie animation not found: {fbxPath}");
                continue;
            }

            try
            {
                var animScene = GD.Load<PackedScene>(fbxPath);
                if (animScene == null) continue;

                var tempInstance = animScene.Instantiate();
                var tempAnimPlayer = FindAnimationPlayerRecursive(tempInstance);

                if (tempAnimPlayer != null)
                {
                    var animList = tempAnimPlayer.GetAnimationList();
                    if (animList.Length > 0)
                    {
                        // Get the first animation (usually "Take 001" or similar)
                        var srcAnim = tempAnimPlayer.GetAnimation(animList[0]);
                        if (srcAnim != null)
                        {
                            // Duplicate and add to our library
                            var animCopy = (Animation)srcAnim.Duplicate();
							// Idle/Walk/Run/Crawl loop, Hit/Attack/Death don't loop
							bool shouldLoop = animName == "Idle" || animName == "Walk" || animName == "Run" || animName == "Crawl";
							animCopy.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

							if (lib.HasAnimation(animName))
								lib.RemoveAnimation(animName);
							lib.AddAnimation(animName, animCopy);

							GD.Print($"[Monsters] Loaded zombie animation: {animName}");
						}
					}
				}

				tempInstance.QueueFree();
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Monsters] Failed to load zombie animation {animName}: {e.Message}");
			}
		}
	}

	private void LoadCrawlerAnimations()
	{
		// Crawler model (Anim_Monster_1.fbx) has animations embedded
		// We just need to map them to standard names
		if (_animPlayer == null) return;

		var animList = _animPlayer.GetAnimationList();
		GD.Print($"[Monsters] Crawler embedded animations: {string.Join(", ", animList)}");

		// Find and alias animations to standard names
		var lib = _animPlayer.GetAnimationLibrary("");
		if (lib == null) return;

		foreach (var animName in animList)
		{
			string lowerName = animName.ToLower();
			Animation anim = _animPlayer.GetAnimation(animName);
			if (anim == null) continue;

			// Map embedded animation names to standard names
			if (lowerName.Contains("idle") && !lib.HasAnimation("Idle"))
			{
				var copy = (Animation)anim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Idle", copy);
			}
			else if (lowerName.Contains("walk") && !lib.HasAnimation("Walk"))
			{
				var copy = (Animation)anim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Walk", copy);
			}
			else if (lowerName.Contains("attack") && !lib.HasAnimation("Attack"))
			{
				var copy = (Animation)anim.Duplicate();
				lib.AddAnimation("Attack", copy);
			}
			else if (lowerName.Contains("death") && !lib.HasAnimation("Death"))
			{
				var copy = (Animation)anim.Duplicate();
				lib.AddAnimation("Death", copy);
			}
		}

		// If no Idle found, use the first animation as Idle
		if (!lib.HasAnimation("Idle") && animList.Length > 0)
		{
			var firstAnim = _animPlayer.GetAnimation(animList[0]);
			if (firstAnim != null)
			{
				var copy = (Animation)firstAnim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Idle", copy);
				GD.Print($"[Monsters] Created Idle from first animation: {animList[0]}");
			}
		}
	}

	private void LoadExternalAnimations(Dictionary<string, string> animFiles)
	{
		var lib = _animPlayer.GetAnimationLibrary("");
		if (lib == null)
		{
			lib = new AnimationLibrary();
			_animPlayer.AddAnimationLibrary("", lib);
		}

		foreach (var kvp in animFiles)
		{
			string animName = kvp.Key;
			string fbxPath = kvp.Value;

			if (!ResourceLoader.Exists(fbxPath)) continue;

			try
			{
				var animScene = GD.Load<PackedScene>(fbxPath);
				if (animScene == null) continue;

				var tempInstance = animScene.Instantiate();
				var tempAnimPlayer = FindAnimationPlayerRecursive(tempInstance);

				if (tempAnimPlayer != null)
				{
					var animList = tempAnimPlayer.GetAnimationList();
					if (animList.Length > 0)
					{
						var srcAnim = tempAnimPlayer.GetAnimation(animList[0]);
						if (srcAnim != null)
						{
							var animCopy = (Animation)srcAnim.Duplicate();
							bool shouldLoop = animName == "Idle" || animName == "Walk" || animName == "Run" || animName == "Crawl";
							animCopy.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

							if (lib.HasAnimation(animName))
								lib.RemoveAnimation(animName);
							lib.AddAnimation(animName, animCopy);
						}
					}
				}
				tempInstance.QueueFree();
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Monsters] Failed to load animation {animName}: {e.Message}");
			}
		}
	}

	private void PlayAnimationRobust(string animName)
	{
		if (_animPlayer == null) return;

		var anims = _animPlayer.GetAnimationList();

		if (_animPlayer.HasAnimation(animName))
		{
			_animPlayer.Play(animName);
		}
		else if (anims.Length > 0)
		{
			string fallback = anims[0];

			var animRef = _animPlayer.GetAnimation(fallback);
			if (animRef != null)
			{
				animRef.LoopMode = Animation.LoopModeEnum.Linear;
			}

			_animPlayer.Play(fallback);
		}
	}

	/// <summary>
	/// Public method for AI to set animation state.
	/// </summary>
	public void SetAnimation(string animName)
	{
		PlayAnimationRobust(animName);
	}

	/// <summary>
	/// Apply movement velocity. Since Monsters extends Node3D (via InteractableObject)
	/// but is attached to a CharacterBody3D node, we access movement via the native node.
	/// </summary>
	public void ApplyMovement(Vector3 velocity, float delta)
	{
		// Apply horizontal movement
		Vector3 newPos = GlobalPosition;
		newPos.X += velocity.X * delta;
		newPos.Z += velocity.Z * delta;

		// Raycast down to find ground
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(
			newPos + Vector3.Up * 2.0f,  // Start above
			newPos + Vector3.Down * 5.0f  // End below
		);
		query.CollisionMask = 2; // Terrain layer

		var result = spaceState.IntersectRay(query);
		if (result.Count > 0)
		{
			// Snap to ground
			newPos.Y = ((Vector3)result["position"]).Y;
		}

		GlobalPosition = newPos;
	}

	private void UpdateSpeciesVisuals()
	{
		MonsterVisuals.UpdateSpeciesVisuals(this, Species, _animPlayer, UpdateCollisionShape);
	}

	private void UpdateCollisionShape()
	{
		MonsterVisuals.UpdateCollisionShape(this);
	}

	private AnimationPlayer FindAnimationPlayerRecursive(Node node)
	{
		if (node is AnimationPlayer ap) return ap;
		foreach (Node child in node.GetChildren())
		{
			var found = FindAnimationPlayerRecursive(child);
			if (found != null) return found;
		}
		return null;
	}

	private AnimationPlayer FindPopulatedAnimationPlayerRecursive(Node node)
	{
		if (node is AnimationPlayer ap && ap.GetAnimationList().Length > 0) return ap;
		foreach (Node child in node.GetChildren())
		{
			var found = FindPopulatedAnimationPlayerRecursive(child);
			if (found != null) return found;
		}
		return null;
	}

	public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitDirection)
	{
		if (_isDead) return;

		// Broadcast hit to all clients for synced visuals
		Rpc(nameof(NetOnHit), damage, hitPosition);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void NetOnHit(float damage, Vector3 hitPosition)
	{
		if (_isDead) return;

		Health -= damage;
		GD.Print($"[Monsters] {ObjectName} took {damage} damage. Health: {Health}");

		// Spawn floating damage number
		SpawnDamageNumber(damage, hitPosition);

		// Update or create health bar
		UpdateHealthBar();

		if (Health <= 0)
		{
			Die();
		}
		else
		{
			PlayHitReaction();
		}
	}

	private void SpawnDamageNumber(float damage, Vector3 hitPosition)
	{
		if (!GameSettings.ShowDamageNumbers) return;

		var scene = GD.Load<PackedScene>("res://Scenes/VFX/DamageNumber.tscn");
		if (scene != null)
		{
			var dmgNum = scene.Instantiate<Node3D>();
			GetTree().CurrentScene.AddChild(dmgNum);
			dmgNum.GlobalPosition = hitPosition + new Vector3(0, 0.5f, 0);

			if (dmgNum is DamageNumber dn)
			{
				dn.SetDamage(damage);
			}
		}
	}

	private void UpdateHealthBar()
	{
		if (!GameSettings.ShowEnemyHealthBars) return;

		// Create health bar if it doesn't exist
        if (_healthBar == null)
        {
            var scene = GD.Load<PackedScene>("res://Scenes/UI/Combat/HealthBar3D.tscn");
            if (scene != null)
            {
                _healthBar = scene.Instantiate<HealthBar3D>();
                AddChild(_healthBar);
            }
        }

        if (_healthBar != null)
        {
            // Position based on user preference
            float yPos = GameSettings.HealthBarsAboveEnemy ? 2.5f : 0.15f;
            _healthBar.Position = new Vector3(0, yPos, 0);

            _healthBar.UpdateHealth(Health, MaxHealth);
        }
    }

    private void PlayHitReaction()
    {
        PlayAnimationRobust("Hit");

        if (_animPlayer != null && !_animPlayer.HasAnimation("Hit"))
        {
            _animPlayer.SpeedScale = 2.0f;
            SceneTreeTimer timer = GetTree().CreateTimer(0.5f);
            timer.Timeout += () =>
            {
                if (_animPlayer != null) _animPlayer.SpeedScale = 1.0f;
            };
        }
    }

    private void Die()
    {
        _isDead = true;
        GD.Print($"[Monsters] {ObjectName} died!");

        PlayAnimationRobust("Death");

		// Corpse remains - don't auto-delete
		// Disable collision so player can walk through corpse
		var colShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (colShape != null) colShape.Disabled = true;

		// Remove health bar after a short delay
		if (_healthBar != null)
		{
			SceneTreeTimer timer = GetTree().CreateTimer(1.0f);
			timer.Timeout += () =>
			{
				if (_healthBar != null && IsInstanceValid(_healthBar))
				{
					_healthBar.QueueFree();
					_healthBar = null;
				}
			};
		}
	}

	public override void _Process(double delta)
	{
#if DEBUG
		if (Input.IsKeyPressed(Key.T))
		{
			DebugShuffleAnimation();
		}
#endif
	}

	private void DebugShuffleAnimation()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastShuffleTime < 0.2) return;
		_lastShuffleTime = now;

		if (_animPlayer == null) return;

		string dirPath = "res://Assets/Animations/Monsters/";
		var dir = DirAccess.Open(dirPath);
		if (dir != null)
		{
			dir.ListDirBegin();
			List<string> files = new List<string>();
			string file = dir.GetNext();
			while (file != "")
			{
				if (file.EndsWith(".res") || file.EndsWith(".tres")) files.Add(file);
				file = dir.GetNext();
			}

			if (files.Count > 0)
			{
				var rnd = new RandomNumberGenerator();
				rnd.Randomize();
				string chosen = files[rnd.RandiRange(0, files.Count - 1)];
				GD.Print($"[Monsters] Debug Shuffle: Playing {chosen} on {Species}");

				var scene = GetNodeOrNull("Visuals/scene");
				var skel = scene != null ? MonsterVisuals.FindVisibleSkeleton(scene) : null;
				if (skel != null)
				{
					MonsterVisuals.PlaySharedAnimation(dirPath + chosen, _animPlayer, skel);
				}
			}
		}
	}

	private void AliasEmbeddedAnimations()
	{
		if (_animPlayer == null) return;
		var animList = _animPlayer.GetAnimationList();
		var lib = _animPlayer.GetAnimationLibrary("");
		if (lib == null) return;

		foreach (var animName in animList)
		{
			string lowerName = animName.ToLower();
			Animation anim = _animPlayer.GetAnimation(animName);
			if (anim == null) continue;

			// Map any animation containing "idle" to "Idle"
			if (lowerName.Contains("idle") && !lib.HasAnimation("Idle"))
			{
				var copy = (Animation)anim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Idle", copy);
			}
			// Map any animation containing "walk" to "Walk"
			else if (lowerName.Contains("walk") && !lib.HasAnimation("Walk"))
			{
				var copy = (Animation)anim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Walk", copy);
			}
			// Map "run" and "fast" to "Run"
			else if ((lowerName.Contains("run") || lowerName.Contains("fast")) && !lib.HasAnimation("Run"))
			{
				var copy = (Animation)anim.Duplicate();
				copy.LoopMode = Animation.LoopModeEnum.Linear;
				lib.AddAnimation("Run", copy);
			}
			// Map "attack" to "Attack"
			else if (lowerName.Contains("attack") && !lib.HasAnimation("Attack"))
			{
				var copy = (Animation)anim.Duplicate();
				lib.AddAnimation("Attack", copy);
			}
			// Map "death" or "die" to "Death"
			else if ((lowerName.Contains("death") || lowerName.Contains("die")) && !lib.HasAnimation("Death"))
			{
				var copy = (Animation)anim.Duplicate();
				lib.AddAnimation("Death", copy);
			}
		}

		// Fallback: If no Idle, use first one
		if (!lib.HasAnimation("Idle") && animList.Length > 0)
		{
			var copy = (Animation)_animPlayer.GetAnimation(animList[0]).Duplicate();
			copy.LoopMode = Animation.LoopModeEnum.Linear;
			lib.AddAnimation("Idle", copy);
		}
	}
}
