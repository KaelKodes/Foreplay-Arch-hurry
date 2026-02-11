using Godot;
using Archery;

/// <summary>
/// Static helper to handle Cleric-specific greatsword ability animations.
/// Slot 0: High Remedy — heal target/self (150 + 2.0 × INT)
/// Slot 1: Celestial Buff — +15 Speed (Haste, Concentration, Agility) for 30s
/// Slot 2: Judgement — AoE smite (60 + 1.2 × INT)
/// Slot 3: Divine Intervention — HP Shield on caster + nearby allies
/// </summary>
public static class ClericAbilities
{
    /// <summary>Get player stats from caster's ArcherySystem (shared stat service).</summary>
    private static Stats GetStats(PlayerController caster)
    {
        var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
        return archery?.PlayerStats ?? new Stats { Intelligence = 10 };
    }

    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var modelMgr = caster.GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        switch (slot)
        {
            case 0: // High Remedy (Ability 1)
                CastHighRemedy(caster, modelMgr);
                break;

            case 1: // Celestial Buff (Ability 2)
                CastCelestialBuff(caster, modelMgr);
                break;

            case 2: // Judgement (Ability 3)
                CastJudgement(caster, modelMgr);
                break;

            case 3: // Divine Intervention (Ability 4)
                CastDivineIntervention(caster, modelMgr);
                break;
        }
    }

    private static void CastHighRemedy(PlayerController caster, CharacterModelManager modelMgr)
    {
        modelMgr?.PlayAnimation("SpellCast");

        var stats = GetStats(caster);
        // 100 + (2.0 × INT) — matches tooltip
        float healAmount = 100f + (2.0f * stats.Intelligence);

        Node3D target = caster.CurrentTarget;
        bool targetHealed = false;

        if (target != null)
        {
            if (target is PlayerController tp && tp.Team == caster.Team)
            {
                tp.Heal(healAmount);
                SpawnHolySmoke(tp, caster);
                targetHealed = true;
                GD.Print($"[ClericAbilities] Healed ally {tp.Name} for {healAmount:F0} (INT:{stats.Intelligence})");
            }
            else if (target is Monsters tm && tm.Team == caster.Team)
            {
                tm.Heal(healAmount);
                SpawnHolySmoke(tm, caster);
                targetHealed = true;
                GD.Print($"[ClericAbilities] Healed ally monster {tm.Name} for {healAmount:F0}");
            }
        }

        if (!targetHealed)
        {
            caster.Heal(healAmount);
            SpawnHolySmoke(caster, caster);
            GD.Print($"[ClericAbilities] Self-healed for {healAmount:F0} (INT:{stats.Intelligence})");
        }
    }

    private static void SpawnHolySmoke(Node3D target, PlayerController caster)
    {
        var scene = GD.Load<PackedScene>("res://Scenes/VFX/HolySmoke.tscn");
        if (scene != null)
        {
            var smoke = scene.Instantiate<Node3D>();
            target.AddChild(smoke);
            smoke.Position = Vector3.Zero; // Relative to target, stays attached
        }
    }

    private static void CastCelestialBuff(PlayerController caster, CharacterModelManager modelMgr)
    {
        modelMgr?.PlayAnimation("SpellCast");

        // +15 to Haste, Concentration, and Agility for 30s
        int speedBonus = 15;
        float duration = 30.0f;

        Node3D buffTarget = caster.CurrentTarget;
        bool buffApplied = false;

        if (buffTarget != null && buffTarget is PlayerController tp && tp.Team == caster.Team)
        {
            tp.ApplySpeedBuff(speedBonus, speedBonus, speedBonus, duration);
            buffApplied = true;
            GD.Print($"[ClericAbilities] Speed buff applied to ally: {tp.Name}");
        }

        if (!buffApplied)
        {
            caster.ApplySpeedBuff(speedBonus, speedBonus, speedBonus, duration);
            GD.Print("[ClericAbilities] Speed buff applied to self");
        }
    }

    private static void CastJudgement(PlayerController caster, CharacterModelManager modelMgr)
    {
        modelMgr?.PlayAnimation("SpinSlot3");

        var stats = GetStats(caster);
        // 60 + (1.2 × INT) — matches tooltip
        float damage = 60f + (1.2f * stats.Intelligence);
        float radius = 6.0f;

        TargetingHelper.PerformAoEAction(caster, caster.GlobalPosition, radius, (enemy) =>
        {
            if (enemy is Monsters monster)
            {
                monster.OnHit(damage, monster.GlobalPosition, Vector3.Up, caster);
                monster.ApplyDebuff(0.15f, 4.0f); // -15% damage for 4s
            }
            else if (enemy is PlayerController player)
            {
                player.OnHit(damage, player.GlobalPosition, Vector3.Up, caster);
            }
        }, caster.Team);

        GD.Print($"[ClericAbilities] Judgement: {damage:F0} dmg (60 + 1.2×{stats.Intelligence} INT) in {radius}u radius");
    }

    private static void CastDivineIntervention(PlayerController caster, CharacterModelManager modelMgr)
    {
        modelMgr?.PlayAnimation("CastingSlot4");

        var stats = GetStats(caster);
        // Shield scales with INT: 50 + INT
        int shieldAmount = 50 + stats.Intelligence;
        float shieldDuration = 10.0f;
        float shieldRadius = 12.0f;

        // 1. Shield Caster
        caster.ApplyShield(shieldAmount, shieldDuration);

        // 2. Shield Nearby Allies
        var tree = caster.GetTree();
        var players = tree.GetNodesInGroup("player");
        foreach (var node in players)
        {
            if (node is PlayerController p && p != caster)
            {
                if (p.Team == caster.Team && caster.GlobalPosition.DistanceTo(p.GlobalPosition) <= shieldRadius)
                {
                    p.ApplyShield(shieldAmount, shieldDuration);
                    GD.Print($"[ClericAbilities] Shield ({shieldAmount}) applied to ally: {p.Name}");
                }
            }
        }

        GD.Print($"[ClericAbilities] Divine Intervention: {shieldAmount} shield (50 + {stats.Intelligence} INT) for {shieldDuration}s");
    }
}
