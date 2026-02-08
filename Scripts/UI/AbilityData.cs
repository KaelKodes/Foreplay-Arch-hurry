using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Describes a single hero ability for tooltip display.
/// </summary>
public class AbilityInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Cooldown { get; set; }
    public string Cost { get; set; }
    public string CostType { get; set; }  // "Stamina", "Mana", "Fury", "HP"
    public string NextLevelPreview { get; set; }

    public AbilityInfo(string name, string desc, string cd, string cost, string costType, string nextLevel)
    {
        Name = name;
        Description = desc;
        Cooldown = cd;
        Cost = cost;
        CostType = costType;
        NextLevelPreview = nextLevel;
    }
}

/// <summary>
/// Static registry of all hero ability data for tooltip display.
/// Keys match the ability names used in ToolManager.UpdateRPGAbilities().
/// </summary>
public static class AbilityData
{
    private static readonly Dictionary<string, AbilityInfo> _abilities = new()
    {
        // ── Ranger ──────────────────────────────────────────────
        ["RapidFire"] = new AbilityInfo(
            "Rapid Fire",
            "Channel a barrage of arrows for 5s with +50% attack speed.",
            "16s", "10/sec", "Stamina",
            "+1s duration, +10% attack speed"
        ),
        ["PiercingShot"] = new AbilityInfo(
            "Piercing Shot",
            "Fire a powerful arrow dealing 120% AD that passes through enemies.",
            "10s", "40", "Mana",
            "+30% AD scaling, +5u range"
        ),
        ["RainOfArrows"] = new AbilityInfo(
            "Rain of Arrows",
            "Rain arrows on a 5u area dealing 20% AD every 0.5s for 3s.",
            "18s", "60", "Mana",
            "+1s duration, +1u radius"
        ),
        ["Vault"] = new AbilityInfo(
            "Vault",
            "Dash 8u forward, becoming untargetable briefly. Leaves a decoy behind.",
            "12s", "30", "Stamina",
            "-2s cooldown, decoy gains 10% player HP"
        ),

        // ── Warrior ─────────────────────────────────────────────
        ["ShieldSlam"] = new AbilityInfo(
            "Shield Slam",
            "Bash your shield dealing 80% AD and stunning the target for 1.0s.",
            "14s", "20", "Stamina",
            "+0.5s stun, +20% AD scaling"
        ),
        ["Intercept"] = new AbilityInfo(
            "Intercept",
            "Charge 15u toward a target, knocking them back on impact.",
            "12s", "25", "Stamina",
            "+5u range, +30% knockback force"
        ),
        ["DemoralizingShout"] = new AbilityInfo(
            "Demoralizing Shout",
            "Shout in a 6u radius, taunting enemies for 3s and reducing their AP by 15%.",
            "20s", "30", "Fury",
            "+1s taunt duration, -5% more AP reduction"
        ),
        ["AvatarOfWar"] = new AbilityInfo(
            "Avatar of War",
            "Enter a berserker state for 10s, gaining 30% lifesteal and CC immunity.",
            "120s", "100", "Fury",
            "+3s duration, +10% lifesteal"
        ),

        // ── Cleric ──────────────────────────────────────────────
        ["HighRemedy"] = new AbilityInfo(
            "High Remedy",
            "Heal a target for 150 + (2.0 × INT) at 8u range.",
            "6s", "15", "Mana",
            "+50 base heal, +0.5 INT scaling"
        ),
        ["CelestialBuff"] = new AbilityInfo(
            "Celestial Buff",
            "Grant a target +10 Armor and +10% Max HP for 30s.",
            "15s", "30", "Mana",
            "+5 Armor, +5% Max HP bonus"
        ),
        ["Judgement"] = new AbilityInfo(
            "Judgement",
            "Smite an enemy for 60 + (1.2 × INT) damage, reducing their damage by 15% for 4s.",
            "8s", "25", "Mana",
            "+30 base damage, +1s debuff duration"
        ),
        ["DivineIntervention"] = new AbilityInfo(
            "Divine Intervention",
            "Shield an ally from death for 10s. If lethal damage is taken, they survive at 1 HP.",
            "100s", "150", "Mana",
            "-15s cooldown, target healed to 25% on save"
        ),

        // ── Necromancer ─────────────────────────────────────────
        ["Lifetap"] = new AbilityInfo(
            "Lifetap",
            "Drain an enemy for 80 + (1.5 × INT) damage, healing yourself for 40% of the damage.",
            "8s", "20", "Mana",
            "+20 base damage, +10% heal ratio"
        ),
        ["PlagueOfDarkness"] = new AbilityInfo(
            "Plague of Darkness",
            "Inflict a plague dealing 20 damage every 0.5s for 10s at 15u range.",
            "12s", "40", "Mana",
            "+5 damage per tick, plague spreads on death"
        ),
        ["SummonSkeleton"] = new AbilityInfo(
            "Summon Skeleton",
            "Summon a skeleton warrior with 150 + (10 × LVL) HP and 15 + (1.2 × INT) damage.",
            "30s", "50", "Mana",
            "+50 pet HP, +5 pet damage"
        ),
        ["LichForm"] = new AbilityInfo(
            "Lich Form",
            "Transform into a Lich for 20s. Costs 2% Max HP/sec but grants +10 Mana/sec regen.",
            "100s", "2%/sec", "HP",
            "+5s duration, +3 Mana/sec regen"
        ),
    };

    /// <summary>Get ability info by name. Returns null if not found.</summary>
    public static AbilityInfo Get(string abilityName)
    {
        return _abilities.TryGetValue(abilityName, out var info) ? info : null;
    }
}
