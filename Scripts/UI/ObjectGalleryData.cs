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
    public static readonly string[] MainCategories = { "Nature", "Structures", "Furniture", "Decor", "Utility", "Misc" };

    /// <summary>
    /// Scans and returns all available assets for the gallery.
    /// </summary>
    public static List<ObjectAsset> ScanAssets()
    {
        var assets = new List<ObjectAsset>();

        // Utility - Markers
        assets.Add(new ObjectAsset { Name = "TeePin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        assets.Add(new ObjectAsset { Name = "Pin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        assets.Add(new ObjectAsset { Name = "DistanceSign", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
        assets.Add(new ObjectAsset { Name = "CourseMap", MainCategory = "Utility", SubCategory = "Markers", Path = "" });

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
            assets.Add(new ObjectAsset { Name = m, MainCategory = "Utility", SubCategory = "Combat", Path = "" });
        }

        // Scan filesystem for GLTF assets
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
                    if (fileName.EndsWith(".gltf") || fileName.EndsWith(".gltf.remap") || fileName.EndsWith(".gltf.import"))
                    {
                        string logicalName = fileName.Replace(".remap", "").Replace(".import", "");
                        string cleanName = logicalName.Replace(".gltf", "");

                        if (!assets.Exists(a => a.Name == cleanName))
                        {
                            var (main, sub) = GetAssetCategories(cleanName);
                            assets.Add(new ObjectAsset
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

        GD.Print($"ObjectGalleryData: Scanned {assets.Count} assets.");
        return assets;
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
