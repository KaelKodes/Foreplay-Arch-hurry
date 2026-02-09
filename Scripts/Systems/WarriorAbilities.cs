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

        // Small delay to match animation strike (reduced from 0.3s for faster feel)
        caster.GetTree().CreateTimer(0.2f).Timeout += () =>
        {
            Vector3 forward = -caster.GlobalTransform.Basis.Z;
            // Shifted to 1.5u forward and 2.0u radius for a tight melee feel
            Vector3 center = caster.GlobalPosition + (forward * 1.5f) + (Vector3.Up * 0.5f);
            float radius = 2.0f;

            TargetingHelper.PerformAoEAction(caster, center, radius, (target) =>
            {
                // Robust target identification: 
                // 1. Check if direct hit on MonsterPart or has one attached
                // 2. Check if direct hit on Monsters
                // 3. Search ancestors for Monsters (for child hitboxes)
                var monsterPart = target as MonsterPart ?? target.GetNodeOrNull<MonsterPart>("MonsterPart");
                var monster = target as Monsters ?? target.GetParent() as Monsters ?? FindAncestor<Monsters>(target);

                if (monsterPart != null)
                {
                    monsterPart.OnHit(25f, target.GlobalPosition, forward, caster);
                    // Also apply stun/knockback to the owner via derived monster ref
                    monster?.ApplyStun(1.5f);
                }
                else if (monster != null)
                {
                    monster.OnHit(25f, target.GlobalPosition, forward, caster);
                    monster.ApplyStun(1.5f);
                }
                else if (target is InteractableObject io)
                {
                    io.OnHit(25f, target.GlobalPosition, forward, caster);
                }
            }, caster.Team);

            GD.Print($"[WarriorAbilities] Shield Slam (Kick) executed at {center} (Radius: {radius})");
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
            Vector3 forward = caster.GlobalTransform.Basis.Z;
            Vector3 center = caster.GlobalPosition + forward * 1.5f + Vector3.Up * 0.5f;
            float radius = 2.5f;

            TargetingHelper.PerformAoEAction(caster, center, radius, (target) =>
            {
                float damage = 20f;
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

            GD.Print($"[WarriorAbilities] Intercept impact check at {center}");
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
                // This case is largely covered by the monster search above, but kept for absolute safety
                var owner = FindAncestor<Monsters>(target);
                owner?.ApplyTaunt(caster, 3.0f);
                owner?.ApplyDebuff(0.15f, 8.0f);
            }
        }, caster.Team);

        // Add visual pulse effect
        var melee = caster.GetNodeOrNull<MeleeSystem>("MeleeSystem");
        if (melee != null)
        {
            melee.EmitSignal(MeleeSystem.SignalName.PowerSlamTriggered, caster.GlobalPosition, caster.PlayerIndex);
        }

        GD.Print($"[WarriorAbilities] Demoralizing Shout at {caster.GlobalPosition}");
    }

    private static void CastAvatarOfWar(PlayerController caster, CharacterModelManager modelMgr)
    {
        // Play casting/power up animation for the buff
        modelMgr?.PlayAnimation("Casting");

        // Self-buff: 30% Lifesteal + CC Immunity for 10s
        caster.ApplyLifesteal(0.3f, 10.0f);
        caster.ApplyCCImmunity(10.0f);

        // Visual: Make the Warrior glow red
        modelMgr?.ApplyGlow(new Color(1.5f, 0.2f, 0.2f, 1.0f), 10.0f);

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
