using Godot;
using Archery;

namespace Archery;

/// <summary>
/// Static helper class for player animation updates.
/// Extracted from PlayerController to reduce file size.
/// </summary>
public static class PlayerAnimations
{
    /// <summary>
    /// Updates the animation tree based on player state and velocity.
    /// </summary>
    public static void UpdateAnimations(
        AnimationTree animTree,
        PlayerController player,
        MeleeSystem meleeSystem,
        ArcherySystem archerySystem,
        CharacterModelManager modelManager,
        float moveSpeed,
        bool isJumping,
        Vector3 velocity,
        ref DrawStage lastArcheryStage)
    {
        // 1. Calculate Common Flags
        float speed = new Vector2(velocity.X, velocity.Z).Length();
        bool isRanger = player.CurrentModelId.ToLower() == "ranger" || player.CurrentModelId.ToLower() == "erika";
        bool isRPG = ToolManager.Instance != null && ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;

        DrawStage archeryStage = (DrawStage)player.SynchronizedArcheryStage;
        bool isArcheryAction = (archeryStage != DrawStage.Idle);
        bool isArchery = (player.CurrentState == PlayerState.CombatArcher) || (isRPG && isRanger && isArcheryAction);

        bool currentlySprinting = player.IsLocal ? (Input.IsKeyPressed(Key.Shift) && speed > 0.1f) : player.IsSprinting;
        if (player.IsLocal) player.IsSprinting = currentlySprinting;

        // Custom model logic (Early Return)
        if (modelManager != null)
        {
            var registry = CharacterRegistry.Instance;
            var model = registry?.GetModel(modelManager.CurrentModelId);
            if (model != null && model.IsCustomSkeleton)
            {
                bool customSwinging = false;
                if (meleeSystem != null)
                {
                    var sState = (MeleeSystem.SwingState)player.SynchronizedMeleeStage;
                    if (sState == MeleeSystem.SwingState.Finishing || sState == MeleeSystem.SwingState.Executing)
                        customSwinging = true;
                }

                bool isFiring = (isArchery && archerySystem != null &&
                                (archeryStage == DrawStage.Executing || archeryStage == DrawStage.ShotComplete));

                float lockedPower = (meleeSystem != null) ? meleeSystem.LockedPower : 0f;
                modelManager.UpdateCustomAnimations(speed > 0.1f, currentlySprinting, isJumping, customSwinging, isFiring, lockedPower);
                return;
            }
        }

        if (animTree == null) return;

        // 2. Calculate Movement Vectors
        Vector3 localVel = player.GlobalTransform.Basis.Inverse() * velocity;
        float moveX = localVel.X / moveSpeed;
        float moveY = -localVel.Z / moveSpeed;
        float normalizedSpeed = speed / moveSpeed;

        if (currentlySprinting)
        {
            normalizedSpeed *= 2.0f;
            moveX *= 2.0f;
            moveY *= 2.0f;
        }

        // 3. Set Base Parameters
        animTree.Set("parameters/conditions/is_moving", speed > 0.1f);
        animTree.Set("parameters/conditions/is_idle", speed <= 0.1f);
        animTree.Set("parameters/conditions/is_sprinting", currentlySprinting);
        animTree.Set("parameters/conditions/is_not_sprinting", !currentlySprinting);
        animTree.Set("parameters/move_speed", normalizedSpeed);
        animTree.Set("parameters/conditions/is_on_floor", player.IsOnFloor() && !isJumping);
        animTree.Set("parameters/conditions/is_jumping", isJumping || (!player.IsOnFloor() && velocity.Y > 0));

        // Drive Normal BlendSpaces
        var blendPos = new Vector2(moveX, moveY);
        animTree.Set("parameters/Run/blend_position", blendPos);
        animTree.Set("parameters/Sprint/blend_position", blendPos);
        animTree.Set("parameters/MeleeRun/blend_position", blendPos);
        animTree.Set("parameters/MeleeSprint/blend_position", blendPos);

        // 4. Melee Logic
        bool isSwinging = false;
        if (player.CurrentState == PlayerState.CombatMelee && meleeSystem != null)
        {
            var sState = (MeleeSystem.SwingState)player.SynchronizedMeleeStage;
            if (sState == MeleeSystem.SwingState.Finishing || sState == MeleeSystem.SwingState.Executing)
            {
                isSwinging = true;
                animTree.Set("parameters/MeleeAttack/WindupSpeed/scale", 1.0f);

                string attackType = "Normal";
                if (meleeSystem.IsTripleSwing) attackType = "Triple";
                else if (meleeSystem.IsPerfectSlam) attackType = "Perfect";

                animTree.Set("parameters/MeleeAttack/AttackType/transition_request", attackType);
            }
        }
        animTree.Set("parameters/conditions/is_swinging", isSwinging);
        animTree.Set("parameters/conditions/is_not_swinging", !isSwinging);

        // 5. Archery & State Conditions
        bool isMelee = player.CurrentState == PlayerState.CombatMelee;
        animTree.Set("parameters/conditions/is_archery", isArchery);
        animTree.Set("parameters/conditions/is_melee", isMelee);
        animTree.Set("parameters/conditions/is_not_melee", !isMelee);
        animTree.Set("parameters/conditions/is_not_archery", !isArchery);
        animTree.Set("parameters/conditions/is_vaulting", player.SynchronizedVaulting);

        if (isArchery && archerySystem != null)
        {
            // Update the Aim BlendSpace (includes walking animations)
            animTree.Set("parameters/ArcheryAim/blend_position", blendPos);

            bool justFired = (archeryStage == DrawStage.Executing && lastArcheryStage != DrawStage.Executing) ||
                             (archeryStage == DrawStage.ShotComplete && lastArcheryStage != DrawStage.ShotComplete && lastArcheryStage != DrawStage.Executing);

            animTree.Set("parameters/conditions/is_firing", justFired);
            animTree.Set("parameters/conditions/is_melee_basic", false);

            lastArcheryStage = archeryStage;
        }
    }
}
