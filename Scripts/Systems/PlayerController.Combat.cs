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
        if (ability != null)
        {
            ability.Execute(this);
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
