using Godot;

namespace Archery;

/// <summary>
/// Temporary wrapper to keep existing Ranger logic working while transitioning to Step 3.
/// </summary>
public partial class GenericHeroAbility : HeroAbilityBase
{
    public override void Execute(PlayerController caster)
    {
        string classId = caster.CurrentModelId?.ToLower() ?? "";

        switch (classId)
        {
            case "ranger":
                RangerAbilities.ExecuteAbility(caster, AbilitySlot);
                break;
            case "warrior":
                WarriorAbilities.ExecuteAbility(caster, AbilitySlot);
                break;
            default:
                GD.Print($"[Ability] {caster.CurrentModelId} ability {AbilitySlot + 1} triggered (No implementation yet)");
                break;
        }
    }
}
