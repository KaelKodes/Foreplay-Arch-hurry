using Godot;
using System.Collections.Generic;

/// <summary>
/// Central registry of available character models.
/// All models must share the same bone structure for animation compatibility.
/// </summary>
public partial class CharacterRegistry : Node
{
    /// <summary>
    /// Represents a character model that can be selected by players.
    /// </summary>
    public class CharacterModel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string MeleeScenePath { get; set; }  // Path to melee model scene
        public string ArcheryScenePath { get; set; } // Path to archery model scene (with bow)
        public Texture2D Icon { get; set; }  // Optional preview icon for menu
    }

    private static CharacterRegistry _instance;
    public static CharacterRegistry Instance => _instance;

    /// <summary>
    /// List of all available character models.
    /// Add new models here as they become available.
    /// </summary>
    public List<CharacterModel> AvailableModels { get; private set; } = new();

    /// <summary>
    /// Tracks which models are currently in use by players (for first-come-first-serve).
    /// Key: Model ID, Value: Player ID using it (or -1 if available)
    /// </summary>
    private Dictionary<string, long> _modelAssignments = new();

    public override void _Ready()
    {
        _instance = this;
        RegisterDefaultModels();
        GD.Print($"[CharacterRegistry] Initialized with {AvailableModels.Count} models");
    }

    private void RegisterDefaultModels()
    {
        // Erika - Default model
        AvailableModels.Add(new CharacterModel
        {
            Id = "erika",
            DisplayName = "Erika",
            MeleeScenePath = "res://Assets/Erika/Erika Archer.fbx",
            ArcheryScenePath = "res://Assets/ErikaBow/Erika Archer With Bow Arrow.fbx"
        });

        // Warrior
        AvailableModels.Add(new CharacterModel
        {
            Id = "warrior",
            DisplayName = "Warrior",
            MeleeScenePath = "res://Assets/CharacterMeshes/Warrior.fbx",
            ArcheryScenePath = "res://Assets/CharacterMeshes/Warrior.fbx" // Same for now (no bow variant)
        });

        // Paladin
        AvailableModels.Add(new CharacterModel
        {
            Id = "paladin",
            DisplayName = "Paladin",
            MeleeScenePath = "res://Assets/CharacterMeshes/Paladin.fbx",
            ArcheryScenePath = "res://Assets/CharacterMeshes/Paladin.fbx" // Same for now (no bow variant)
        });

        // Initialize assignments
        foreach (var model in AvailableModels)
        {
            _modelAssignments[model.Id] = -1;
        }
    }

    /// <summary>
    /// Get a model by its ID.
    /// </summary>
    public CharacterModel GetModel(string id)
    {
        return AvailableModels.Find(m => m.Id == id);
    }

    /// <summary>
    /// Get the model at a specific index (for cycling).
    /// </summary>
    public CharacterModel GetModelByIndex(int index)
    {
        if (AvailableModels.Count == 0) return null;
        return AvailableModels[index % AvailableModels.Count];
    }

    /// <summary>
    /// Get the next model in the list (wraps around).
    /// </summary>
    public CharacterModel GetNextModel(string currentModelId)
    {
        int currentIndex = AvailableModels.FindIndex(m => m.Id == currentModelId);
        if (currentIndex < 0) currentIndex = 0;
        int nextIndex = (currentIndex + 1) % AvailableModels.Count;
        return AvailableModels[nextIndex];
    }

    /// <summary>
    /// Check if a model is available (not claimed by another player).
    /// </summary>
    public bool IsModelAvailable(string modelId)
    {
        return _modelAssignments.TryGetValue(modelId, out long playerId) && playerId == -1;
    }

    /// <summary>
    /// Attempt to claim a model for a player. Returns true if successful.
    /// </summary>
    public bool TryClaimModel(string modelId, long playerId)
    {
        if (!IsModelAvailable(modelId)) return false;
        _modelAssignments[modelId] = playerId;
        GD.Print($"[CharacterRegistry] Player {playerId} claimed model '{modelId}'");
        return true;
    }

    /// <summary>
    /// Release a model when a player disconnects or changes model.
    /// </summary>
    public void ReleaseModel(long playerId)
    {
        foreach (var key in _modelAssignments.Keys)
        {
            if (_modelAssignments[key] == playerId)
            {
                _modelAssignments[key] = -1;
                GD.Print($"[CharacterRegistry] Released model '{key}' from player {playerId}");
                break;
            }
        }
    }

    /// <summary>
    /// Get the model ID currently assigned to a player.
    /// </summary>
    public string GetPlayerModel(long playerId)
    {
        foreach (var kvp in _modelAssignments)
        {
            if (kvp.Value == playerId) return kvp.Key;
        }
        return null;
    }
}
