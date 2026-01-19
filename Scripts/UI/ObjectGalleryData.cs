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

        // If DB is empty, trigger a sync and retry once
        if (assets.Count == 0)
        {
            GD.Print("ObjectGalleryData: Asset DB empty. Synchronizing...");
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

        // Utility - Combat (Monsters)
        string[] monsters = {
            "Yeti", "Yeti_Blob", "Orc", "Orc_Blob", "Ninja", "Ninja_Blob", "Wizard_Blob",
            "Pigeon", "Pigeon_Blob", "Slime", "Shroom", "Alpaking", "Alpaking_Evolved",
            "Armabee", "Armabee_Evolved", "Birb", "Birb_Blob", "Blue_Blob", "Bunny",
            "Cactoro", "Cactoro_Blob", "Chicken", "Chicken_Blob", "Demon", "Demon_Flying",
            "Dino", "Dog_Blob", "Dragon", "Dragon_Fly", "Fish", "Fish_Blob", "Frog",
            "Ghost", "Goleling", "Goleling_Evolved", "Green_Blob", "GreenSpiky_Blob",
            "Hywirl", "Monkroose", "Mushroom", "MushroomKing", "Pink_Blob", "Squidle",
            "Tribal", "Tribal_Flying"
        };
        foreach (var m in monsters)
        {
            discoveredAssets.Add(new ObjectAsset { Name = m, MainCategory = "Utility", SubCategory = "Combat", Path = "" });
        }

        // Monsters with dedicated scenes
        discoveredAssets.Add(new ObjectAsset { Name = "Zombie", MainCategory = "Utility", SubCategory = "Combat", Path = "res://Scenes/Entities/Zombie.tscn" });

        // 2. Scan filesystem
        string[] searchPaths = {
            "res://Assets/Textures/NatureObjects/",
            "res://Assets/Textures/ManObjects/",
            "res://Assets/Textures/BuildingObjects/"
        };

        foreach (string path in searchPaths)
        {
            using var dir = DirAccess.Open(path);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (fileName != "")
                {
                    if (fileName.EndsWith(".gltf") || fileName.EndsWith(".gltf.remap") || fileName.EndsWith(".gltf.import") ||
                        fileName.EndsWith(".fbx") || fileName.EndsWith(".fbx.import"))
                    {
                        string logicalName = fileName.Replace(".remap", "").Replace(".import", "");
                        string cleanName = logicalName.Replace(".gltf", "").Replace(".fbx", "");

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

    /// <summary>
    /// Determines the category for an asset based on its name.
    /// </summary>
    public static (string Main, string Sub) GetAssetCategories(string name)
    {
        name = name.ToLower();

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
    /// Maps species aliases for Monster creation.
    /// </summary>
    public static string ResolveMonsterSpecies(string objectId)
    {
        string species = objectId;
        if (species == "Monster") species = "Yeti";
        if (species == "Slime") species = "Glub";
        if (species == "Shroom") species = "Mushnub_Evolved";
        if (species == "Wizard") species = "Wizard_Blob";
        if (species == "Blue_Blob") species = "BlueDemon";
        if (species == "Gold_Blob") species = "MushroomKing";
        if (species == "Warrior" || species == "Knight" || species == "Skeleton") species = "Orc";
        return species;
    }
}
