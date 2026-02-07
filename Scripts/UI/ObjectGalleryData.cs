using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Static data and utility methods for the object gallery.
/// Extracted from MainHUDController to reduce file size.
/// </summary>
public static class ObjectGalleryData
{
    public struct ObjectAsset
    {
        public string Name;
        public string Path;
        public string MainCategory;
        public string SubCategory;
    }

    /// <summary>
    /// Main categories for the object gallery.
    /// </summary>
    public static readonly string[] MainCategories = { "Nature", "Structures", "Furniture", "Decor", "Utility", "MOBA", "Misc" };

    /// <summary>
    /// Returns all available assets for the gallery from the SQL database.
    /// </summary>
    public static List<ObjectAsset> GetAssets()
    {
        var assets = new List<ObjectAsset>();
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Name, Path, MainCategory, SubCategory FROM GalleryAssets ORDER BY MainCategory, SubCategory, Name";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        assets.Add(new ObjectAsset
                        {
                            Name = reader.GetString(0),
                            Path = reader.GetString(1),
                            MainCategory = reader.GetString(2),
                            SubCategory = reader.GetString(3)
                        });
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"ObjectGalleryData: Failed to fetch assets: {e.Message}");
        }

        // If DB is empty or missing the new "Minion" category, OR contains "bad" data, trigger sync
        bool missingMinions = !assets.Exists(a => a.SubCategory == "Minion");
        bool hasBadData = assets.Exists(a => a.Name.ToLower().Contains("zombie attack"));
        // Also check if any Minion entry has an FBX path (should be .tscn)
        bool hasWrongMinionPath = assets.Exists(a => a.SubCategory == "Minion" && a.Path.EndsWith(".fbx"));

        if (assets.Count == 0 || missingMinions || hasBadData || hasWrongMinionPath)
        {
            GD.Print("ObjectGalleryData: Database stale or corrupted. Force synchronizing...");
            SyncAssetsToDB();
            return GetAssets(); // Recursion once
        }

        return assets;
    }

    /// <summary>
    /// Scans the filesystem and indexes assets into the SQL database.
    /// This should be called once on game start or when the library changes.
    /// </summary>
    public static void SyncAssetsToDB()
    {
        var discoveredAssets = new List<ObjectAsset>();

        // 1. Add procedural/virtual assets (not file-based)
        // Utility - Markers
        discoveredAssets.Add(new ObjectAsset { Name = "TeePin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        discoveredAssets.Add(new ObjectAsset { Name = "Pin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        discoveredAssets.Add(new ObjectAsset { Name = "DistanceSign", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        discoveredAssets.Add(new ObjectAsset { Name = "CourseMap", MainCategory = "Utility", SubCategory = "Markers", Path = "" });

        // MOBA Minions (Dedicated Scenes)
        discoveredAssets.Add(new ObjectAsset { Name = "Zombie", MainCategory = "MOBA", SubCategory = "Minion", Path = "res://Scenes/Entities/Zombie.tscn" });
        discoveredAssets.Add(new ObjectAsset { Name = "Crawler", MainCategory = "MOBA", SubCategory = "Minion", Path = "res://Scenes/Entities/Crawler.tscn" });
        discoveredAssets.Add(new ObjectAsset { Name = "Skeleton", MainCategory = "MOBA", SubCategory = "Minion", Path = "res://Assets/Monsters/SkeletonWarrior/skeleton_animated.glb" });
        discoveredAssets.Add(new ObjectAsset { Name = "Lich", MainCategory = "MOBA", SubCategory = "Minion", Path = "res://Assets/Monsters/Lich/Lich.glb" });
        discoveredAssets.Add(new ObjectAsset { Name = "Conjurer", MainCategory = "MOBA", SubCategory = "Minion", Path = "res://Assets/Monsters/Conjurer/demonic_ethereal_conjurer.glb" });

        // 2. Scan filesystem
        string[] searchPaths = {
            "res://Assets/Textures/NatureObjects/",
            "res://Assets/Textures/ManObjects/",
            "res://Assets/Textures/BuildingObjects/",
            "res://Assets/Monsters/"
        };

        foreach (string path in searchPaths)
        {
            ScanDirectoryRecursive(path, discoveredAssets);
        }

        // 3. Update Database
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    // Clear old entries (simple approach) or upsert
                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM GalleryAssets";
                    deleteCmd.ExecuteNonQuery();

                    var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO GalleryAssets (Name, Path, MainCategory, SubCategory) VALUES (@name, @path, @main, @sub)";
                    var pName = insertCmd.Parameters.Add("@name", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pPath = insertCmd.Parameters.Add("@path", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pMain = insertCmd.Parameters.Add("@main", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pSub = insertCmd.Parameters.Add("@sub", Microsoft.Data.Sqlite.SqliteType.Text);

                    foreach (var asset in discoveredAssets)
                    {
                        pName.Value = asset.Name;
                        pPath.Value = asset.Path;
                        pMain.Value = asset.MainCategory;
                        pSub.Value = asset.SubCategory;
                        insertCmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            GD.Print($"ObjectGalleryData: Synchronized {discoveredAssets.Count} assets to SQL.");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"ObjectGalleryData: Sync failure: {e.Message}");
        }
    }

    private static void ScanDirectoryRecursive(string path, List<ObjectAsset> discoveredAssets)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (dir.CurrentIsDir())
            {
                if (fileName != "." && fileName != "..")
                {
                    ScanDirectoryRecursive(path + fileName + "/", discoveredAssets);
                }
            }
            else if (fileName.EndsWith(".gltf") || fileName.EndsWith(".gltf.remap") || fileName.EndsWith(".gltf.import") ||
                fileName.EndsWith(".fbx") || fileName.EndsWith(".fbx.import"))
            {
                string logicalName = fileName.Replace(".remap", "").Replace(".import", "");
                string cleanName = logicalName.Replace(".gltf", "").Replace(".fbx", "");
                string lowerName = cleanName.ToLower();

                // Skip ALL monster FBX files - we use dedicated .tscn scenes for these
                if (lowerName.Contains("vampire") || lowerName.Contains("crawler") ||
                    lowerName.Contains("monster_1") || lowerName.Contains("base mesh") ||
                    lowerName.Contains("zombie") || lowerName.Contains("parasite"))
                {
                    fileName = dir.GetNext();
                    continue;
                }

                // Filter out animation files and secondary assets
                if (cleanName.Contains("@") || cleanName.Contains("Anim_") || lowerName.Contains("sk_"))
                {
                    fileName = dir.GetNext();
                    continue;
                }

                if (lowerName.Contains("idle") || lowerName.Contains("walk") || lowerName.Contains("run") ||
                    lowerName.Contains("attack") || lowerName.Contains("death") || lowerName.Contains("hit") ||
                    lowerName.Contains("scream") || lowerName.Contains("crawl") || lowerName.Contains("biting") ||
                    lowerName.Contains("dying") || lowerName.Contains("neck bite") || lowerName.Contains("jump") ||
                    lowerName.Contains("flying") || lowerName.Contains("atack") || lowerName.Contains("sex"))
                {
                    fileName = dir.GetNext();
                    continue;
                }

                // 4. Ensure name uniqueness
                if (!discoveredAssets.Exists(a => a.Name == cleanName))
                {
                    var (main, sub) = GetAssetCategories(cleanName);
                    discoveredAssets.Add(new ObjectAsset
                    {
                        Name = cleanName,
                        Path = path + logicalName,
                        MainCategory = main,
                        SubCategory = sub
                    });
                }
            }
            fileName = dir.GetNext();
        }
    }

    /// <summary>
    /// Determines the category for an asset based on its name.
    /// </summary>
    public static (string Main, string Sub) GetAssetCategories(string name)
    {
        name = name.ToLower();

        // MOBA Minions
        if (name.Contains("zombie") || name.Contains("crawler"))
            return ("MOBA", "Minion");

        // Nature
        if (name.Contains("tree") || name.Contains("pine") || name.Contains("trunk") || name.Contains("log")) return ("Nature", "Trees");
        if (name.Contains("rock") || name.Contains("pebble") || name.Contains("rubble")) return ("Nature", "Rocks");
        if (name.Contains("flower") || name.Contains("bush") || name.Contains("grass") || name.Contains("fern") || name.Contains("clover") || name.Contains("plant") || name.Contains("vine") || name.Contains("leaf")) return ("Nature", "Greenery");
        if (name.Contains("mushroom")) return ("Nature", "Mushrooms");

        // Structures
        if (name.Contains("wall") || name.Contains("barrier")) return ("Structures", "Walls");
        if (name.Contains("roof") || name.Contains("overhang")) return ("Structures", "Roofs");
        if (name.Contains("floor") || name.Contains("foundation") || name.Contains("ceiling")) return ("Structures", "Floors");
        if (name.Contains("stair")) return ("Structures", "Stairs");
        if (name.Contains("door") || name.Contains("window") || name.Contains("shutter")) return ("Structures", "Doors/Windows");
        if (name.Contains("column") || name.Contains("pillar") || name.Contains("balcony") || name.Contains("support")) return ("Structures", "Columns");

        // Furniture
        if (name.Contains("chair") || name.Contains("stool") || name.Contains("bench")) return ("Furniture", "Seating");
        if (name.Contains("table") || name.Contains("shelf") || name.Contains("shelves")) return ("Furniture", "Surfaces");
        if (name.Contains("bed")) return ("Furniture", "Sleeping");
        if (name.Contains("box") || name.Contains("crate") || name.Contains("barrel") || name.Contains("keg") || name.Contains("chest")) return ("Furniture", "Storage");

        // Decor
        if (name.Contains("torch") || name.Contains("candle") || name.Contains("lantern")) return ("Decor", "Lighting");
        if (name.Contains("banner") || name.Contains("flag") || name.Contains("shield") || name.Contains("sword") || name.Contains("weapon") || name.Contains("keyring")) return ("Decor", "Military");
        if (name.Contains("bottle") || name.Contains("plate") || name.Contains("cup") || name.Contains("coin") || name.Contains("key") || name.Contains("book") || name.Contains("food")) return ("Decor", "Items");
        if (name.Contains("prop") || name.Contains("cart") || name.Contains("wagon")) return ("Decor", "Misc");

        // MOBA
        if (name.Contains("watch+tower")) return ("MOBA", "Towers");
        if (name.Contains("mine_mesh")) return ("MOBA", "Nexus");

        return ("Misc", "General");
    }

    /// <summary>
    /// Maps species aliases for Monsters creation.
    /// </summary>
    public static string ResolveMonsterSpecies(string objectId)
    {
        // Legacy method kept for interface compatibility, but we are no longer using the old monster system.
        // If "Monsters" is passed directly (unlikely) fallback to a known surviving type.
        return objectId == "Monsters" ? "Zombie" : objectId;
    }
}
