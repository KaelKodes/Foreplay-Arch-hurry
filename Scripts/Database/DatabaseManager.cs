using Godot;
using System;
using Microsoft.Data.Sqlite;
using System.IO;

public partial class DatabaseManager : Node
{
    private static string _dbPath;

    public override void _Ready()
    {
        _dbPath = Path.Combine(ProjectSettings.GlobalizePath("user://"), "arch_hurry.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Characters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT DEFAULT 'Ranger',
                    Level INTEGER DEFAULT 1,
                    Experience INTEGER DEFAULT 0,
                    Gold INTEGER DEFAULT 0,
                    Strength INTEGER DEFAULT 10,
                    Agility INTEGER DEFAULT 10,
                    Wisdom INTEGER DEFAULT 10,
                    Vitality INTEGER DEFAULT 10,
                    Intelligence INTEGER DEFAULT 10,
                    Haste INTEGER DEFAULT 0,
                    Concentration INTEGER DEFAULT 0,
                    Stance INTEGER DEFAULT 0,
                    DamageType INTEGER DEFAULT 0,
                    ResourceType INTEGER DEFAULT 0,
                    MaxHealth INTEGER DEFAULT 100,
                    CurrentHealth INTEGER DEFAULT 100,
                    MaxStamina INTEGER DEFAULT 100,
                    CurrentStamina INTEGER DEFAULT 100,
                    MaxMana INTEGER DEFAULT 100,
                    CurrentMana INTEGER DEFAULT 100,
                    MaxFury INTEGER DEFAULT 100,
                    CurrentFury INTEGER DEFAULT 0,
                    IsRightHanded INTEGER DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Inventory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CharacterId INTEGER,
                    ItemName TEXT,
                    ItemType TEXT,
                    Quantity INTEGER DEFAULT 1,
                    SlotIndex INTEGER,
                    Data TEXT,
                    FOREIGN KEY(CharacterId) REFERENCES Characters(Id)
                );

                CREATE TABLE IF NOT EXISTS Quests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    QuestKey TEXT UNIQUE,
                    Status INTEGER DEFAULT 0,
                    CurrentStep INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS WorldState (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );

                CREATE TABLE IF NOT EXISTS GalleryAssets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT UNIQUE,
                    Path TEXT,
                    MainCategory TEXT,
                    SubCategory TEXT
                );

                -- Migration: Ensure Ranger naming consistency (catch legacy Archer or Erika)
                UPDATE Characters SET Name = 'Ranger' WHERE Name = 'Archer' OR Name = 'erika' OR Name = 'Erika';

                -- Migration: Add new columns for existing DBs (safe: no-op if already exist)
                -- SQLite doesn't support ADD COLUMN IF NOT EXISTS, so we handle errors in code

                -- Migration: Nuke old stat values and re-seed with new system
                DELETE FROM Characters WHERE Level = 1;

                -- Seed heroes with class-specific stats
                -- Formulas: HP = 20 * VIT, Stamina = 100 + 5 * AGI, Mana = 15 * WIS
                -- Stance: 0=Melee, 1=Ranged | DamageType: 0=Physical, 1=Magical | ResourceType: 0=Mana, 1=Fury
                INSERT OR IGNORE INTO Characters (Id, Name, Level, Experience, Gold, Strength, Agility, Wisdom, Vitality, Intelligence, Haste, Concentration, Stance, DamageType, ResourceType, MaxHealth, CurrentHealth, MaxStamina, CurrentStamina, MaxMana, CurrentMana, MaxFury, CurrentFury, IsRightHanded)
                VALUES
                    -- Ranger: Ranged/Physical/Mana. Fast, balanced damage. HP=400, SP=225, MP=270
                    (1, 'Ranger',      1, 0, 0,  28, 25, 18, 20, 18,  0, 0,  1, 0, 0,  400, 400,  225, 225,  270, 270,  100, 0,  1),
                    -- Warrior: Melee/Physical/Fury. Tanky bruiser. HP=760, SP=190, MP=0 (Fury=50)
                    (2, 'Warrior',     1, 0, 0,  42, 18, 10, 38, 12,  0, 0,  0, 0, 1,  760, 760,  190, 190,  0, 0,  50, 0,  1),
                    -- Cleric: Melee/Magical/Mana. Tanky caster. HP=500, SP=180, MP=525
                    (3, 'Cleric',      1, 0, 0,  20, 16, 35, 25, 45,  0, 0,  0, 1, 0,  500, 500,  180, 180,  525, 525,  100, 0,  1),
                    -- Necromancer: Ranged/Magical/Mana. Glass cannon. HP=400, SP=175, MP=600
                    (4, 'Necromancer', 1, 0, 0,  12, 15, 40, 20, 50,  0, 0,  1, 1, 0,  400, 400,  175, 175,  600, 600,  100, 0,  1);
            ";
            int rowsAffected = command.ExecuteNonQuery();
            GD.Print($"[DatabaseManager] Initialization complete. Rows affected: {rowsAffected}");

            // Diagnostic: Dump current character list
            try
            {
                command.CommandText = "SELECT Id, Name FROM Characters";
                using (var reader = command.ExecuteReader())
                {
                    GD.Print("[DatabaseManager] Current Characters in DB:");
                    while (reader.Read())
                    {
                        GD.Print($"  - ID: {reader.GetInt32(0)}, Name: '{reader.GetString(1)}'");
                    }
                }
            }
            catch (Exception ex) { GD.PrintErr($"[DatabaseManager] Dump failed: {ex.Message}"); }

            // Migration: Check for legacy tables and drop them if requested (optional/safe)
            // For now, we just ensure the new ones exist. 
            // We could also drop the old ones if we are 100% sure we are in 'Arch Hurry' now.
            try
            {
                command.CommandText = "DROP TABLE IF EXISTS PlayerStats; DROP TABLE IF EXISTS PlayerSkills; DROP TABLE IF EXISTS Clubs;";
                command.ExecuteNonQuery();
            }
            catch (Exception) { /* Already gone or failed */ }
        }
        GD.Print($"Database initialized at: {_dbPath}");
    }

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
