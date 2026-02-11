using System.Collections.Generic;

namespace Archery;

// ── Enums ────────────────────────────────────────────────────────────
public enum ItemTier { Consumable, Common, Uncommon, Rare, Legendary }

// ── Stat Bonuses ─────────────────────────────────────────────────────
/// <summary>
/// Flat stat bonuses an item grants when equipped.
/// </summary>
public class ItemStats
{
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Vitality { get; set; }
    public int Wisdom { get; set; }
    public int Agility { get; set; }
    public int Haste { get; set; }
    public int Concentration { get; set; }

    public static ItemStats None => new();

    public ItemStats() { }
    public ItemStats(int str = 0, int intel = 0, int vit = 0, int wis = 0,
                     int agi = 0, int haste = 0, int conc = 0)
    {
        Strength = str; Intelligence = intel; Vitality = vit;
        Wisdom = wis; Agility = agi; Haste = haste; Concentration = conc;
    }
}

// ── Item Info ────────────────────────────────────────────────────────
/// <summary>
/// Complete definition of a MOBA shop item.
/// </summary>
public class ItemInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ItemTier Tier { get; set; }
    public int GoldCost { get; set; }
    public ItemStats Stats { get; set; }

    /// <summary>Passive or active effect description (empty = no special effect).</summary>
    public string PassiveName { get; set; }
    public string PassiveDescription { get; set; }

    /// <summary>Item IDs required to craft this item. Empty = purchased directly.</summary>
    public string[] Recipe { get; set; }

    /// <summary>Extra gold on top of component costs (recipe scroll cost).</summary>
    public int RecipeCost { get; set; }

    /// <summary>For consumables: max stack count. 0 = not stackable.</summary>
    public int MaxStacks { get; set; }

    /// <summary>Duration in seconds for timed consumables. 0 = instant/permanent.</summary>
    public float Duration { get; set; }

    /// <summary>Godot resource path to this item's icon texture.</summary>
    public string IconPath { get; set; }

    public ItemInfo(string id, string name, string desc, ItemTier tier, int gold,
                    ItemStats stats, string passiveName = "", string passiveDesc = "",
                    string[] recipe = null, int recipeCost = 0,
                    int maxStacks = 0, float duration = 0f, string iconPath = "")
    {
        Id = id; Name = name; Description = desc; Tier = tier;
        GoldCost = gold; Stats = stats ?? ItemStats.None;
        PassiveName = passiveName; PassiveDescription = passiveDesc;
        Recipe = recipe ?? System.Array.Empty<string>();
        RecipeCost = recipeCost; MaxStacks = maxStacks; Duration = duration;
        IconPath = iconPath;
    }
}

