using Godot;
using System;

namespace Archery;

/// <summary>
/// Attached to individual collision shapes on a monster to track specific part damage.
/// </summary>
public partial class MonsterPart : Node3D
{
    [Export] public string PartName { get; set; } = "Unknown";
    [Export] public float HealthMultiplier { get; set; } = 1.0f; // Damage multiplier for this part (e.g. 2.0 for Head)
    [Export] public float PartHealth = 50.0f;
    [Export] public float MaxPartHealth = 50.0f;

    public bool IsDestroyed { get; private set; } = false;

    // Parent monster reference
    private Monsters _monster;

    public override void _Ready()
    {
        // Try to find the parent Monster script
        Node current = GetParent();
        while (current != null && !(current is Monsters))
        {
            current = current.GetParent();
        }
        _monster = current as Monsters;

        if (_monster == null)
        {
            GD.PrintErr($"[MonsterPart] {PartName} could not find parent Monsters script!");
        }
    }

    public void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal, Node attacker = null)
    {
        if (IsDestroyed) return;

        float appliedDamage = damage * HealthMultiplier;
        PartHealth -= appliedDamage;

        GD.Print($"[MonsterPart] {PartName} hit for {appliedDamage} damage. Health: {PartHealth}/{MaxPartHealth}");

        if (PartHealth <= 0)
        {
            DestroyPart();
        }

        // Pass damage to the main monster health as well
        _monster?.OnHit(appliedDamage, hitPosition, hitNormal, attacker);
    }

    private void DestroyPart()
    {
        IsDestroyed = true;
        GD.Print($"[MonsterPart] {PartName} has been DESTROYED!");

        // Notify monster to trigger specific visual/behavioral changes
        _monster?.OnPartDestroyed(this);
    }
}
