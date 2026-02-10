using Godot;
using Archery;

/// <summary>
/// Static helper to handle Necromancer-specific ability animations.
/// Slot 0: Standing 1H Magic Attack 01 (Kick)
/// Slot 1: Standing 2H Magic Area Attack 01 (MeleeAttack3)
/// Slot 2: Standing 2H Magic Attack 03 (PowerUp)
/// Slot 3: Standing 2H Magic Area Attack 02 (Casting)
/// </summary>
public static class NecromancerAbilities
{
    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var modelMgr = caster.GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        switch (slot)
        {
            case 0:
                modelMgr?.PlayAnimation("Kick");
                GD.Print("[NecromancerAbilities] Slot 1 (Kick) triggered");
                break;

            case 1:
                modelMgr?.PlayAnimation("MeleeAttack3");
                GD.Print("[NecromancerAbilities] Slot 2 (MeleeAttack3) triggered");
                break;

            case 2:
                modelMgr?.PlayAnimation("PowerUp");
                GD.Print("[NecromancerAbilities] Slot 3 (PowerUp) triggered");
                break;

            case 3:
                modelMgr?.PlayAnimation("Casting");
                GD.Print("[NecromancerAbilities] Slot 4 (Casting) triggered");
                break;
        }
    }
}