// ── Static Registry ──────────────────────────────────────────────────
/// <summary>
/// Master registry of all 50 MOBA items. Accessed by string ID.
/// </summary>
public static class ItemData
{
    private static readonly Dictionary<string, ItemInfo> _items = new()
    {
        // ═══════════════════════════════════════════════════════════
        //  CONSUMABLES (6)
        // ═══════════════════════════════════════════════════════════
        ["HealthFlask"] = new ItemInfo(
            "HealthFlask", "Health Flask",
            "Restore 120 HP over 10s.",
            ItemTier.Consumable, 50,
            ItemStats.None,
            maxStacks: 3, duration: 10f,
            iconPath: "res://assets/ui/items/item316.png"
        ),
        ["ManaDraught"] = new ItemInfo(
            "ManaDraught", "Mana Draught",
            "Restore 80 Mana over 10s.",
            ItemTier.Consumable, 50,
            ItemStats.None,
            maxStacks: 3, duration: 10f,
            iconPath: "res://assets/ui/items/item462.png"
        ),
        ["FuryTonic"] = new ItemInfo(
            "FuryTonic", "Fury Tonic",
            "Instantly gain 30 Fury.",
            ItemTier.Consumable, 75,
            ItemStats.None,
            maxStacks: 3,
            iconPath: "res://assets/ui/items/item393.png"
        ),
        ["ElixirOfFortitude"] = new ItemInfo(
            "ElixirOfFortitude", "Elixir of Fortitude",
            "+50 Max HP and +5 VIT for 3 minutes.",
            ItemTier.Consumable, 300,
            new ItemStats(vit: 5),
            duration: 180f,
            iconPath: "res://assets/ui/items/item389.png"
        ),
        ["ScrollOfSpeed"] = new ItemInfo(
            "ScrollOfSpeed", "Scroll of Speed",
            "+10 Haste, +10 Concentration, +10 Agility for 2 minutes.",
            ItemTier.Consumable, 200,
            new ItemStats(haste: 10, conc: 10, agi: 10),
            duration: 120f,
            iconPath: "res://assets/ui/items/item491.png"
        ),
        ["WardStone"] = new ItemInfo(
            "WardStone", "Ward Stone",
            "Place a vision ward (60s duration, 12u reveal).",
            ItemTier.Consumable, 75,
            ItemStats.None,
            maxStacks: 2, duration: 60f,
            iconPath: "res://assets/ui/items/item423.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  COMMON (12) — Single-stat building blocks
        // ═══════════════════════════════════════════════════════════
        ["IronBuckler"] = new ItemInfo(
            "IronBuckler", "Iron Buckler",
            "A small iron shield. Good for staying alive.",
            ItemTier.Common, 200,
            new ItemStats(vit: 5),
            iconPath: "res://assets/ui/items/item130.png"
        ),
        ["LeatherGrips"] = new ItemInfo(
            "LeatherGrips", "Leather Grips",
            "Worn leather gloves that improve striking power.",
            ItemTier.Common, 200,
            new ItemStats(str: 5),
            iconPath: "res://assets/ui/items/item208.png"
        ),
        ["ArcaneFragment"] = new ItemInfo(
            "ArcaneFragment", "Arcane Fragment",
            "A shard of crystallized magic.",
            ItemTier.Common, 200,
            new ItemStats(intel: 5),
            iconPath: "res://assets/ui/items/item401.png"
        ),
        ["SagePendant"] = new ItemInfo(
            "SagePendant", "Sage Pendant",
            "A necklace imbued with wisdom.",
            ItemTier.Common, 200,
            new ItemStats(wis: 5),
            iconPath: "res://assets/ui/items/item602.png"
        ),
        ["SwiftBoots"] = new ItemInfo(
            "SwiftBoots", "Swift Boots",
            "Light boots for quick footwork.",
            ItemTier.Common, 300,
            new ItemStats(agi: 5),
            iconPath: "res://assets/ui/items/item186.png"
        ),
        ["Whetstone"] = new ItemInfo(
            "Whetstone", "Whetstone",
            "Sharpens blades for faster strikes.",
            ItemTier.Common, 250,
            new ItemStats(haste: 5),
            iconPath: "res://assets/ui/items/item424.png"
        ),
        ["MeditationShard"] = new ItemInfo(
            "MeditationShard", "Meditation Shard",
            "A calming crystal that sharpens focus.",
            ItemTier.Common, 250,
            new ItemStats(conc: 5),
            iconPath: "res://assets/ui/items/item422.png"
        ),
        ["ChainmailVest"] = new ItemInfo(
            "ChainmailVest", "Chainmail Vest",
            "Heavy chain links woven for protection.",
            ItemTier.Common, 350,
            new ItemStats(vit: 8),
            iconPath: "res://assets/ui/items/item153.png"
        ),
        ["Broadsword"] = new ItemInfo(
            "Broadsword", "Broadsword",
            "A sturdy sword with a wide blade.",
            ItemTier.Common, 350,
            new ItemStats(str: 8),
            iconPath: "res://assets/ui/items/item4.png"
        ),
        ["Spellbook"] = new ItemInfo(
            "Spellbook", "Spellbook",
            "Arcane knowledge bound in leather.",
            ItemTier.Common, 350,
            new ItemStats(intel: 8),
            iconPath: "res://assets/ui/items/item487.png"
        ),
        ["VitalityRing"] = new ItemInfo(
            "VitalityRing", "Vitality Ring",
            "A ring that pulses with life energy.",
            ItemTier.Common, 300,
            new ItemStats(vit: 3, wis: 3),
            iconPath: "res://assets/ui/items/item648.png"
        ),
        ["HuntersQuiver"] = new ItemInfo(
            "HuntersQuiver", "Hunter's Quiver",
            "A quiver designed for rapid draws.",
            ItemTier.Common, 300,
            new ItemStats(str: 3, haste: 3),
            iconPath: "res://assets/ui/items/item555.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  UNCOMMON — Crafted (8)
        // ═══════════════════════════════════════════════════════════
        ["Platemail"] = new ItemInfo(
            "Platemail", "Platemail",
            "Heavy armor forged from iron plates.",
            ItemTier.Uncommon, 700,
            new ItemStats(vit: 15),
            recipe: new[] { "IronBuckler", "ChainmailVest" },
            recipeCost: 150,
            iconPath: "res://assets/ui/items/item154.png"
        ),
        ["WarriorsEdge"] = new ItemInfo(
            "WarriorsEdge", "Warrior's Edge",
            "A blade tempered for battle-hardened fighters.",
            ItemTier.Uncommon, 750,
            new ItemStats(str: 15, haste: 3),
            recipe: new[] { "LeatherGrips", "Broadsword" },
            recipeCost: 200,
            iconPath: "res://assets/ui/items/item5.png"
        ),
        ["ChannelersFocus"] = new ItemInfo(
            "ChannelersFocus", "Channeler's Focus",
            "A crystal orb that amplifies magical flow.",
            ItemTier.Uncommon, 750,
            new ItemStats(intel: 15, conc: 3),
            recipe: new[] { "ArcaneFragment", "Spellbook" },
            recipeCost: 200,
            iconPath: "res://assets/ui/items/item399.png"
        ),
        ["PilgrimsSandals"] = new ItemInfo(
            "PilgrimsSandals", "Pilgrim's Sandals",
            "Sandals blessed for long journeys.",
            ItemTier.Uncommon, 700,
            new ItemStats(agi: 10, wis: 5),
            recipe: new[] { "SwiftBoots", "SagePendant" },
            recipeCost: 200,
            iconPath: "res://assets/ui/items/item200.png"
        ),
        ["RapidEdges"] = new ItemInfo(
            "RapidEdges", "Rapid Edges",
            "Twin blades designed for relentless attacks.",
            ItemTier.Uncommon, 700,
            new ItemStats(haste: 10, str: 5),
            recipe: new[] { "Whetstone", "LeatherGrips" },
            recipeCost: 250,
            iconPath: "res://assets/ui/items/item15.png"
        ),
        ["TomeOfInsight"] = new ItemInfo(
            "TomeOfInsight", "Tome of Insight",
            "Ancient wisdom that reduces casting delays.",
            ItemTier.Uncommon, 700,
            new ItemStats(conc: 10, intel: 5),
            recipe: new[] { "MeditationShard", "ArcaneFragment" },
            recipeCost: 250,
            iconPath: "res://assets/ui/items/item488.png"
        ),
        ["GuardiansRing"] = new ItemInfo(
            "GuardiansRing", "Guardian's Ring",
            "A ring worn by temple protectors.",
            ItemTier.Uncommon, 800,
            new ItemStats(vit: 8, wis: 8),
            recipe: new[] { "ChainmailVest", "SagePendant" },
            recipeCost: 250,
            iconPath: "res://assets/ui/items/item647.png"
        ),
        ["RangersLongbow"] = new ItemInfo(
            "RangersLongbow", "Ranger's Longbow",
            "A composite bow built for rapid volleys.",
            ItemTier.Uncommon, 800,
            new ItemStats(str: 8, haste: 8),
            recipe: new[] { "HuntersQuiver", "Whetstone" },
            recipeCost: 250,
            iconPath: "res://assets/ui/items/item102.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  UNCOMMON — Standalone (4)
        // ═══════════════════════════════════════════════════════════
        ["BloodpactDagger"] = new ItemInfo(
            "BloodpactDagger", "Bloodpact Dagger",
            "A cursed dagger that feeds on wounds.",
            ItemTier.Uncommon, 900,
            new ItemStats(str: 10),
            "Siphon", "Basic attacks heal for 5% of damage dealt.",
            iconPath: "res://assets/ui/items/item599.png"
        ),
        ["RunicLantern"] = new ItemInfo(
            "RunicLantern", "Runic Lantern",
            "An enchanted lantern that reveals hidden truths.",
            ItemTier.Uncommon, 900,
            new ItemStats(intel: 10),
            "Illumination", "Abilities reveal the target (4u reveal, 3s).",
            iconPath: "res://assets/ui/items/item571.png"
        ),
        ["ThornweaveCloak"] = new ItemInfo(
            "ThornweaveCloak", "Thornweave Cloak",
            "A cloak woven from briar thorns.",
            ItemTier.Uncommon, 900,
            new ItemStats(vit: 12),
            "Thorns", "Melee attackers take 8 flat damage per hit.",
            iconPath: "res://assets/ui/items/item666.png"
        ),
        ["WindrunnerToken"] = new ItemInfo(
            "WindrunnerToken", "Windrunner Token",
            "A feathered charm carried by scouts.",
            ItemTier.Uncommon, 900,
            new ItemStats(agi: 10),
            "Tailwind", "+10% move speed for 3s after a kill or assist.",
            iconPath: "res://assets/ui/items/item483.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  RARE — Crafted (7)
        // ═══════════════════════════════════════════════════════════
        ["CrimsonBulwark"] = new ItemInfo(
            "CrimsonBulwark", "Crimson Bulwark",
            "A towering shield stained red from countless battles.",
            ItemTier.Rare, 1800,
            new ItemStats(vit: 25, wis: 10),
            "Fortified", "Reduce all incoming damage by 5%.",
            recipe: new[] { "Platemail", "GuardiansRing" },
            recipeCost: 300,
            iconPath: "res://assets/ui/items/item156.png"
        ),
        ["Worldsplitter"] = new ItemInfo(
            "Worldsplitter", "Worldsplitter",
            "A massive cleaver that splits the earth itself.",
            ItemTier.Rare, 2000,
            new ItemStats(str: 25, haste: 8),
            "Cleave", "Basic attacks deal 30% splash damage in a small cone.",
            recipe: new[] { "WarriorsEdge", "RapidEdges" },
            recipeCost: 550,
            iconPath: "res://assets/ui/items/item44.png"
        ),
        ["AstralConduit"] = new ItemInfo(
            "AstralConduit", "Astral Conduit",
            "A staff thrumming with extraplanar energy.",
            ItemTier.Rare, 2000,
            new ItemStats(intel: 25, conc: 10),
            "Aftershock", "Abilities deal 15% bonus magic damage to targets below 40% HP.",
            recipe: new[] { "ChannelersFocus", "TomeOfInsight" },
            recipeCost: 550,
            iconPath: "res://assets/ui/items/item553.png"
        ),
        ["ZephyrStriders"] = new ItemInfo(
            "ZephyrStriders", "Zephyr Striders",
            "Boots infused with captured wind spirits.",
            ItemTier.Rare, 1800,
            new ItemStats(agi: 15, haste: 10, conc: 5),
            "Fleet", "After dodging, gain +20% move speed for 2s.",
            recipe: new[] { "PilgrimsSandals", "Whetstone" },
            recipeCost: 550,
            iconPath: "res://assets/ui/items/item197.png"
        ),
        ["CrusadersOath"] = new ItemInfo(
            "CrusadersOath", "Crusader's Oath",
            "A holy vow that empowers restorative magic.",
            ItemTier.Rare, 2200,
            new ItemStats(intel: 15, vit: 15, wis: 8),
            "Devotion", "Healing done to allies is increased by 15%.",
            recipe: new[] { "ChannelersFocus", "VitalityRing" },
            recipeCost: 700,
            iconPath: "res://assets/ui/items/item611.png"
        ),
        ["PredatorsFang"] = new ItemInfo(
            "PredatorsFang", "Fang",
            "A fang-shaped blade that hungers for blood.",
            ItemTier.Rare, 2100,
            new ItemStats(str: 15, haste: 12),
            "Siphon+", "Attacks heal for 10% of damage dealt. Kills heal for 20% of the slain unit's max HP.",
            recipe: new[] { "BloodpactDagger", "RapidEdges" },
            recipeCost: 500,
            iconPath: "res://assets/ui/items/item31.png"
        ),
        ["StormweaveMantle"] = new ItemInfo(
            "StormweaveMantle", "Stormweave Mantle",
            "A cloak woven with storm energy that punishes aggressors.",
            ItemTier.Rare, 2000,
            new ItemStats(vit: 20, conc: 10),
            "Thorns+", "Melee attackers take 15 damage. Proc applies a 15% slow for 1.5s.",
            recipe: new[] { "ThornweaveCloak", "TomeOfInsight" },
            recipeCost: 400,
            iconPath: "res://assets/ui/items/item157.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  RARE — Standalone (3)
        // ═══════════════════════════════════════════════════════════
        ["ExecutionersMaul"] = new ItemInfo(
            "ExecutionersMaul", "Executioner's Maul",
            "A heavy hammer designed for finishing blows.",
            ItemTier.Rare, 2200,
            new ItemStats(str: 20),
            "Execute", "Attacks deal 8% of target's missing HP as bonus damage.",
            iconPath: "res://assets/ui/items/item97.png"
        ),
        ["NullfireBlade"] = new ItemInfo(
            "NullfireBlade", "Nullfire Blade",
            "A blade that disrupts magical energy on contact.",
            ItemTier.Rare, 2200,
            new ItemStats(intel: 15, str: 10),
            "Mana Break", "Attacks burn 15 Mana, dealing magic damage equal to Mana burned.",
            iconPath: "res://assets/ui/items/item11.png"
        ),
        ["EtherealShroud"] = new ItemInfo(
            "EtherealShroud", "Ethereal Shroud",
            "A ghostly mantle that bridges the material and spirit planes.",
            ItemTier.Rare, 2000,
            new ItemStats(vit: 15, wis: 15),
            "Phase", "Active (30s CD): Become untargetable for 1.5s. Cannot attack or cast during phase.",
            iconPath: "res://assets/ui/items/item158.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  LEGENDARY — Crafted (6)
        // ═══════════════════════════════════════════════════════════
        ["AegisOfTheImmortal"] = new ItemInfo(
            "AegisOfTheImmortal", "Aegis of the Immortal",
            "A divine shield that defies death itself.",
            ItemTier.Legendary, 4000,
            new ItemStats(vit: 35, wis: 20, conc: 10),
            "Resurrect", "Upon death, revive at 30% HP after 3s stasis. 180s cooldown.",
            recipe: new[] { "CrimsonBulwark", "EtherealShroud" },
            recipeCost: 200,
            iconPath: "res://assets/ui/items/item133.png"
        ),
        ["Dreadnought"] = new ItemInfo(
            "Dreadnought", "Dreadnought",
            "An apocalyptic weapon forged for total war.",
            ItemTier.Legendary, 4200,
            new ItemStats(str: 40, haste: 15),
            "Cleave+ / Rampage", "Attacks deal 40% splash in a cone. Each kill/assist grants +3 STR for 30s (stacks 5x).",
            recipe: new[] { "Worldsplitter", "ExecutionersMaul" },
            recipeCost: 0,
            iconPath: "res://assets/ui/items/item99.png"
        ),
        ["ArchmagesRegalia"] = new ItemInfo(
            "ArchmagesRegalia", "Archmage's Regalia",
            "Robes worn by the greatest mages in history.",
            ItemTier.Legendary, 4200,
            new ItemStats(intel: 40, conc: 15, wis: 10),
            "Amplify / Enlightenment", "All ability damage increased by 20%. Kills and assists restore 10% max Mana.",
            recipe: new[] { "AstralConduit", "MeditationShard" },
            recipeCost: 1000,
            iconPath: "res://assets/ui/items/item654.png"
        ),
        ["GaleforceTreads"] = new ItemInfo(
            "GaleforceTreads", "Galeforce Treads",
            "Boots that bend the wind to their wearer's will.",
            ItemTier.Legendary, 3500,
            new ItemStats(agi: 25, haste: 15, conc: 10),
            "Untouchable / Fleet+", "15% chance to dodge basic attacks. Kills/assists grant +30% move speed for 4s.",
            recipe: new[] { "ZephyrStriders", "WindrunnerToken" },
            recipeCost: 800,
            iconPath: "res://assets/ui/items/item199.png"
        ),
        ["ArkOfSalvation"] = new ItemInfo(
            "ArkOfSalvation", "Ark of Salvation",
            "A sacred reliquary that channels divine energy to all nearby allies.",
            ItemTier.Legendary, 4500,
            new ItemStats(intel: 25, vit: 25, wis: 15),
            "Devotion+ / Sanctuary", "Healing increased by 25%. Active (120s CD): AoE heal of 200 + 3x INT to allies within 8u.",
            recipe: new[] { "CrusadersOath", "SagePendant" },
            recipeCost: 1500,
            iconPath: "res://assets/ui/items/item614.png"
        ),
        ["Bloodreaver"] = new ItemInfo(
            "Bloodreaver", "Bloodreaver",
            "A cursed blade that grows stronger with each life it takes.",
            ItemTier.Legendary, 4000,
            new ItemStats(str: 30, haste: 15),
            "Vampirism / Frenzy", "Attacks heal for 15% of damage dealt. Below 30% HP, gain +20 Haste for 5s (60s CD).",
            recipe: new[] { "PredatorsFang", "Broadsword" },
            recipeCost: 600,
            iconPath: "res://assets/ui/items/item660.png"
        ),

        // ═══════════════════════════════════════════════════════════
        //  LEGENDARY — Standalone (4)
        // ═══════════════════════════════════════════════════════════
        ["DivineRapier"] = new ItemInfo(
            "DivineRapier", "Divine Rapier",
            "The ultimate gamble. Immense power at the cost of everything.",
            ItemTier.Legendary, 5000,
            new ItemStats(str: 60), // Adapts to hero damage type at runtime
            "Ultimate Risk", "Grants +60 to your primary damage stat (STR or INT). Drops on death. Can be picked up by anyone.",
            iconPath: "res://assets/ui/items/item17.png"
        ),
        ["AbyssalCrown"] = new ItemInfo(
            "AbyssalCrown", "Abyssal Crown",
            "A crown forged in the deep abyss that devours magic.",
            ItemTier.Legendary, 4000,
            new ItemStats(intel: 30, vit: 20),
            "Spellshield / Domination", "Blocks the first enemy ability (45s CD). Abilities reduce enemy healing by 40% for 3s.",
            iconPath: "res://assets/ui/items/item140.png"
        ),
        ["SentinelsBastion"] = new ItemInfo(
            "SentinelsBastion", "Sentinel's Bastion",
            "An immovable fortress that protects all who stand behind it.",
            ItemTier.Legendary, 4000,
            new ItemStats(vit: 40, wis: 15),
            "Fortified+ / Bulwark", "Reduce incoming damage by 10%. Active (90s CD): Shield allies within 6u for 15% of your Max HP for 5s.",
            iconPath: "res://assets/ui/items/item215.png"
        ),
        ["PhantomEdge"] = new ItemInfo(
            "PhantomEdge", "Phantom Edge",
            "A blade that strikes from the shadow between worlds.",
            ItemTier.Legendary, 3800,
            new ItemStats(str: 25, agi: 25, haste: 10),
            "Shadow Strike / Ghostwalk", "Every 4th basic attack deals 150% damage and cannot miss. +5% move speed permanently.",
            iconPath: "res://assets/ui/items/item18.png"
        ),
    };

    // ── Accessors ────────────────────────────────────────────────

    /// <summary>Get item info by ID. Returns null if not found.</summary>
    public static ItemInfo Get(string itemId)
    {
        return _items.TryGetValue(itemId, out var info) ? info : null;
    }

    /// <summary>Get all items as a read-only dictionary.</summary>
    public static IReadOnlyDictionary<string, ItemInfo> GetAll() => _items;

    /// <summary>Get all items of a specific tier.</summary>
    public static List<ItemInfo> GetByTier(ItemTier tier)
    {
        var result = new List<ItemInfo>();
        foreach (var kvp in _items)
            if (kvp.Value.Tier == tier) result.Add(kvp.Value);
        return result;
    }

    /// <summary>Check if an item can be crafted from components.</summary>
    public static bool IsCraftable(string itemId)
    {
        var info = Get(itemId);
        return info != null && info.Recipe.Length > 0;
    }

    /// <summary>
    /// Calculate total gold cost including all components recursively.
    /// </summary>
    public static int GetTotalCost(string itemId)
    {
        var info = Get(itemId);
        if (info == null) return 0;
        if (info.Recipe.Length == 0) return info.GoldCost;

        int total = info.RecipeCost;
        foreach (var compId in info.Recipe)
            total += GetTotalCost(compId);
        return total;
    }
}
