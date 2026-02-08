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
                    Dexterity INTEGER DEFAULT 10,
                    Vitality INTEGER DEFAULT 10,
                    Intelligence INTEGER DEFAULT 10,
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

                -- Migration: Repair/Seed base stats for existing users who have default 100 HP
                UPDATE Characters SET MaxHealth = 400, CurrentHealth = 400, MaxStamina = 275, CurrentStamina = 275, MaxMana = 270, CurrentMana = 270, Strength = 21, Agility = 35, Dexterity = 42, Vitality = 20, Intelligence = 18 WHERE Name = 'Ranger' AND MaxHealth = 100 AND Level = 1;
                UPDATE Characters SET MaxHealth = 760, CurrentHealth = 760, MaxStamina = 200, CurrentStamina = 200, MaxMana = 180, CurrentMana = 180, Strength = 42, Agility = 20, Dexterity = 25, Vitality = 38, Intelligence = 12 WHERE Name = 'Warrior' AND MaxHealth = 100 AND Level = 1;
                UPDATE Characters SET MaxHealth = 500, CurrentHealth = 500, MaxStamina = 190, CurrentStamina = 190, MaxMana = 675, CurrentMana = 675, Strength = 15, Agility = 18, Dexterity = 20, Vitality = 25, Intelligence = 45 WHERE Name = 'Cleric' AND MaxHealth = 100 AND Level = 1;
                UPDATE Characters SET MaxHealth = 400, CurrentHealth = 400, MaxStamina = 175, CurrentStamina = 175, MaxMana = 750, CurrentMana = 750, Strength = 12, Agility = 15, Dexterity = 18, Vitality = 20, Intelligence = 50 WHERE Name = 'Necromancer' AND MaxHealth = 100 AND Level = 1;

                -- Seed heroes with class-specific stats if table is empty
                -- Formulas: HP = 20 * Vitality, SP = 100 + 5 * Agility, MP = 15 * Intelligence
                -- Values derived from growth curves (scaled by 10) + base floor.
                INSERT OR IGNORE INTO Characters (Id, Name, Level, Experience, Gold, Strength, Agility, Dexterity, Vitality, Intelligence, MaxHealth, CurrentHealth, MaxStamina, CurrentStamina, MaxMana, CurrentMana, MaxFury, CurrentFury, IsRightHanded)
                VALUES
                    -- Ranger/Archer: High DEX/AGI. HP=400, SP=275, MP=270
                    (1, 'Ranger',      1, 0, 0,  21, 35, 42, 20, 18,  400, 400,  275, 275,  270, 270,  100, 0,  1),
                    -- Warrior: High STR/VIT. HP=760, SP=200, MP=180 (Uses Fury 0/100)
                    (2, 'Warrior',     1, 0, 0,  42, 20, 25, 38, 12,  760, 760,  200, 200,  180, 180,  100, 0,  1),
                    -- Cleric: High INT/VIT. HP=500, SP=190, MP=675
                    (3, 'Cleric',      1, 0, 0,  15, 18, 20, 25, 45,  500, 500,  190, 190,  675, 675,  100, 0,  1),
                    -- Necromancer: Highest INT. HP=400, SP=175, MP=750
                    (4, 'Necromancer', 1, 0, 0,  12, 15, 18, 20, 50,  400, 400,  175, 175,  750, 750,  100, 0,  1);
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
