using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class PlagueEffect : Node3D
{
    private float _duration = 10.0f;
    private float _damagePerSecond = 15.0f;
    private float _tickTimer = 0f;
    private Node3D _host;
    private Node3D _caster;
    private bool _isSpreading = false;

    public static void Apply(Node3D target, Node3D caster, float duration)
    {
        // Non-stacking check
        foreach (Node child in target.GetChildren())
        {
            if (child is PlagueEffect existing)
            {
                // Refresh duration if new one is longer
                if (duration > existing._duration) existing._duration = duration;
                return;
            }
        }

        var scene = GD.Load<PackedScene>("res://Scenes/VFX/PlagueEffect.tscn");
        if (scene != null)
        {
            var effect = scene.Instantiate<PlagueEffect>();
            target.AddChild(effect);
            effect._host = target;
            effect._caster = caster;
            effect._duration = duration;

            // Position at center/chest
            effect.Position = new Vector3(0, 1.2f, 0);
        }
    }

    public override void _Process(double delta)
    {
        if (_host == null || !IsInstanceValid(_host))
        {
            QueueFree();
            return;
        }

        // Damage Tick
        _tickTimer += (float)delta;
        if (_tickTimer >= 1.0f)
        {
            ApplyDamage();
            _tickTimer = 0f;
        }

        // Duration check
        _duration -= (float)delta;
        if (_duration <= 0)
        {
            QueueFree();
            return;
        }

        // Death Spread Logic
        bool isDead = false;
        if (_host is Monsters m && m.Health <= 0) isDead = true;
        else if (_host is PlayerController pc && pc.GetNodeOrNull<ArcherySystem>("ArcherySystem")?.PlayerStats.CurrentHealth <= 0) isDead = true;

        if (isDead && !_isSpreading)
        {
            _isSpreading = true;
            SpreadPlague();
            QueueFree();
        }
    }

    private void ApplyDamage()
    {
        if (_host is Monsters m) m.OnHit(_damagePerSecond, m.GlobalPosition, Vector3.Up, _caster);
        else if (_host is PlayerController pc) pc.OnHit(_damagePerSecond, pc.GlobalPosition, Vector3.Up, _caster);
    }

    private void SpreadPlague()
    {
        GD.Print($"[PlagueEffect] Spreading from dying host: {_host.Name}");

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = new SphereShape3D { Radius = 3.5f }; // Reduced spread radius
        query.Transform = GlobalTransform;
        query.CollisionMask = 1 | 2;
        if (_host is CollisionObject3D co)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
        }

        var results = spaceState.IntersectShape(query);
        foreach (var result in results)
        {
            var collider = (Node)result["collider"];
            var target = collider as Node3D ?? collider.GetParent() as Node3D;

            if (target != null && target != _caster)
            {
                MobaTeam targetTeam = MobaTeam.None;
                if (target is InteractableObject io) targetTeam = io.Team;
                else if (target is PlayerController pc) targetTeam = pc.Team;

                MobaTeam casterTeam = (_caster is PlayerController casterPC) ? casterPC.Team : MobaTeam.None;

                if (TeamSystem.AreEnemies(casterTeam, targetTeam) || targetTeam == MobaTeam.None)
                {
                    Apply(target, _caster, _duration);
                }
            }
        }
    }
}
