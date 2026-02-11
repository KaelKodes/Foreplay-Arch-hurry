using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

// ── Hero Archetype Enums ──────────────────────────────────────────
public enum HeroStance { Melee, Ranged }
public enum DamageType { Physical, Magical }
public enum ResourceType { Mana, Fury }

public class Stats
{
    // ── Core Attributes ───────────────────────────────────────────
    public int Strength { get; set; } = 10;      // Physical Damage modifier
    public int Intelligence { get; set; } = 10;  // Magical Damage modifier
    public int Vitality { get; set; } = 10;      // Max HP
    public int Wisdom { get; set; } = 10;        // Resource pool (Mana or Fury)
    public int Agility { get; set; } = 10;       // Movement speed

    // ── Secondary Stats (start at 0, raised by items/buffs/perks) ─
    public int Haste { get; set; } = 0;          // Basic attack cooldown reduction (cap 50)
    public int Concentration { get; set; } = 0;  // Ability cooldown reduction (cap 50)

    // ── Archetype ─────────────────────────────────────────────────
    public HeroStance Stance { get; set; } = HeroStance.Melee;
    public DamageType DamageType { get; set; } = DamageType.Physical;
    public ResourceType ResourceType { get; set; } = ResourceType.Mana;

    // ── Progression ───────────────────────────────────────────────
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int Gold { get; set; } = 0;
    public int AbilityPoints { get; set; } = 0;
    public int AttributePoints { get; set; } = 0;
    public int[] AbilityLevels { get; set; } = new int[4] { 1, 1, 1, 1 };
    public List<string> SelectedPerks { get; set; } = new List<string>();

    // ── Vitals ────────────────────────────────────────────────────
    public int MaxHealth { get; set; } = 100;
    public int CurrentHealth { get; set; } = 100;
    public int CurrentShield { get; set; } = 0;
    public int MaxStamina { get; set; } = 100;
    public int CurrentStamina { get; set; } = 100;
    public int MaxMana { get; set; } = 100;
    public int CurrentMana { get; set; } = 100;
    public int MaxFury { get; set; } = 100;
    public int CurrentFury { get; set; } = 0;

    public bool IsRightHanded { get; set; } = true;

    // ── Derived Stats ─────────────────────────────────────────────

    /// <summary>
    /// Physical Damage modifier (STR-based).
    /// </summary>
    public int PhysicalDamage => Strength;

    /// <summary>
    /// Magical Damage modifier (INT-based).
    /// </summary>
    public int MagicDamage => Intelligence;

    /// <summary>
    /// Attack Damage — uses the hero's damage type to pick STR or INT.
    /// </summary>
    public int AttackDamage => DamageType == DamageType.Physical ? PhysicalDamage : MagicDamage;

    /// <summary>
    /// Movement speed derived from Agility. Base 5.0 at AGI=20.
    /// </summary>
    public float DerivedMoveSpeed => 5.0f * (Agility / 20.0f);

    /// <summary>
    /// Basic attack cooldown multiplier (1.0 = no reduction, 0.5 = minimum at cap).
    /// Haste is capped at 50.
    /// </summary>
    public float AttackCooldownMultiplier => 1.0f - (Math.Min(Haste, 50) / 100.0f);

    /// <summary>
    /// Ability cooldown multiplier (1.0 = no reduction, 0.5 = minimum at cap).
    /// Concentration is capped at 50.
    /// </summary>
    public float AbilityCooldownMultiplier => 1.0f - (Math.Min(Concentration, 50) / 100.0f);
}
