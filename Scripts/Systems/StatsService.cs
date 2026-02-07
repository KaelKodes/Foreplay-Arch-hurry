using Godot;
using System;

namespace Archery;
public partial class StatsService : Node
{
    private Stats _playerStats = new Stats();

    public Stats PlayerStats => _playerStats;

    public void LoadStats()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Level, Experience, Gold, Strength, Agility, Dexterity, Vitality, Intelligence, MaxHealth, CurrentHealth, MaxStamina, CurrentStamina, IsRightHanded FROM Characters WHERE Id = 1";
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _playerStats.Level = reader.GetInt32(0);
                        _playerStats.Experience = reader.GetInt32(1);
                        _playerStats.Gold = reader.GetInt32(2);
                        _playerStats.Strength = reader.GetInt32(3);
                        _playerStats.Agility = reader.GetInt32(4);
                        _playerStats.Dexterity = reader.GetInt32(5);
                        _playerStats.Vitality = reader.GetInt32(6);
                        _playerStats.Intelligence = reader.GetInt32(7);
                        _playerStats.MaxHealth = reader.GetInt32(8);
                        _playerStats.CurrentHealth = reader.GetInt32(9);
                        _playerStats.MaxStamina = reader.GetInt32(10);
                        _playerStats.CurrentStamina = reader.GetInt32(11);
                        _playerStats.IsRightHanded = reader.GetInt32(12) == 1;
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to load stats: {e.Message}");
        }
    }

    public void SavePlayerProgress()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"UPDATE Characters SET 
                    Level = @lvl, Experience = @xp, Gold = @gold,
                    Strength = @str, Agility = @agi, Dexterity = @dex,
                    Vitality = @vit, Intelligence = @int,
                    MaxHealth = @mhp, CurrentHealth = @chp,
                    MaxStamina = @mst, CurrentStamina = @cst
                    WHERE Id = 1";

                command.Parameters.AddWithValue("@lvl", _playerStats.Level);
                command.Parameters.AddWithValue("@xp", _playerStats.Experience);
                command.Parameters.AddWithValue("@gold", _playerStats.Gold);
                command.Parameters.AddWithValue("@str", _playerStats.Strength);
                command.Parameters.AddWithValue("@agi", _playerStats.Agility);
                command.Parameters.AddWithValue("@dex", _playerStats.Dexterity);
                command.Parameters.AddWithValue("@vit", _playerStats.Vitality);
                command.Parameters.AddWithValue("@int", _playerStats.Intelligence);
                command.Parameters.AddWithValue("@mhp", _playerStats.MaxHealth);
                command.Parameters.AddWithValue("@chp", _playerStats.CurrentHealth);
                command.Parameters.AddWithValue("@mst", _playerStats.MaxStamina);
                command.Parameters.AddWithValue("@cst", _playerStats.CurrentStamina);

                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to save progress: {e.Message}");
        }
    }

    public void UpdateAnger(float accuracyError)
    {
        // Legacy anger mechanic from golf era - disabled for Arch Hurry
    }
}
