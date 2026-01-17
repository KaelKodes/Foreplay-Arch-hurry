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

        Health -= damage;
        GD.Print($"[Monster] {ObjectName} took {damage} damage. Health: {Health}");

        if (Health <= 0)
        {
            Die();
        }
        else
        {
            PlayHitReaction();
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

        SceneTreeTimer timer = GetTree().CreateTimer(2.0f);
        timer.Timeout += () => QueueFree();
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
