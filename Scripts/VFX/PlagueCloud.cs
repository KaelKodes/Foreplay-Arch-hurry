using Godot;
using System;

namespace Archery;

public partial class PlagueCloud : Node3D
{
    private float _duration = 10.0f;
    private float _radius = 3.0f;
    private float _timer = 0f;
    private Node3D _caster;

    public void Initialize(Node3D caster)
    {
        _caster = caster;
    }

    public override void _Ready()
    {
        // Initial infection pulse
        ApplyInfection();
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;

        // Re-infect every 0.5s for enemies staying in the cloud
        if (_timer >= 0.5f)
        {
            ApplyInfection();
            _timer = 0f;
        }

        _duration -= (float)delta;
        if (_duration <= 0) QueueFree();
    }

    private void ApplyInfection()
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = new SphereShape3D { Radius = _radius };
        query.Transform = GlobalTransform;
        query.CollisionMask = 1 | 2; // Monsters and Players

        var results = spaceState.IntersectShape(query);
        foreach (var result in results)
        {
            var collider = (Node)result["collider"];
            var target = collider as Node3D ?? collider.GetParent() as Node3D;

            if (target != null && target != _caster)
            {
                // Verify team enmity
                MobaTeam targetTeam = MobaTeam.None;
                if (target is InteractableObject io) targetTeam = io.Team;
                else if (target is PlayerController pc) targetTeam = pc.Team;

                MobaTeam casterTeam = (_caster is PlayerController casterPC) ? casterPC.Team : MobaTeam.None;

                if (TeamSystem.AreEnemies(casterTeam, targetTeam) || targetTeam == MobaTeam.None)
                {
                    PlagueEffect.Apply(target, _caster, 10.0f);
                }
            }
        }
    }
}
