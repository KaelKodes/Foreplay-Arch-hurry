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
        // For custom-skeleton heroes, skip AnimationTree entirely — go straight to direct PlayAnimation
        if (modelManager != null)
        {
            var registry = CharacterRegistry.Instance;
            var model = registry?.GetModel(modelManager.CurrentModelId);
            if (model != null && model.IsCustomSkeleton)
            {
                float customSpeed = new Vector2(velocity.X, velocity.Z).Length();
                bool customSprinting = player.IsLocal ? (Input.IsKeyPressed(Key.Shift) && customSpeed > 0.1f) : player.IsSprinting;
                if (player.IsLocal) player.IsSprinting = customSprinting;

                bool customSwinging = false;
                if (meleeSystem != null)
                {
                    var sState = (MeleeSystem.SwingState)player.SynchronizedMeleeStage;
                    if (sState == MeleeSystem.SwingState.Finishing || sState == MeleeSystem.SwingState.Executing)
                        customSwinging = true;
                }

                bool isFiring = (player.CurrentState == PlayerState.CombatArcher && archerySystem != null &&
                                ((DrawStage)player.SynchronizedArcheryStage == DrawStage.Executing ||
                                 (DrawStage)player.SynchronizedArcheryStage == DrawStage.ShotComplete));

                float lockedPower = (meleeSystem != null) ? meleeSystem.LockedPower : 0f;
                modelManager.UpdateCustomAnimations(customSpeed > 0.1f, customSprinting, isJumping, customSwinging, isFiring, lockedPower);
                return;
            }
        }

        if (animTree == null) return;

        // 1. Calculate Horizontal Movement
        Vector3 localVel = player.GlobalTransform.Basis.Inverse() * velocity;

        float moveX = localVel.X / moveSpeed;
        float moveY = -localVel.Z / moveSpeed;

        float speed = new Vector2(velocity.X, velocity.Z).Length();
        float normalizedSpeed = speed / moveSpeed;

        // Sprint check
        bool currentlySprinting = player.IsLocal ? (Input.IsKeyPressed(Key.Shift) && speed > 0.1f) : player.IsSprinting;
        if (player.IsLocal) player.IsSprinting = currentlySprinting;

        if (currentlySprinting)
        {
            normalizedSpeed *= 2.0f;
            moveX *= 2.0f;
            moveY *= 2.0f;
        }

        // 2. Set Parameters
        animTree.Set("parameters/conditions/is_moving", speed > 0.1f);
        animTree.Set("parameters/conditions/is_idle", speed <= 0.1f);
        animTree.Set("parameters/conditions/is_sprinting", currentlySprinting);
        animTree.Set("parameters/conditions/is_not_sprinting", !currentlySprinting);
        animTree.Set("parameters/move_speed", normalizedSpeed);

        // Drive the BlendSpace2Ds
        var blendPos = new Vector2(moveX, moveY);
        animTree.Set("parameters/Run/blend_position", blendPos);
        animTree.Set("parameters/Sprint/blend_position", blendPos);
        animTree.Set("parameters/MeleeRun/blend_position", blendPos);
        animTree.Set("parameters/MeleeSprint/blend_position", blendPos);

        // 3. Melee Attack Logic
        bool isSwinging = false;
        if (player.CurrentState == PlayerState.CombatMelee && meleeSystem != null)
        {
            var sState = (MeleeSystem.SwingState)player.SynchronizedMeleeStage;

            // Drawing = bar fills, no animation yet
            // Animation only starts on second click (Finishing/Executing)
            if (sState == MeleeSystem.SwingState.Finishing || sState == MeleeSystem.SwingState.Executing)
            {
                isSwinging = true;
                animTree.Set("parameters/MeleeAttack/WindupSpeed/scale", 1.0f);

                string attackType = "Normal";
                if (meleeSystem.IsTripleSwing) attackType = "Triple";
                else if (meleeSystem.IsPerfectSlam) attackType = "Perfect";

                animTree.Set("parameters/MeleeAttack/AttackType/transition_request", attackType);
                GD.Print($"[AnimationDebug] Erika attacking with type: {attackType}");
            }
        }

        animTree.Set("parameters/conditions/is_swinging", isSwinging);
        animTree.Set("parameters/conditions/is_not_swinging", !isSwinging);

        // 4. States & Conditions
        bool isMelee = player.CurrentState == PlayerState.CombatMelee;
        bool isArchery = player.CurrentState == PlayerState.CombatArcher;

        // RPG Mode Overlay: Show archery anim if firing/executing even in WalkMode
        bool isRanger = player.CurrentModelId.ToLower() == "ranger" || player.CurrentModelId.ToLower() == "erika";
        bool isRPG = ToolManager.Instance != null && ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;

        DrawStage stage = (DrawStage)player.SynchronizedArcheryStage;
        bool isArcheryAction = (stage != DrawStage.Idle);

        if (isRPG && isRanger && isArcheryAction)
        {
            isArchery = true;
        }

        animTree.Set("parameters/conditions/is_on_floor", player.IsOnFloor() && !isJumping);
        animTree.Set("parameters/conditions/is_jumping", isJumping || (!player.IsOnFloor() && velocity.Y > 0));
        animTree.Set("parameters/conditions/is_archery", isArchery);
        animTree.Set("parameters/conditions/is_melee", isMelee);
        animTree.Set("parameters/conditions/is_not_melee", !isMelee);
        animTree.Set("parameters/conditions/is_not_archery", !isArchery);

        if (player.CurrentState == PlayerState.CombatArcher && archerySystem != null)
        {
            animTree.Set("parameters/ArcheryAim/blend_position", blendPos);

            var currentStage = (DrawStage)player.SynchronizedArcheryStage;
            bool justFired = (currentStage == DrawStage.Executing && lastArcheryStage != DrawStage.Executing) ||
                             (currentStage == DrawStage.ShotComplete && lastArcheryStage == DrawStage.Aiming);
            animTree.Set("parameters/conditions/is_firing", justFired);

            lastArcheryStage = currentStage;
        }

        // 5. Custom Model Logic
        if (modelManager != null)
        {
            var registry = CharacterRegistry.Instance;
            var model = registry?.GetModel(modelManager.CurrentModelId);
            if (model != null && model.IsCustomSkeleton)
            {
                bool isFiring = (player.CurrentState == PlayerState.CombatArcher && archerySystem != null &&
                                ((DrawStage)player.SynchronizedArcheryStage == DrawStage.Executing ||
                                 (DrawStage)player.SynchronizedArcheryStage == DrawStage.ShotComplete));

                float lockedPower = (meleeSystem != null) ? meleeSystem.LockedPower : 0f;
                modelManager.UpdateCustomAnimations(speed > 0.1f, currentlySprinting, isJumping, isSwinging, isFiring, lockedPower);
            }
        }
    }
}
