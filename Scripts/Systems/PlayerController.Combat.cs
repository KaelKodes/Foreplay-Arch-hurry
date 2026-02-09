using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void PerformBasicAttack()
    {
        if (CurrentState == PlayerState.CombatMelee && _meleeSystem != null)
            _meleeSystem.ExecuteAttack(0f);
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

        if (CurrentState == PlayerState.CombatMelee && _meleeSystem != null)
            _meleeSystem.UpdateChargeProgress(chargePercent);
        else if (CurrentState == PlayerState.CombatArcher && _archerySystem != null)
            _archerySystem.UpdateChargeProgress(chargePercent);
    }

    private void TriggerAbility(int index)
    {
        if (_abilities == null || !_abilities.ContainsKey(index)) return;

        var ability = _abilities[index];
        if (ability == null) return;

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
                // Instant: No auto-snap, fires where crosshair/camera is looking
                ability.Execute(this);
                break;

            case AbilityType.Aim:
                // Aim: Starts a hold phase (handled by Input + Execute release)
                ability.Execute(this);
                break;

            case AbilityType.Aura:
                // Aura: Casts around self
                ability.Execute(this);
                break;
        }
    }

    private void UpdateFluidTargeting()
    {
        if (!IsLocal) return;

        // Sticky Targeting: If we have a target (hard or soft), check if it's still in the cone
        if (_hardLockTarget != null)
        {
            if (!TargetingHelper.IsTargetInCone(this, GetViewport(), _hardLockTarget, 15.0f))
            {
                _hardLockTarget = null;
                GD.Print("[PlayerController] Hard Lock dropped (looked away)");
            }
        }

        if (_fluidTarget != null && _hardLockTarget == null)
        {
            if (!TargetingHelper.IsTargetInCone(this, GetViewport(), _fluidTarget, 15.0f))
            {
                _fluidTarget = null;
            }
        }

        // Search for new fluid target if needed
        if (_hardLockTarget == null)
        {
            bool alliesOnly = Input.IsKeyPressed(Key.Ctrl);
            _fluidTarget = TargetingHelper.GetFluidTarget(this, GetViewport(), alliesOnly: alliesOnly, attackerTeam: Team);
        }

        // Update Visual Feedback (Targeting Ring)
        Node3D current = CurrentTarget;
        if (current != _lastTarget)
        {
            if (_lastTarget is InteractableObject lastIo) lastIo.SetSelected(false);
            if (current is InteractableObject currIo) currIo.SetSelected(true);
            _lastTarget = current;
        }

        // Update camera with current target (visual feedback or bias if needed, but bias was removed)
        if (_camera != null)
        {
            _camera.SetLockedTarget(CurrentTarget);
        }
    }

    private void HandleTargetingHotkeys()
    {
        if (Input.IsActionJustPressed("ui_focus_next")) // Tab
        {
            bool allies = Input.IsKeyPressed(Key.Ctrl);
            _hardLockTarget = TargetingHelper.GetNextTabTarget(this, _hardLockTarget, allies, attackerTeam: Team);
            GD.Print($"[PlayerController] Hard Lock{(allies ? " (Ally)" : "")}: {(_hardLockTarget != null ? _hardLockTarget.Name : "Cleared")}");
        }

        if (Input.IsActionJustPressed("ui_cancel")) // Escape or similar to clear lock
        {
            _hardLockTarget = null;
        }
    }

    private void OnPowerSlamTriggered(Vector3 position, int playerIndex)
    {
        var scene = GD.Load<PackedScene>("res://Scenes/VFX/Shockwave.tscn");
        if (scene != null)
        {
            var wave = scene.Instantiate<Node3D>();
            // Add to world root so it doesn't move with player
            GetTree().CurrentScene.AddChild(wave);
            wave.GlobalPosition = position;

            // Set color based on player index (syncs across multiplayer)
            if (wave is Shockwave sw)
            {
                Color playerColor = TargetingHelper.GetPlayerColor(playerIndex);
                sw.SetColor(playerColor);
            }
        }
    }

    private void InitializeAbilities(string classId)
    {
        // Cleanup old
        foreach (var ability in _abilities.Values) ability.QueueFree();
        _abilities.Clear();

        for (int i = 0; i < 4; i++)
        {
            var placeholder = new GenericHeroAbility();
            placeholder.AbilitySlot = i;
            AddChild(placeholder);
            _abilities[i] = placeholder;
        }
    }
}
