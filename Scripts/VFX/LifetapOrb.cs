using Godot;
using System;

namespace Archery;

public partial class LifetapOrb : Node3D
{
    private Node3D _targetCaster;
    private float _speed = 10.0f;
    private float _arrivalThreshold = 0.5f;
    private float _healAmount = 0f;

    public void Initialize(Node3D targetCaster, float healAmount)
    {
        _targetCaster = targetCaster;
        _healAmount = healAmount;
    }

    public override void _Process(double delta)
    {
        if (_targetCaster == null || !IsInstanceValid(_targetCaster))
        {
            QueueFree();
            return;
        }

        // Move towards the caster's chest/center
        Vector3 targetPos = _targetCaster.GlobalPosition + new Vector3(0, 1.2f, 0);
        Vector3 direction = (targetPos - GlobalPosition).Normalized();

        GlobalPosition += direction * _speed * (float)delta;

        // Check for arrival
        if (GlobalPosition.DistanceTo(targetPos) < _arrivalThreshold)
        {
            OnArrival();
        }
    }

    private void OnArrival()
    {
        if (_targetCaster is PlayerController pc)
        {
            pc.Heal(_healAmount);
        }
        else if (_targetCaster is Monsters monster)
        {
            monster.Heal(_healAmount);
        }

        QueueFree();
    }
}
