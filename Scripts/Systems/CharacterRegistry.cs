using Godot;
using System.Collections.Generic;
using Archery;

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

        // Custom Skeleton Support
        public bool IsCustomSkeleton { get; set; } = false;
        public Dictionary<string, string> AnimationMap { get; set; } = new();
        public Dictionary<string, string> AnimationSources { get; set; } = new(); // New

        // Transform Adjustments for Custom Rigs
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public Vector3 PositionOffset { get; set; } = Vector3.Zero;
        public Vector3 ModelScale { get; set; } = Vector3.One;

        // Mesh Configuration
        public Dictionary<string, CharacterConfig.MeshConfig> Meshes { get; set; } = new();

        // Fallback Model (if animation missing, try this model's mapping)
        public string FallbackModelId { get; set; } = "Ranger";
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
        LoadCustomConfigs();
        GD.Print($"[CharacterRegistry] Initialized with {AvailableModels.Count} models");
    }

    /// <summary>
    /// Load character configs from JSON files in Data/Characters.
    /// </summary>
    private void LoadCustomConfigs()
    {
        var configs = CharacterConfigManager.LoadAllConfigs();
        foreach (var config in configs)
        {
            // Skip if already registered (e.g., overlaps with default)
            if (AvailableModels.Exists(m => m.Id == config.Id)) continue;

            var model = new CharacterModel
            {
                Id = config.Id,
                DisplayName = config.DisplayName,
                MeleeScenePath = config.ModelPath,
                ArcheryScenePath = config.ModelPath,
                IsCustomSkeleton = true,
                AnimationMap = config.AnimationMap ?? new Dictionary<string, string>(),
                AnimationSources = config.AnimationSources ?? new Dictionary<string, string>(), // New
                RotationOffset = config.Rotation,
                ModelScale = config.Scale,
                FallbackModelId = config.AnimationSource == "Ranger" ? "Ranger" : config.AnimationSource,
                Meshes = config.Meshes ?? new Dictionary<string, CharacterConfig.MeshConfig>()
            };

            // Store bone map for later use by CharacterModelManager
            model.AnimationMap["__BONE_MAP__"] = System.Text.Json.JsonSerializer.Serialize(config.BoneMap);

            AvailableModels.Add(model);
            _modelAssignments[model.Id] = -1;
            GD.Print($"[CharacterRegistry] Loaded custom: {config.DisplayName}");
        }
    }

    private void RegisterDefaultModels()
    {
        // Ranger (Formerly Erika - Shared Skeleton)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Ranger",
            DisplayName = "Ranger",
            MeleeScenePath = "res://Assets/Erika/Erika Archer.fbx",
            ArcheryScenePath = "res://Assets/ErikaBow/Erika Archer With Bow Arrow.fbx",
            IsCustomSkeleton = false,
            AnimationMap = new Dictionary<string, string> {
                { "Idle", "standing idle 01" },
                { "Walk", "standing walk forward" },
                { "Run", "standing run forward" }
            }
        });

        // Warrior (Formerly Paladin.fbx - Shared Skeleton)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Warrior",
            DisplayName = "Warrior",
            MeleeScenePath = "res://Assets/CharacterMeshes/Paladin.fbx",
            ArcheryScenePath = "res://Assets/CharacterMeshes/Paladin.fbx",
            IsCustomSkeleton = false
        });

        // Cleric (Formerly Warrior.fbx - Shared Skeleton)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Cleric",
            DisplayName = "Cleric",
            MeleeScenePath = "res://Assets/CharacterMeshes/Warrior.fbx",
            ArcheryScenePath = "res://Assets/CharacterMeshes/Warrior.fbx",
            IsCustomSkeleton = false
        });

        // Necromancer (Mixamo rig - uses his own custom skeleton logic)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Necromancer",
            DisplayName = "Necromancer",
            MeleeScenePath = "res://Assets/Necromancer/Vampire A Lusth.fbx",
            ArcheryScenePath = "res://Assets/Necromancer/Vampire A Lusth.fbx",
            IsCustomSkeleton = true,
            AnimationSources = new Dictionary<string, string> {
                { "Idle", "res://Assets/Necromancer/standing idle.fbx" },
                { "Walk", "res://Assets/Necromancer/Standing Walk Forward.fbx" },
                { "Run", "res://Assets/Necromancer/Standing Run Forward.fbx" },
                { "Jump", "res://Assets/Necromancer/Standing Jump.fbx" },
                { "MeleeAttack1", "res://Assets/Necromancer/Standing 1H Magic Attack 01.fbx" },
                { "MeleeAttack2", "res://Assets/Necromancer/Standing 1H Magic Attack 02.fbx" },
                { "MeleeAttack3", "res://Assets/Necromancer/Standing 1H Magic Attack 03.fbx" },
                { "ArcheryIdle", "res://Assets/Necromancer/standing idle.fbx" }, // Use Idle for Bow Aim
                { "ArcheryDraw", "res://Assets/Necromancer/standing 1H cast spell 01.fbx" },
                { "ArcheryFire", "res://Assets/Necromancer/Standing 1H Magic Attack 01.fbx" },
                { "Death", "res://Assets/Necromancer/Standing React Death Backward.fbx" }
            }
        });

        // Note: Guardian, Monk and Pale Knight removed - they had compatibility issues

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
