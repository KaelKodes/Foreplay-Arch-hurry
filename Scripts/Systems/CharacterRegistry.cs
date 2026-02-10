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

        public string WeaponOverridePath { get; set; } = ""; // Path to FBX/GLB for external weapon theft
        public Vector3 WeaponPositionOffset { get; set; } = Vector3.Zero;
        public Vector3 WeaponRotationOffset { get; set; } = Vector3.Zero;

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
            MeleeScenePath = "res://Assets/Heroes/Ranger/Animations/Erika Archer With Bow Arrow.fbx",
            ArcheryScenePath = "res://Assets/Heroes/Ranger/Animations/Erika Archer With Bow Arrow.fbx",
            IsCustomSkeleton = false,
            AnimationMap = new Dictionary<string, string> {
                { "Idle", "standing idle 01" },
                { "Walk", "standing walk forward" },
                { "Run", "standing run forward" }
            }
        });

        // Warrior (New Paladin WProp model - Own Skeleton + Animations)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Warrior",
            DisplayName = "Warrior",
            MeleeScenePath = "res://Assets/Heroes/Warrior/animations/Paladin WProp J Nordstrom.fbx",
            ArcheryScenePath = "res://Assets/Heroes/Warrior/animations/Paladin WProp J Nordstrom.fbx",
            IsCustomSkeleton = true,
            AnimationSources = new Dictionary<string, string> {
                // Core locomotion
                { "Idle", "res://Assets/Heroes/Warrior/animations/sword and shield idle.fbx" },
                { "Walk", "res://Assets/Heroes/Warrior/animations/sword and shield walk.fbx" },
                { "Run", "res://Assets/Heroes/Warrior/animations/sword and shield run.fbx" },
                { "Jump", "res://Assets/Heroes/Warrior/animations/sword and shield jump.fbx" },
                // Melee combat — basic attack
                { "MeleeAttack1", "res://Assets/Heroes/Warrior/animations/sword and shield slash.fbx" },
                // Melee combat — charged tiers
                { "PowerSlash", "res://Assets/Heroes/Warrior/animations/sword and shield slash (4).fbx" },
                { "SlashCombo", "res://Assets/Heroes/Warrior/animations/sword and shield slash (2).fbx" },
                // Extra slash variants
                { "MeleeAttack2", "res://Assets/Heroes/Warrior/animations/sword and shield slash (3).fbx" },
                { "MeleeAttack3", "res://Assets/Heroes/Warrior/animations/sword and shield attack.fbx" },
                // Archery slots (Warrior doesn't use a bow — reuse melee anims)
                { "ArcheryIdle", "res://Assets/Heroes/Warrior/animations/sword and shield idle.fbx" },
                { "ArcheryDraw", "res://Assets/Heroes/Warrior/animations/sword and shield idle.fbx" },
                { "ArcheryFire", "res://Assets/Heroes/Warrior/animations/sword and shield slash.fbx" },
                // Death
                { "Death", "res://Assets/Heroes/Warrior/animations/sword and shield death.fbx" },
                // Abilities
                { "Kick", "res://Assets/Heroes/Warrior/animations/sword and shield kick.fbx" },
                { "PowerUp", "res://Assets/Heroes/Warrior/animations/sword and shield power up.fbx" },
                { "Casting", "res://Assets/Heroes/Warrior/animations/sword and shield casting.fbx" },
                // Extra combat anims
                { "Block", "res://Assets/Heroes/Warrior/animations/sword and shield block.fbx" },
                { "BlockIdle", "res://Assets/Heroes/Warrior/animations/sword and shield block idle.fbx" },
                { "Impact", "res://Assets/Heroes/Warrior/animations/sword and shield impact.fbx" }
            }
        });

        // Cleric (Custom Greatsword User - now using Custom Skeleton logic)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Cleric",
            DisplayName = "Cleric",
            MeleeScenePath = "res://Assets/Heroes/Cleric/Animations/Knight D Pelegrini.fbx",
            ArcheryScenePath = "res://Assets/Heroes/Cleric/Animations/Knight D Pelegrini.fbx",
            IsCustomSkeleton = true,
            PositionOffset = new Vector3(0, -0.18f, 0),
            WeaponOverridePath = "res://Assets/Heroes/Cleric/Animations/GreatswordT-Pose.fbx",
            WeaponPositionOffset = new Vector3(-0.816f, -0.062f, -1.272f),
            WeaponRotationOffset = new Vector3(-46.647f, 132.593f, 50.388f),
            AnimationSources = new Dictionary<string, string> {
                { "Idle", "res://Assets/Heroes/Cleric/Animations/great sword idle.fbx" },
                { "Walk", "res://Assets/Heroes/Cleric/Animations/great sword walk.fbx" },
                { "Run", "res://Assets/Heroes/Cleric/Animations/great sword run.fbx" },
                { "Jump", "res://Assets/Heroes/Cleric/Animations/great sword jump.fbx" },
                // Melee combat
                { "MeleeAttack1", "res://Assets/Heroes/Cleric/Animations/great sword slash.fbx" },
                { "PowerSlash", "res://Assets/Heroes/Cleric/Animations/great sword slash (4).fbx" },
                { "SlashCombo", "res://Assets/Heroes/Cleric/Animations/great sword slash (2).fbx" },
                // Ability slots
                { "CastingSlot1", "res://Assets/Heroes/Cleric/Animations/great sword casting.fbx" },
                { "IdleSlot2", "res://Assets/Heroes/Cleric/Animations/great sword idle (2).fbx" },
                { "SpinSlot3", "res://Assets/Heroes/Cleric/Animations/great sword high spin attack.fbx" },
                { "CastingSlot4", "res://Assets/Heroes/Cleric/Animations/great sword casting.fbx" },
                // Misc
                { "Death", "res://Assets/Heroes/Cleric/Animations/two handed sword death.fbx" },
                { "Block", "res://Assets/Heroes/Cleric/Animations/great sword blocking.fbx" },
                { "BlockIdle", "res://Assets/Heroes/Cleric/Animations/great sword blocking (2).fbx" },
                { "Impact", "res://Assets/Heroes/Cleric/Animations/great sword impact.fbx" }
            }
        });

        // Necromancer (Mixamo rig - uses his own custom skeleton logic)
        AvailableModels.Add(new CharacterModel
        {
            Id = "Necromancer",
            DisplayName = "Necromancer",
            MeleeScenePath = "res://Assets/Heroes/Necromancer/Animations/Vampire A Lusth.fbx",
            ArcheryScenePath = "res://Assets/Heroes/Necromancer/Animations/Vampire A Lusth.fbx",
            IsCustomSkeleton = true,
            PositionOffset = new Vector3(0, -0.1f, 0),
            AnimationSources = new Dictionary<string, string> {
                { "Idle", "res://Assets/Heroes/Necromancer/Animations/standing idle.fbx" },
                { "Walk", "res://Assets/Heroes/Necromancer/Animations/Standing Walk Forward.fbx" },
                { "Run", "res://Assets/Heroes/Necromancer/Animations/Standing Run Forward.fbx" },
                { "Jump", "res://Assets/Heroes/Necromancer/Animations/Standing Jump.fbx" },
                // Melee/Combat logic
                { "MeleeAttack1", "res://Assets/Heroes/Necromancer/Animations/Standing 1H Magic Attack 01.fbx" },
                { "PowerSlash", "res://Assets/Heroes/Necromancer/Animations/Standing 1H Magic Attack 03.fbx" },
                { "SlashCombo", "res://Assets/Heroes/Necromancer/Animations/Standing 2H Magic Attack 05.fbx" },
                // Ability Slots
                { "Kick", "res://Assets/Heroes/Necromancer/Animations/Standing 1H Magic Attack 01.fbx" },
                { "MeleeAttack3", "res://Assets/Heroes/Necromancer/Animations/Standing 2H Magic Area Attack 01.fbx" },
                { "PowerUp", "res://Assets/Heroes/Necromancer/Animations/Standing 2H Magic Attack 03.fbx" },
                { "Casting", "res://Assets/Heroes/Necromancer/Animations/Standing 2H Magic Area Attack 02.fbx" },
                // Misc
                { "ArcheryIdle", "res://Assets/Heroes/Necromancer/Animations/standing idle.fbx" },
                { "ArcheryDraw", "res://Assets/Heroes/Necromancer/Animations/standing 1H cast spell 01.fbx" },
                { "ArcheryFire", "res://Assets/Heroes/Necromancer/Animations/Standing 1H Magic Attack 01.fbx" },
                { "Death", "res://Assets/Heroes/Necromancer/Animations/Standing React Death Backward.fbx" }
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
