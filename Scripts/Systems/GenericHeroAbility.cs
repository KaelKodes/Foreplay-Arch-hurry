using Godot;

namespace Archery;

/// <summary>
/// Temporary wrapper to keep existing Ranger logic working while transitioning to Step 3.
/// </summary>
public partial class GenericHeroAbility : HeroAbilityBase
{
    public int AbilitySlot { get; set; }

    public override void Execute(PlayerController caster)
    {
        bool isRanger = caster.CurrentModelId.ToLower() == "ranger";

        if (isRanger)
        {
            if (AbilitySlot < 3)
            {
                // Accessing the local ArcherySystem
                caster.GetNodeOrNull<ArcherySystem>("ArcherySystem")?.QuickFire(0f);
            }
            else if (AbilitySlot == 3)
            {
                // PerformVault is now public in PlayerController
                caster.PerformVault();
            }
        }
        else
        {
            GD.Print($"[Ability] {caster.CurrentModelId} ability {AbilitySlot + 1} triggered (No implementation yet)");
        }
    }
}
