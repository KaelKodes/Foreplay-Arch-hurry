using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Archery;

/// <summary>
/// Stores configuration for an imported character model.
/// Saved as JSON and loaded at runtime.
/// </summary>
public class CharacterConfig
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ModelPath { get; set; } = "";  // res:// path to .glb/.fbx
    public string WeaponOverridePath { get; set; } = ""; // Path to FBX for external weapon theft
    public float[] WeaponPositionOffset { get; set; } = new float[] { 0, 0, 0 };
    public float[] WeaponRotationOffset { get; set; } = new float[] { 0, 0, 0 };

    /// <summary>
    /// Animation source: "self" (use model's own), "erika", or another model ID
    /// </summary>
    public string AnimationSource { get; set; } = "erika";

    /// <summary>
    /// Maps our standard bone names to the model's actual bone names.
    /// Key = Standard (e.g., "Hips"), Value = Actual (e.g., "mixamorig_Hips")
    /// </summary>
    public Dictionary<string, string> BoneMap { get; set; } = new();

    /// <summary>
    /// Maps our standard animation names to the model's actual animation names.
    /// Key = Standard (e.g., "Idle"), Value = Actual (e.g., "Armature|Idle")
    /// </summary>
    public Dictionary<string, string> AnimationMap { get; set; } = new();

    /// <summary>
    /// Defines the source for each standard animation.
    /// Key = Standard Animation Name (e.g., "Idle")
    /// Value = "standard" (Erika) OR "internal:AnimationName" (Embedded)
    /// </summary>
    public Dictionary<string, string> AnimationSources { get; set; } = new();

    /// <summary>
    /// Scale adjustment applied to the model root.
    /// </summary>
    public float[] ScaleOffset { get; set; } = new float[] { 1, 1, 1 };

    /// <summary>
    /// Rotation adjustment in degrees.
    /// </summary>
    public float[] RotationOffset { get; set; } = new float[] { 0, 0, 0 };

    /// <summary>
    /// Configuration for detected meshes (visibility, scale, category).
    /// Key = Mesh Node Name
    /// </summary>
    public Dictionary<string, MeshConfig> Meshes { get; set; } = new();

    public class MeshConfig
    {
        public bool IsVisible { get; set; } = true;
        public float[] Scale { get; set; } = new float[] { 1, 1, 1 }; // Uniform support but array for persistence
        public string Category { get; set; } = Categories.Body; // "Body", "Armor", "Prop"
        public string Alias { get; set; } = ""; // User-defined name

        public static class Categories
        {
            public const string Body = "Body";
            public const string WeaponMain = "WeaponMain"; // Sword
            public const string WeaponOff = "WeaponOff"; // Shield
            public const string WeaponBow = "WeaponBow"; // Bow
            public const string Item = "Item";
            public const string Hidden = "Hidden";

            public static readonly string[] All = { Body, WeaponMain, WeaponOff, WeaponBow, Item, Hidden };
        }
    }

    /// <summary>
    /// Whether this config has been validated/tested.
    /// </summary>
    public bool IsValidated { get; set; } = false;

    /// <summary>
    /// Skeleton signature for compatibility matching.
    /// </summary>
    public string SkeletonSignature { get; set; } = "";

    // Helper properties for Godot Vector3 conversion
    [JsonIgnore]
    public Vector3 Scale => new Vector3(ScaleOffset[0], ScaleOffset[1], ScaleOffset[2]);

    [JsonIgnore]
    public Vector3 Rotation => new Vector3(RotationOffset[0], RotationOffset[1], RotationOffset[2]);

    /// <summary>
    /// Standard bone names we expect to map.
    /// </summary>
    public static readonly string[] StandardBones = new string[]
    {
        "Hips", "Spine", "Spine1", "Spine2",
        "Neck", "Head",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "LeftPalm",
        "RightShoulder", "RightArm", "RightForeArm", "RightHand", "RightPalm",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
        "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"
    };

    /// <summary>
    /// Standard animation names we expect.
    /// </summary>
    public static readonly string[] StandardAnimations = new string[]
    {
        "Idle", "Walk", "Run", "Jump",
        "MeleeAttack1", "MeleeAttack2", "MeleeAttack3",
        "ArcheryIdle", "ArcheryDraw", "ArcheryFire",
        "Death"
    };
}

/// <summary>
/// Handles saving and loading CharacterConfig files.
/// Saves to project directory since this is an admin tool.
/// </summary>
public static class CharacterConfigManager
{
    private const string ConfigDirectory = "res://Data/Characters/";

    public static void EnsureDirectoryExists()
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(ConfigDirectory)))
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(ConfigDirectory));
        }
    }

    public static string GetConfigPath(string id)
    {
        return ConfigDirectory + id + ".json";
    }

    public static bool SaveConfig(CharacterConfig config)
    {
        try
        {
            EnsureDirectoryExists();
            string path = GetConfigPath(config.Id);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[CharacterConfig] Failed to open file for writing: {path}");
                return false;
            }
            file.StoreString(json);
            GD.Print($"[CharacterConfig] Saved: {path}");
            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CharacterConfig] Save error: {e.Message}");
            return false;
        }
    }

    public static CharacterConfig LoadConfig(string id)
    {
        try
        {
            string path = GetConfigPath(id);
            if (!FileAccess.FileExists(path)) return null;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;

            string json = file.GetAsText();
            return JsonSerializer.Deserialize<CharacterConfig>(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CharacterConfig] Load error: {e.Message}");
            return null;
        }
    }

    public static List<CharacterConfig> LoadAllConfigs()
    {
        var configs = new List<CharacterConfig>();
        EnsureDirectoryExists();

        string globalPath = ProjectSettings.GlobalizePath(ConfigDirectory);
        using var dir = DirAccess.Open(globalPath);
        if (dir == null) return configs;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                string id = fileName.Replace(".json", "");
                var config = LoadConfig(id);
                if (config != null) configs.Add(config);
            }
            fileName = dir.GetNext();
        }

        GD.Print($"[CharacterConfig] Loaded {configs.Count} character configs.");
        return configs;
    }
}
