using Godot;
using Archery;

/// <summary>
/// Static helper to handle Warrior-specific ability logic.
/// Slot 0: Shield Slam (Kick animation) — melee AoE kick/bash
/// Slot 1: Intercept — dash to target
/// Slot 2: Demoralizing Shout (PowerUp animation) — AoE debuff/buff
/// Slot 3: Avatar of War — self-buff ultimate
/// </summary>
public static class WarriorAbilities
{
    /// <summary>Get player stats from caster's ArcherySystem (shared stat service).</summary>
    private static Stats GetStats(PlayerController caster)
    {
        var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
        return archery?.PlayerStats ?? new Stats { Strength = 10 };
    }

    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var modelMgr = caster.GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        switch (slot)
        {
            case 0: // Shield Slam (1) — uses Kick animation
                CastShieldSlam(caster, modelMgr);
                break;

            case 1: // Intercept (2) — dash to target
                CastIntercept(caster, modelMgr);
                break;

            case 2: // Demoralizing Shout (3) — uses PowerUp animation
                CastDemoralizingShout(caster, modelMgr);
                break;

            case 3: // Avatar of War (4) — self-buff
                CastAvatarOfWar(caster, modelMgr);
                break;
        }
    }

    private static void CastShieldSlam(PlayerController caster, CharacterModelManager modelMgr)
    {
        // Play kick animation
        modelMgr?.PlayAnimation("Kick");

        // Small delay to match animation strike
        caster.GetTree().CreateTimer(0.2f).Timeout += () =>
        {
            var stats = GetStats(caster);
            // 80% AD damage (matches tooltip)
            float damage = stats.AttackDamage * 0.8f;

            Vector3 forward = -caster.GlobalTransform.Basis.Z;
            Vector3 center = caster.GlobalPosition + (forward * 1.5f) + (Vector3.Up * 0.5f);
            float radius = 2.0f;

            TargetingHelper.PerformAoEAction(caster, center, radius, (target) =>
            {
                var monsterPart = target as MonsterPart ?? target.GetNodeOrNull<MonsterPart>("MonsterPart");
                var monster = target as Monsters ?? target.GetParent() as Monsters ?? FindAncestor<Monsters>(target);

                if (monsterPart != null)
                {
                    monsterPart.OnHit(damage, target.GlobalPosition, forward, caster);
                    monster?.ApplyStun(1.5f);
                }
                else if (monster != null)
                {
                    monster.OnHit(damage, target.GlobalPosition, forward, caster);
                    monster.ApplyStun(1.5f);
                }
                else if (target is InteractableObject io)
                {
                    io.OnHit(damage, target.GlobalPosition, forward, caster);
                }
            }, caster.Team);

            GD.Print($"[WarriorAbilities] Shield Slam: {damage:F0} dmg (80% of {stats.AttackDamage} AD) at {center} (Radius: {radius})");
        };
    }

    private static void CastIntercept(PlayerController caster, CharacterModelManager modelMgr)
    {
        // Play intercept/charge attack animation
        modelMgr?.PlayAnimation("MeleeAttack3");

        // Trigger the dash logic in the controller
        caster.PerformIntercept();

        // Hit detection at the end of the dash (0.4s duration)
        SceneTreeTimer timer = caster.GetTree().CreateTimer(0.4f);
        timer.Timeout += () =>
        {
            var stats = GetStats(caster);
            // 60% AD damage on impact
            float damage = stats.AttackDamage * 0.6f;

            Vector3 forward = caster.GlobalTransform.Basis.Z;
            Vector3 center = caster.GlobalPosition + forward * 1.5f + Vector3.Up * 0.5f;
            float radius = 2.5f;

            TargetingHelper.PerformAoEAction(caster, center, radius, (target) =>
            {
                var monsterPart = target as MonsterPart ?? target.GetNodeOrNull<MonsterPart>("MonsterPart");
                var monster = target as Monsters ?? target.GetParent() as Monsters ?? FindAncestor<Monsters>(target);

                if (monsterPart != null)
                {
                    monsterPart.OnHit(damage, target.GlobalPosition, forward, caster);
                    monster?.ApplyKnockback(forward, 2.5f);
                }
                else if (monster != null)
                {
                    monster.OnHit(damage, target.GlobalPosition, forward, caster);
                    monster.ApplyKnockback(forward, 2.5f);
                }
                else if (target is InteractableObject io)
                {
                    io.OnHit(damage, target.GlobalPosition, forward, caster);
                }
            }, caster.Team);

            GD.Print($"[WarriorAbilities] Intercept impact: {damage:F0} dmg (60% of {stats.AttackDamage} AD) at {center}");
        };

        if (caster.CurrentTarget != null)
        {
            GD.Print($"[WarriorAbilities] Intercept towards {caster.CurrentTarget.Name}");
        }
        else
        {
            GD.Print($"[WarriorAbilities] Intercept (no target — dash forward)");
        }
    }

    private static void CastDemoralizingShout(PlayerController caster, CharacterModelManager modelMgr)
    {
        // Play power up animation
        modelMgr?.PlayAnimation("PowerUp");

        // 6m radius AoE taunt + 15% AP reduction
        TargetingHelper.PerformAoEAction(caster, caster.GlobalPosition, 6.0f, (target) =>
        {
            var monster = target as Monsters ?? target.GetParent() as Monsters ?? FindAncestor<Monsters>(target);
            var monsterPart = target as MonsterPart ?? target.GetNodeOrNull<MonsterPart>("MonsterPart");

            if (monster != null)
            {
                monster.ApplyTaunt(caster, 3.0f);
                monster.ApplyDebuff(0.15f, 8.0f);
            }
            else if (monsterPart != null)
            {
                var owner = FindAncestor<Monsters>(target);
                owner?.ApplyTaunt(caster, 3.0f);
                owner?.ApplyDebuff(0.15f, 8.0f);
            }
        }, caster.Team);

        // Add visual pulse effect
        var melee = caster.GetNodeOrNull<MeleeSystem>("MeleeSystem");
        if (melee != null)
        {
            melee.EmitSignal(MeleeSystem.SignalName.PowerSlamTriggered, caster.GlobalPosition, caster.PlayerIndex, new Color(1, 1, 1, 1), 4.0f);
        }

        GD.Print($"[WarriorAbilities] Demoralizing Shout at {caster.GlobalPosition}");
    }

    private static void CastAvatarOfWar(PlayerController caster, CharacterModelManager modelMgr)
    {
        // Play casting/power up animation for the buff
        modelMgr?.PlayAnimation("Casting");

        // Self-buff: 30% Lifesteal + CC Immunity for 10s
        // Red pulse is handled automatically by PlayerController.ProcessBuffs while lifesteal is active
        caster.ApplyLifesteal(0.3f, 10.0f);
        caster.ApplyCCImmunity(10.0f);

        GD.Print($"[WarriorAbilities] Avatar of War activated!");
    }
    private static T FindAncestor<T>(Node node) where T : class
    {
        Node parent = node.GetParent();
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = parent.GetParent();
        }
        return null;
    }
}
