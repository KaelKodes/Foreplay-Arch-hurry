using Godot;
using Archery;

/// <summary>
/// Static helper to handle Ranger-specific ability logic.
/// Keeps GenericHeroAbility and PlayerController clean.
/// </summary>
public static class RangerAbilities
{
    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
        if (archery == null) return;

        switch (slot)
        {
            case 0: // Rapid Fire (1) - Slot 0
                // Just fires faster? Or normal shot?
                // GenericHeroAbility was using QuickFire.
                archery.QuickFire(0f);
                break;

            case 1: // Piercing Shot (2) - Slot 1
                archery.SetNextShotPiercing(true);
                archery.QuickFire(0f);
                break;

            case 2: // Rain of Arrows (3) - Slot 2
                CastRainOfArrows(caster, archery);
                break;

            case 3: // Vault (4) - Slot 3
                CastVault(caster);
                break;
        }
    }

    private static void CastRainOfArrows(PlayerController caster, ArcherySystem archery)
    {
        // Determine target position
        Vector3 targetPos;
        if (archery.CurrentTarget != null)
        {
            targetPos = archery.CurrentTarget.GlobalPosition;
        }
        else
        {
            // Default: 12m in front of player
            Vector3 fwd = -caster.GlobalTransform.Basis.Z;
            fwd.Y = 0;
            targetPos = caster.GlobalPosition + fwd.Normalized() * 12.0f;
        }

        // Create the effect object (it's a pure code Node3D)
        var rain = new RainOfArrowsEffect();
        caster.GetTree().CurrentScene.AddChild(rain);
        rain.GlobalPosition = targetPos;

        // Configure and Start
        // Damage scaling: 40% of Power per arrow?
        float damage = archery.PlayerStats.Power * 0.4f;
        rain.Start(archery.ArrowScene, damage, caster);

        GD.Print($"[RangerAbilities] Rain of Arrows cast at {targetPos}");
    }

    private static void CastVault(PlayerController caster)
    {
        caster.PerformVault();

        // Decoy Logic
        // Spawn a decoy at previous position?
        // PerformVault moves the player immediately? 
        // Actually PerformVault sets velocity/state, movement happens over time.
        // So current position IS the start position.

        SpawnDecoy(caster);
    }

    private static void SpawnDecoy(PlayerController caster)
    {
        // "Ghost" Decoy Implementation
        // For now, we use a mesh instance but style it to look like a ghost.
        // Ideally, this would be a PackedScene "Decoy.tscn" with health and aggro logic.

        var decoy = new MeshInstance3D();
        decoy.Mesh = new CapsuleMesh();

        // Make it look ghostly (Transparent Blue)
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 0.5f, 1f, 0.4f); // Cyan/Blue transparent
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0, 0.2f, 0.8f);
        mat.EmissionEnergyMultiplier = 0.5f;
        decoy.MaterialOverride = mat;

        caster.GetTree().CurrentScene.AddChild(decoy);

        // Position at caster's location (before they vaulted away)
        decoy.GlobalPosition = caster.GlobalPosition;
        decoy.GlobalRotation = caster.GlobalRotation;
        decoy.Name = "RangerDecoy";

        GD.Print($"[RangerAbilities] Decoy Spawned at {decoy.GlobalPosition}");

        // Auto-destroy decoy after 3s
        var timer = caster.GetTree().CreateTimer(3.0f);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(decoy))
            {
                decoy.QueueFree();
            }
        };
    }
}
