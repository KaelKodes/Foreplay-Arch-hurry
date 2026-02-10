using Godot;
using Archery;

/// <summary>
/// Static helper to handle Cleric-specific greatsword ability animations.
/// Slot 0: great sword casting (CastingSlot1)
/// Slot 1: great sword idle (2) (IdleSlot2)
/// Slot 2: great sword high spin attack (SpinSlot3)
/// Slot 3: great sword casting (CastingSlot4)
/// </summary>
public static class ClericAbilities
{
    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var modelMgr = caster.GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        switch (slot)
        {
            case 0:
                modelMgr?.PlayAnimation("CastingSlot1");
                GD.Print("[ClericAbilities] Slot 1 (Casting) triggered");
                break;

            case 1:
                modelMgr?.PlayAnimation("IdleSlot2");
                GD.Print("[ClericAbilities] Slot 2 (Idle 2) triggered");
                break;

            case 2:
                modelMgr?.PlayAnimation("SpinSlot3");
                GD.Print("[ClericAbilities] Slot 3 (High Spin Attack) triggered");
                break;

            case 3:
                modelMgr?.PlayAnimation("CastingSlot4");
                GD.Print("[ClericAbilities] Slot 4 (Casting) triggered");
                break;
        }
    }
}
