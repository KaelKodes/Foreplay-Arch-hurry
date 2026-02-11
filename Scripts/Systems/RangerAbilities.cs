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
                archery.SetNextShotFlat(true);
                archery.QuickFire(0f);
                // +50% Haste (25 out of 50 cap) for 5 seconds, green pulse on player
                caster.ApplyHasteBuff(25, 5.0f);
                break;

            case 1: // Piercing Shot (2) - Slot 1
                // Sync the player's current target (smart/tab) into the archery system
                // so ExecuteShot auto-aims at it. If no target, fires at crosshair.
                if (caster.CurrentTarget != null)
                {
                    archery.SetTarget(caster.CurrentTarget);
                }
                archery.SetNextShotPiercing(true);
                archery.SetNextShotFlat(true);
                archery.QuickFire(0f);
                break;

            case 2: // Rain of Arrows (3) - Slot 2
                archery.PlayAbilityAnimation(true);
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
            // No target: raycast from crosshair (screen center) to find aim point
            var cam = caster.GetViewport().GetCamera3D();
            if (cam != null)
            {
                var screenCenter = caster.GetViewport().GetVisibleRect().Size / 2;
                Vector3 rayFrom = cam.ProjectRayOrigin(screenCenter);
                Vector3 rayDir = cam.ProjectRayNormal(screenCenter);

                // Raycast to find ground/terrain
                var spaceState = caster.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayFrom + rayDir * 100f);
                query.CollideWithBodies = true;
                query.Exclude = new Godot.Collections.Array<Rid> { caster.GetRid() };
                var result = spaceState.IntersectRay(query);

                if (result.Count > 0)
                {
                    targetPos = (Vector3)result["position"];
                }
                else
                {
                    // No hit: place 15m along camera forward at player height
                    targetPos = caster.GlobalPosition + rayDir * 15.0f;
                    targetPos.Y = caster.GlobalPosition.Y;
                }
            }
            else
            {
                // Fallback: 6m in front of player
                Vector3 fwd = -caster.GlobalTransform.Basis.Z;
                fwd.Y = 0;
                targetPos = caster.GlobalPosition + fwd.Normalized() * 6.0f;
            }
        }

        // Create the effect object (it's a pure code Node3D)
        var rain = new RainOfArrowsEffect();
        caster.GetTree().CurrentScene.AddChild(rain);
        rain.GlobalPosition = targetPos;

        // Configure and Start
        // Damage scaling: 40% of Strength per arrow?
        float damage = archery.PlayerStats.Strength * 0.4f;
        rain.Start(archery.ArrowScene, damage, caster);

        GD.Print($"[RangerAbilities] Rain of Arrows cast at {targetPos}");
    }

    private static void CastVault(PlayerController caster)
    {
        caster.PerformVault();
        GD.Print($"[RangerAbilities] Vault executed");
    }
}
