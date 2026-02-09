using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void OnToolChanged(int toolInt)
    {
        if (!IsLocal) return;
        SynchronizedTool = toolInt;
    }

    private void OnHotbarModeChanged(int modeInt)
    {
        if (!IsLocal) return;

        var toolManager = ToolManager.Instance;
        bool isRPG = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        if (isRPG)
        {
            string heroClass = CurrentModelId;
            toolManager?.UpdateRPGAbilities(heroClass);

            if (heroClass.ToLower() == "ranger")
            {
                _archerySystem?.PrepareNextShot();
                SetModelMode(true);
            }
            else
            {
                SetModelMode(false);
            }
        }
    }

    private void ApplyToolChange(ToolType newTool)
    {
        _currentTool = newTool;
        _archerySystem?.ExitCombatMode();
        _meleeSystem?.ExitMeleeMode();

        switch (_currentTool)
        {
            case ToolType.Bow:
                _archerySystem?.EnterCombatMode();
                SetModelMode(true);
                break;
            case ToolType.Sword:
                _meleeSystem?.EnterMeleeMode();
                SetModelMode(false);
                break;
            case ToolType.Hammer:
                _archerySystem?.EnterBuildMode();
                _hud?.SetBuildTool(MainHUDController.BuildTool.Selection);
                SetModelMode(false);
                break;
            case ToolType.Shovel:
                _archerySystem?.EnterBuildMode();
                _hud?.SetBuildTool(MainHUDController.BuildTool.Survey);
                SetModelMode(false);
                break;
            case ToolType.None:
                CurrentState = PlayerState.WalkMode;
                SetModelMode(false);
                break;
        }

        if (_sword != null)
        {
            _sword.Visible = (_currentTool == ToolType.Sword);
        }
    }

    private void SetModelMode(bool archery)
    {
        _modelManager?.SetModelMode(archery);
        _animPlayer = archery ? _archeryAnimPlayer : _meleeAnimPlayer;
    }

    private void CycleCharacterModel()
    {
        _modelManager?.CycleCharacterModel();

        if (IsLocal && _archerySystem != null)
        {
            string heroId = _modelManager?.CurrentModelId ?? "Ranger";
            _archerySystem.PlayerStatsService?.LoadStats(heroId);
        }
    }

    private void SetupVisualSword()
    {
        // Logic to set up sword mesh/position
    }
}
