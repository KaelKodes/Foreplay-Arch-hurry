using Godot;
using System;
using Archery;

public partial class TargetGreen : Area3D
{
    [Export] public string GreenName = "Target Green";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is ArrowController arrow)
        {
            GD.Print($"🎯 BULLSEYE! Landed on {GreenName}!");
            // Future: Trigger score / sound
        }
    }
}
