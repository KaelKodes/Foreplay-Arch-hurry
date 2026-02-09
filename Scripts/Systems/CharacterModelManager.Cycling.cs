using Godot;
using System;

namespace Archery;

public partial class CharacterModelManager
{
    /// <summary>
    /// Cycles to the next available character model.
    /// </summary>
    public void CycleCharacterModel()
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null)
        {
            GD.PrintErr("[CharacterModelManager] CharacterRegistry not found!");
            return;
        }

        var nextModel = registry.GetNextModel(_currentModelId);
        if (nextModel != null && nextModel.Id != _currentModelId)
        {
            SetCharacterModel(nextModel.Id);
            GD.Print($"[CharacterModelManager] Switched to model: {nextModel.DisplayName}");
        }
    }

    /// <summary>
    /// Sets the character model by ID.
    /// </summary>
    public void SetCharacterModel(string modelId)
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) return;

        var model = registry.GetModel(modelId);
        if (model == null)
        {
            GD.PrintErr($"[CharacterModelManager] Model not found: {modelId}");
            return;
        }

        if (modelId == _currentModelId && _meleeModel != null && _meleeModel.Visible) return; // Already correctly set

        _currentModelId = modelId;

        if (model.IsCustomSkeleton)
        {
            // Full scene swap
            SetupCustomModel(model);
        }
        else
        {
            // Shared skeleton mesh swap
            CleanupCustomModel();
            SwapModelMesh(_meleeModel, model.MeleeScenePath);
            SwapModelMesh(_archeryModel, model.ArcheryScenePath);
        }

        GD.Print($"[CharacterModelManager] Model set to: {model.DisplayName} (Custom Rig: {model.IsCustomSkeleton})");

        // Refresh visibility/mode
        SetModelMode(_player.CurrentState == PlayerState.CombatArcher);
    }
}
