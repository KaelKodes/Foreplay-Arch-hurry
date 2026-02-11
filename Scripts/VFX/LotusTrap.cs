using Godot;
using System;

namespace Archery;

public partial class LotusTrap : Node3D
{
    public MobaTeam CasterTeam { get; set; } = MobaTeam.None;
    public Node3D Caster { get; set; }
    private float _radius = 30.0f;
    private bool _isTriggered = false;

    public void OnBodyEntered(Node body)
    {
        if (_isTriggered) return;

        Node3D target = null;
        if (body is InteractableObject io)
        {
            // Only trigger for enemies
            if (TeamSystem.AreEnemies(CasterTeam, io.Team) || io.Team == MobaTeam.None)
            {
                target = io;
            }
        }

        if (target != null)
        {
            _isTriggered = true;
            TriggerTrap(target);
        }
    }

    private void TriggerTrap(Node3D victim)
    {
        GD.Print($"[LotusTrap] Triggered by {victim.Name}!");

        // 1. Apply Poison Effect & Pink Visuals
        NecroPoisonEffect.Apply(victim, 2.5f);

        // 2. Pink Cloud VFX (Using Shockwave as template)
        var shockScene = GD.Load<PackedScene>("res://Scenes/VFX/Shockwave.tscn");
        if (shockScene != null)
        {
            var sw = shockScene.Instantiate<Shockwave>();
            GetTree().CurrentScene.AddChild(sw);
            sw.GlobalPosition = GlobalPosition;
            sw.SetColor(new Color(1.0f, 0.4f, 0.8f)); // Pink!
        }

        // 1. Apply Snare (40% slow)
        // If the victim has a movement component, we'd slow it. 
        // For Monsters, we can nudge their AI speed.
        if (victim is Monsters m)
        {
            var ai = m.GetNodeOrNull<MonsterAI>("MonsterAI");
            if (ai != null)
            {
                // Temporary slow
                float originalSpeed = (float)ai.GetType().GetField("_moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ai);
                ai.GetType().GetField("_moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(ai, originalSpeed * 0.6f);

                GetTree().CreateTimer(5.0f).Timeout += () =>
                {
                    if (IsInstanceValid(ai))
                        ai.GetType().GetField("_moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(ai, originalSpeed);
                };
            }
        }

        // 2. Broadcast to Skeletons
        var skeletons = GetTree().GetNodesInGroup("monster_ai");
        foreach (var node in skeletons)
        {
            if (node is SummonedSkeletonAI skelAI)
            {
                float dist = GlobalPosition.DistanceTo(skelAI.GetParent<Node3D>().GlobalPosition);
                if (dist <= _radius)
                {
                    skelAI.SetForcedTarget(victim);
                }
            }
        }

        // 3. Visuals & Removal
        // Spawn some petals or particles here if desired
        QueueFree();
    }
}
