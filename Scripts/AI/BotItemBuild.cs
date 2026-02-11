using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Per-hero-class scripted item build orders.
/// Beginner bots follow these linearly. Higher tiers will adapt.
/// </summary>
public static class BotItemBuild
{
    /// <summary>
    /// Returns the ordered list of item IDs a bot of the given class should buy.
    /// Items are purchased in order as gold becomes available.
    /// </summary>
    public static List<string> GetBuildOrder(string heroClass)
    {
        return heroClass?.ToLower() switch
        {
            "ranger" => new List<string>
            {
				// Early game: cheap damage + sustain
				"health_potion",
                "short_bow",
                "leather_tunic",
				// Mid game: scaling AD
				"hunters_cloak",
                "longbow",
                "windwalkers",
				// Late game: big items
				"deadeye",
                "fang",
                "phantom_quiver"
            },

            "warrior" => new List<string>
            {
                "health_potion",
                "wooden_shield",
                "iron_gauntlets",
                "leather_tunic",
                "warplate",
                "giants_belt",
                "iron_shield",
                "titans_wrath",
                "sunfire_plate"
            },

            "cleric" => new List<string>
            {
                "health_potion",
                "mana_gem",
                "prayer_beads",
                "healers_robe",
                "luminous_pearl",
                "holy_relic",
                "divine_aegis",
                "soulguard_mantle"
            },

            "necro" => new List<string>
            {
                "health_potion",
                "mana_gem",
                "cursed_tome",
                "shadow_staff",
                "void_crystal",
                "abyssal_veil",
                "death_pact_ring",
                "lich_crown"
            },

            _ => new List<string> { "health_potion" }
        };
    }

    /// <summary>
    /// Returns the index of the next item to buy given what's already been purchased.
    /// </summary>
    public static int GetNextBuildIndex(string heroClass, int itemsBought)
    {
        var build = GetBuildOrder(heroClass);
        if (itemsBought >= build.Count) return -1; // Build complete
        return itemsBought;
    }
}
