using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void PerformBasicAttack()
    {
        // In RPG mode, non-Rangers stay in WalkMode — still trigger melee
        if (_meleeSystem != null && (CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.WalkMode))
        {
            _meleeSystem.StartCharge();
            _meleeSystem.ExecuteAttack(0f);
        }
        else if (CurrentState == PlayerState.CombatArcher && _archerySystem != null)
            _archerySystem.ExecuteAttack(0f);
    }

    private void HandleCombatCharge(double delta)
    {
        if (!_isChargingAttack)
        {
            _chargeBar?.Reset();
            return;
        }

        _attackHoldTimer += (float)delta;

        // Update 3D bar
        _chargeBar?.UpdateValue(_attackHoldTimer);

        // Cap visual at 1.5s for the SYSTEM percent (which signals events)
        float chargePercent = Mathf.Clamp(_attackHoldTimer / 1.5f, 0f, 1f) * 100f;

        if ((CurrentState == PlayerState.CombatMelee || (CurrentState == PlayerState.WalkMode && _meleeSystem != null)) && _meleeSystem != null)
            _meleeSystem.UpdateChargeProgress(chargePercent);
        else if (CurrentState == PlayerState.CombatArcher && _archerySystem != null)
            _archerySystem.UpdateChargeProgress(chargePercent);
    }

    private void TriggerAbility(int index)
    {
        if (_abilities == null || !_abilities.ContainsKey(index)) return;

        var ability = _abilities[index];
        if (ability == null) return;

        // ── Cooldown Gate ─────────────────────────────────────────
        if (ability.IsOnCooldown)
        {
            GD.Print($"[Ability] {ability.AbilityName ?? $"Slot {index}"} on cooldown ({ability.CooldownRemaining:F1}s remaining)");
            return;
        }
        if (_abilityBusyTimer > 0f)
        {
            GD.Print($"[Ability] Busy (animation lock {_abilityBusyTimer:F1}s remaining)");
            return;
        }

        // Logic based on AbilityType (RoR2 inspired)
        switch (ability.Type)
        {
            case AbilityType.Auto:
                // Auto-targeting: snaps rotation to CurrentTarget if available
                if (CurrentTarget != null)
                {
                    // Look at target before executing (visual snap)
                    Vector3 toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
                    toTarget.Y = 0;
                    if (toTarget.LengthSquared() > 0.1f)
                    {
                        Quaternion targetRot = Basis.LookingAt(-toTarget.Normalized(), Vector3.Up).GetRotationQuaternion();
                        GlobalBasis = new Basis(targetRot);
                    }
                }
                ability.Execute(this);
                break;

            case AbilityType.Instant:
                ability.Execute(this);
                break;

            case AbilityType.Aim:
                ability.Execute(this);
                break;

            case AbilityType.Aura:
                ability.Execute(this);
                break;
        }

        // ── Start Cooldown & Animation Lock ───────────────────────
        var stats = _archerySystem?.PlayerStats;
        float cdMultiplier = stats?.AbilityCooldownMultiplier ?? 1.0f;
        ability.StartCooldown(cdMultiplier);
        _abilityBusyTimer = ability.CurrentCooldown;

        // Notify UI
        EmitSignal(SignalName.AbilityUsed, index, ability.CurrentCooldown);
    }


    private void UpdateFluidTargeting(float delta)
    {
        if (!IsLocal) return;

        bool alliesOnly = Input.IsKeyPressed(Key.Ctrl);

        // Throttled Broad-Phase Discovery (Targeting Ping)
        _targetPingTimer -= delta;
        if (_targetPingTimer <= 0)
        {
            _targetPingTimer = TargetPingInterval;
            _cachedPotentialTargets = TargetingHelper.GetSortedTargets(this, Team, alliesOnly, 100f);
        }

        // Sticky Targeting: Locked target persists even if looking away
        // (Fluid target still drops if looking away)
        if (_hardLockTarget != null && TargetingHelper.IsTargetDead(_hardLockTarget))
        {
            _hardLockTarget = null;
            if (_archerySystem != null) _archerySystem.ClearTarget();
            GD.Print("[PlayerController] Hard Lock cleared (Target Died)");
        }

        if (_fluidTarget != null && _hardLockTarget == null)
        {
            if (!TargetingHelper.IsTargetInCone(this, GetViewport(), _fluidTarget, 15.0f))
            {
                _fluidTarget = null;
            }
        }

        // Search for new fluid target if needed
        if (_hardLockTarget != null)
        {
            // FORCE smart target to be the hard lock target
            _fluidTarget = _hardLockTarget;
        }
        else
        {
            _fluidTarget = TargetingHelper.GetFluidTargetWithList(this, GetViewport(), _cachedPotentialTargets);
        }

        // Update Visual Feedback (Targeting Ring)
        Node3D currentTarget = CurrentTarget;
        bool lockStateChanged = _hardLockTarget != _lastLockTarget;

        if (currentTarget != _lastTarget || lockStateChanged)
        {
            bool isLocked = currentTarget == _hardLockTarget;
            if (GodotObject.IsInstanceValid(_lastTarget) && _lastTarget is InteractableObject lastIo) lastIo.SetSelected(false);
            if (GodotObject.IsInstanceValid(currentTarget) && currentTarget is InteractableObject currIo) currIo.SetSelected(true, isLocked);

            _lastTarget = currentTarget;
            _lastLockTarget = _hardLockTarget;

            // Sync ArcherySystem target for accurate shot logic
            if (_archerySystem != null) _archerySystem.SetTarget(currentTarget);
        }

        // Update camera with current target (visual feedback or bias if needed, but bias was removed)
        if (_camera != null)
        {
            _camera.SetLockedTarget(CurrentTarget);
        }
    }

    private void HandleTargetingHotkeys()
    {
        bool matchesFocusNext = Input.IsActionJustPressed("ui_focus_next");
        bool matchesFocusPrev = Input.IsActionJustPressed("ui_focus_prev");

        if (matchesFocusNext || matchesFocusPrev)
        {
            // Tab (FocusNext) cycles Enemies, Shift+Tab (FocusPrev) cycles Allies
            bool allies = matchesFocusPrev;
            _hardLockTarget = TargetingHelper.GetNextTabTarget(this, _hardLockTarget, allies, attackerTeam: Team);

            GD.Print($"[PlayerController] Hard Lock {(allies ? "(Ally)" : "(Enemy)")}: {(_hardLockTarget != null ? _hardLockTarget.Name : "Cleared")}");

            // Sync with ArcherySystem immediately
            if (_archerySystem != null) _archerySystem.SetTarget(_hardLockTarget);
        }

        if (Input.IsActionJustPressed("ui_cancel")) // Escape or similar to clear lock
        {
            _hardLockTarget = null;
            if (_archerySystem != null) _archerySystem.ClearTarget();
        }
    }

    private void OnPowerSlamTriggered(Vector3 position, int playerIndex, Color color, float radius)
    {
        var scene = GD.Load<PackedScene>("res://Scenes/VFX/Shockwave.tscn");
        if (scene != null)
        {
            var wave = scene.Instantiate<Node3D>();
            // Add to world root so it doesn't move with player
            GetTree().CurrentScene.AddChild(wave);
            wave.GlobalPosition = position;

            // Set color based on override or player index
            if (wave is Shockwave sw)
            {
                // If the color provided is strictly White (default), use the player's color instead.
                // This preserves the team-color identity for standard warrior slams.
                Color finalColor = color;
                if (color == new Color(1, 1, 1, 1))
                {
                    finalColor = TargetingHelper.GetPlayerColor(playerIndex);
                }

                sw.SetColor(finalColor);
                sw.SetRadius(radius);
            }
        }
    }

    private void InitializeAbilities(string classId)
    {
        // Cleanup old
        foreach (var ability in _abilities.Values) ability.QueueFree();
        _abilities.Clear();

        // Per-class, per-slot cooldowns (synced to animation durations)
        float[] cooldowns = GetAbilityCooldowns(classId);

        for (int i = 0; i < 4; i++)
        {
            var placeholder = new GenericHeroAbility();
            placeholder.AbilitySlot = i;
            placeholder.BaseCooldown = cooldowns[i];
            AddChild(placeholder);
            _abilities[i] = placeholder;
        }
    }

    /// <summary>
    /// Returns 4 cooldown values (one per ability slot) tuned to animation durations.
    /// </summary>
    private static float[] GetAbilityCooldowns(string classId)
    {
        return (classId?.ToLower()) switch
        {
            // Ranger: Rapid Fire, Piercing Shot, Rain of Arrows, Vault
            "ranger" => new float[] { 1.2f, 1.5f, 2.0f, 0.8f },
            // Warrior: Shield Slam, Intercept, Demoralizing Shout, Avatar of War
            "warrior" => new float[] { 1.5f, 1.0f, 2.0f, 3.0f },
            // Cleric: High Remedy, Celestial Buff, Judgement, Divine Intervention
            "cleric" => new float[] { 2.0f, 2.0f, 1.5f, 3.0f },
            // Necromancer: Lifetap, Plague of Darkness, Summon Undead, Lich Form
            "necromancer" => new float[] { 1.2f, 2.0f, 3.0f, 5.0f },
            _ => new float[] { 1.5f, 1.5f, 1.5f, 1.5f },
        };
    }

}
