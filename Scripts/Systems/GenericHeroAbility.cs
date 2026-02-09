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
            var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");

            switch (AbilitySlot)
            {
                case 0: // Rapid Fire
                    archery?.QuickFire(0f);
                    break;
                case 1: // Piercing Shot
                    if (archery != null)
                    {
                        archery.SetNextShotPiercing(true);
                        archery.QuickFire(0f);
                    }
                    break;
                case 2: // Rain of Arrows
                    // TODO: Implement AoE Rain logic
                    archery?.QuickFire(0f);
                    break;
                case 3: // Vault
                    caster.PerformVault();
                    break;
            }
        }
        else
        {
            GD.Print($"[Ability] {caster.CurrentModelId} ability {AbilitySlot + 1} triggered (No implementation yet)");
        }
    }
}
