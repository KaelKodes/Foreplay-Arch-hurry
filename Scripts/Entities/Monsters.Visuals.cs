using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class Monsters
{
    private void UpdateSpeciesVisuals()
    {
        MonsterVisuals.UpdateSpeciesVisuals(this, Species, _animPlayer, AutoSizeCollision ? UpdateCollisionShape : null);
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
        if (node == null) return null;
        if (node is AnimationPlayer ap && ap.GetAnimationList().Length > 0) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindPopulatedAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
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
                bool isLocal = false;
                if (_lastAttacker is PlayerController pc && pc.IsLocal) isLocal = true;

                dn.SetDamage(damage, isLocal);
            }
        }
    }

    internal void SpawnHealNumber(float amount)
    {
        if (!GameSettings.ShowDamageNumbers) return;

        var scene = GD.Load<PackedScene>("res://Scenes/VFX/DamageNumber.tscn");
        if (scene != null)
        {
            var dmgNum = scene.Instantiate<Node3D>();
            GetTree().CurrentScene.AddChild(dmgNum);
            dmgNum.GlobalPosition = GlobalPosition + new Vector3(0, 1.5f, 0);

            if (dmgNum is DamageNumber dn)
            {
                dn.SetHeal(amount);
            }
        }
    }

    private void UpdateHealthBar()
    {
        if (!GameSettings.ShowEnemyHealthBars) return;

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
            float yPos = GameSettings.HealthBarsAboveEnemy ? 2.5f : 0.15f;
            _healthBar.Position = new Vector3(0, yPos, 0);
            _healthBar.UpdateHealth(Health, MaxHealth);
        }
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
                if (skel != null) MonsterVisuals.PlaySharedAnimation(dirPath + chosen, _animPlayer, skel);
            }
        }
    }
}
