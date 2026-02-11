using Godot;
using System;

namespace Archery;

public partial class NecroPoisonEffect : Node3D
{
    private float _duration = 2.5f;
    private Node3D _host;
    private float _timer = 0f;

    public override void _Ready()
    {
        _host = GetParent<Node3D>();
        ApplyVisuals();
    }

    public static void Apply(Node3D target, float duration = 2.5f)
    {
        // Non-stacking check
        foreach (Node child in target.GetChildren())
        {
            if (child is NecroPoisonEffect existing)
            {
                existing._duration = duration;
                return;
            }
        }

        var effect = new NecroPoisonEffect();
        effect._duration = duration;
        target.AddChild(effect);
    }

    private void ApplyVisuals()
    {
        if (_host is InteractableObject io)
        {
            io.UpdateVisuals(new Color(1.0f, 0.4f, 0.8f), true); // Pink tint
        }
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer >= _duration)
        {
            RemoveVisuals();
            QueueFree();
        }
    }

    private void RemoveVisuals()
    {
        if (_host is InteractableObject io)
        {
            io.SetSelected(io.IsSelected); // Restore original visuals
        }
    }
}
