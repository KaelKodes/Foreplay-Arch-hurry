using Godot;
using System;

public class Stats
{
    // RPG Core Stats
    public int Strength { get; set; } = 10;     // Replaces Power
    public int Agility { get; set; } = 10;      // Replaces Control
    public int Dexterity { get; set; } = 10;    // Replaces Touch
    public int Vitality { get; set; } = 10;     // Replaces Consistency
    public int Intelligence { get; set; } = 10; // Replaces Focus
    public int Luck { get; set; } = 10;         // Replaces Temper

    // Progression
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int Gold { get; set; } = 0;

    // Vitals
    public int MaxHealth { get; set; } = 100;
    public int CurrentHealth { get; set; } = 100;
    public int MaxStamina { get; set; } = 100;
    public int CurrentStamina { get; set; } = 100;

    public bool IsRightHanded { get; set; } = true;

    // Legacy Mappings for compatibility during transition
    public int Power { get => Strength; set => Strength = value; }
    public int Control { get => Agility; set => Agility = value; }
    public int Touch { get => Dexterity; set => Dexterity = value; }
    public int Consistency { get => Vitality; set => Vitality = value; }
    public int Focus { get => Intelligence; set => Intelligence = value; }
    public int Temper { get => Luck; set => Luck = value; }
    public float Anger { get; set; } = 0.0f; // Kept for logic compatibility until removed

    // Hard caps for stats (AI/Default Cap: 7, Player Elite: 10)
    public const int STAT_CAP = 10;
}
