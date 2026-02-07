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
                    Name TEXT DEFAULT 'Archer',
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

                -- Initialize default character if empty
                INSERT INTO Characters (Id) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM Characters WHERE Id = 1);
            ";
            command.ExecuteNonQuery();

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
