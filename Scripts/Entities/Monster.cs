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

public partial class Monster : InteractableObject
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
                GD.Print($"[Monster] Found empty AnimationPlayer at {_animPlayer.GetPath()}. Searching further...");
                _animPlayer = FindPopulatedAnimationPlayerRecursive(this);
            }
        }

        UpdateSpeciesVisuals();

        // Load zombie-specific animations from Scary Zombie Pack
        if (Species.ToLower() == "zombie" && _animPlayer != null)
        {
            LoadZombieAnimations();
        }

        if (_animPlayer != null)
        {
            GD.Print($"[Monster] Using AnimationPlayer: {_animPlayer.GetPath()} ({_animPlayer.GetAnimationList().Length} animations)");
            PlayAnimationRobust("Idle");
        }
        else
        {
            GD.PrintErr("[Monster] No AnimationPlayer found anywhere in hierarchy!");
        }
    }

    private void LoadZombieAnimations()
    {
        // Map animation names to FBX files in the Scary Zombie Pack
        var zombieAnimFiles = new Dictionary<string, string>
        {
            { "Idle", "res://Assets/Scary Zombie Pack/zombie idle.fbx" },
            { "Walk", "res://Assets/Scary Zombie Pack/zombie walk.fbx" },
            { "Run", "res://Assets/Scary Zombie Pack/zombie run.fbx" },
            { "Attack", "res://Assets/Scary Zombie Pack/zombie attack.fbx" },
            { "Death", "res://Assets/Scary Zombie Pack/zombie death.fbx" },
            { "Hit", "res://Assets/Scary Zombie Pack/zombie scream.fbx" },
            { "Crawl", "res://Assets/Scary Zombie Pack/zombie crawl.fbx" }
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
                GD.PrintErr($"[Monster] Zombie animation not found: {fbxPath}");
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

                            GD.Print($"[Monster] Loaded zombie animation: {animName}");
                        }
                    }
                }

                tempInstance.QueueFree();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Monster] Failed to load zombie animation {animName}: {e.Message}");
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
        GD.Print($"[Monster] {ObjectName} took {damage} damage. Health: {Health}");

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
        GD.Print($"[Monster] {ObjectName} died!");

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
                GD.Print($"[Monster] Debug Shuffle: Playing {chosen} on {Species}");

                var scene = GetNodeOrNull("Visuals/scene");
                var skel = scene != null ? MonsterVisuals.FindVisibleSkeleton(scene) : null;
                if (skel != null)
                {
                    MonsterVisuals.PlaySharedAnimation(dirPath + chosen, _animPlayer, skel);
                }
            }
        }
    }
}
