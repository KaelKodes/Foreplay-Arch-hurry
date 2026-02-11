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
            "Channel a barrage of arrows for 5s. Grants +50 Haste.",
            "1.2s", "10/sec", "Stamina",
            "+1s duration, +10 Haste"
        ),
        ["PiercingShot"] = new AbilityInfo(
            "Piercing Shot",
            "Fire a powerful arrow dealing 120% AD that passes through enemies.",
            "1.5s", "40", "Mana",
            "+30% AD scaling, +5u range"
        ),
        ["RainOfArrows"] = new AbilityInfo(
            "Rain of Arrows",
            "Rain arrows on a 5u area dealing 20% AD every 0.5s for 3s.",
            "2.0s", "60", "Mana",
            "+1s duration, +1u radius"
        ),
        ["Vault"] = new AbilityInfo(
            "Vault",
            "Dash 8u forward, becoming untargetable briefly.",
            "0.8s", "30", "Stamina",
            "-0.2s cooldown, +2u dash distance"
        ),

        // ── Warrior ─────────────────────────────────────────────
        ["ShieldSlam"] = new AbilityInfo(
            "Shield Slam",
            "Bash your shield dealing 80% AD and stunning the target for 1.0s.",
            "1.5s", "20", "Stamina",
            "+0.5s stun, +20% AD scaling"
        ),
        ["Intercept"] = new AbilityInfo(
            "Intercept",
            "Charge 15u toward a target dealing 60% AD and knocking them back on impact.",
            "1.0s", "25", "Stamina",
            "+5u range, +30% knockback force"
        ),
        ["DemoralizingShout"] = new AbilityInfo(
            "Demoralizing Shout",
            "Shout in a 6u radius, taunting enemies for 3s and reducing their AP by 15%.",
            "2.0s", "30", "Fury",
            "+1s taunt duration, -5% more AP reduction"
        ),
        ["AvatarOfWar"] = new AbilityInfo(
            "Avatar of War",
            "Enter a berserker state for 10s, gaining 30% lifesteal and CC immunity.",
            "3.0s", "100", "Fury",
            "+3s duration, +10% lifesteal"
        ),

        // ── Cleric ──────────────────────────────────────────────
        ["HighRemedy"] = new AbilityInfo(
            "High Remedy",
            "Heal a target for 100 + (2.0 × INT) at 8u range.",
            "2.0s", "15", "Mana",
            "+50 base heal, +0.5 INT scaling"
        ),
        ["CelestialBuff"] = new AbilityInfo(
            "Celestial Buff",
            "Grant a target +10 Armor and +15 Speed for 30s. Speed boosts Haste, Concentration, and Agility.",
            "2.0s", "30", "Mana",
            "+5 Armor, +5 Speed"
        ),
        ["Judgement"] = new AbilityInfo(
            "Judgement",
            "Smite an enemy for 60 + (1.2 × INT) damage, reducing their damage by 15% for 4s.",
            "1.5s", "25", "Mana",
            "+30 base damage, +1s debuff duration"
        ),
        ["DivineIntervention"] = new AbilityInfo(
            "Divine Intervention",
            "Shield an ally from death for 10s. If lethal damage is taken, they survive at 1 HP.",
            "3.0s", "150", "Mana",
            "+25 shield, +1.0s cooldown"
        ),

        // ── Necromancer ─────────────────────────────────────────
        ["Lifetap"] = new AbilityInfo(
            "Lifetap",
            "Drain an enemy for 80 + (1.5 × INT) damage, healing yourself for 40% of the damage.",
            "1.2s", "20", "Mana",
            "+20 base damage, +10% heal ratio"
        ),
        ["PlagueOfDarkness"] = new AbilityInfo(
            "Plague of Darkness",
            "Inflict a plague dealing 20 damage every 0.5s for 10s at 15u range.",
            "2.0s", "40", "Mana",
            "+5 damage per tick, plague spreads on death"
        ),
        ["SummonSkeleton"] = new AbilityInfo(
            "Summon Skeleton",
            "Consume a nearby corpse to deal 40 damage in a 6u radius and summon a skeleton warrior with 150 + (10 × LVL) HP.",
            "3.0s", "30", "Mana",
            "+50 pet HP, +5u explosion radius"
        ),
        ["LotusTrap"] = new AbilityInfo(
            "Lotus Trap",
            "Plant a hidden lotus trap that snares enemies for 40% speed and forces nearby skeletons to swarm the target.",
            "5.0s", "30", "Mana",
            "+1s snare duration, +5u swarm range"
        ),
    };

    /// <summary>Get ability info by name. Returns null if not found.</summary>
    public static AbilityInfo Get(string abilityName)
    {
        return _abilities.TryGetValue(abilityName, out var info) ? info : null;
    }
}
