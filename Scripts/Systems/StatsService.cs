using Godot;
using System;

namespace Archery;
public partial class StatsService : Node
{
    private Stats _playerStats = new Stats();
    private string _currentHeroName = "Ranger";

    public Stats PlayerStats => _playerStats;

    public void LoadStats(string heroName = "Ranger")
    {
        _currentHeroName = heroName;
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Level, Experience, Gold, Strength, Agility, Dexterity, Vitality, Intelligence, MaxHealth, CurrentHealth, MaxStamina, CurrentStamina, MaxMana, CurrentMana, MaxFury, CurrentFury, IsRightHanded FROM Characters WHERE Name = @name";
                command.Parameters.AddWithValue("@name", _currentHeroName);

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
                        _playerStats.MaxMana = reader.GetInt32(12);
                        _playerStats.CurrentMana = reader.GetInt32(13);
                        _playerStats.MaxFury = reader.GetInt32(14);
                        _playerStats.CurrentFury = reader.GetInt32(15);
                        _playerStats.IsRightHanded = reader.GetInt32(16) == 1;
                        GD.Print($"[StatsService] Found record for {_currentHeroName}. HP: {_playerStats.CurrentHealth}");
                    }
                    else
                    {
                        GD.PrintErr($"[StatsService] FAILED to find record for hero: {_currentHeroName}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to load stats for {_currentHeroName}: {e.Message}");
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
                    MaxStamina = @mst, CurrentStamina = @cst,
                    MaxMana = @mmp, CurrentMana = @cmp,
                    MaxFury = @mfu, CurrentFury = @cfu
                    WHERE Name = @name";

                command.Parameters.AddWithValue("@name", _currentHeroName);
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
                command.Parameters.AddWithValue("@mmp", _playerStats.MaxMana);
                command.Parameters.AddWithValue("@cmp", _playerStats.CurrentMana);
                command.Parameters.AddWithValue("@mfu", _playerStats.MaxFury);
                command.Parameters.AddWithValue("@cfu", _playerStats.CurrentFury);

                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to save progress for {_currentHeroName}: {e.Message}");
        }
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        _playerStats.Experience += amount;
        GD.Print($"[StatsService] {amount} XP added. Total: {_playerStats.Experience}");

        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        while (true)
        {
            int requiredXp = GetRequiredXpForNextLevel();
            if (_playerStats.Experience >= requiredXp)
            {
                _playerStats.Experience -= requiredXp;
                _playerStats.Level++;
                ApplyLevelUpBonuses();
                GD.Print($"[StatsService] LEVEL UP! Now Level {_playerStats.Level}");
                EmitSignal(SignalName.LevelUp, _playerStats.Level);
            }
            else break;
        }
    }

    private int GetRequiredXpForNextLevel()
    {
        // Doubled scale: Lvl 2: 480, Lvl 3: 960, Lvl 4: 1600, Lvl 5: 2400, Lvl 6: 3600
        // Formula: 100 * Level * (Level + 1) * 0.8 (approximately matches user request curve)
        // Manual override for specific early levels to ensure exact match with chart
        switch (_playerStats.Level)
        {
            case 1: return 480;
            case 2: return 960;
            case 3: return 1600;
            case 4: return 2400;
            case 5: return 3600;
            default: return 1000 * _playerStats.Level; // Fallback for very high levels
        }
    }

    private void ApplyLevelUpBonuses()
    {
        // Simple scaling: +10% HP, +5% Resource pools, +2 to primary stats
        _playerStats.MaxHealth += (int)(_playerStats.MaxHealth * 0.10f);
        _playerStats.CurrentHealth = _playerStats.MaxHealth; // Full heal

        _playerStats.Strength += 2;
        _playerStats.Agility += 2;
        _playerStats.Dexterity += 2;
        _playerStats.Vitality += 2;
        _playerStats.Intelligence += 2;

        _playerStats.MaxStamina += (int)(_playerStats.MaxStamina * 0.05f);
        _playerStats.CurrentStamina = _playerStats.MaxStamina;

        _playerStats.MaxMana += (int)(_playerStats.MaxMana * 0.05f);
        _playerStats.CurrentMana = _playerStats.MaxMana;

        // Step 4: Grant points
        _playerStats.AbilityPoints += 1;
        _playerStats.AttributePoints += 5;

        GD.Print($"[StatsService] Points granted! Ability: {_playerStats.AbilityPoints}, Attribute: {_playerStats.AttributePoints}");
    }

    [Signal] public delegate void LevelUpEventHandler(int newLevel);
    [Signal] public delegate void ExperienceGainedEventHandler(int current, int total);
    [Signal] public delegate void AbilityUpgradedEventHandler(int slot, int newLevel, bool perkTriggered);
    [Signal] public delegate void PerkSelectedEventHandler(string perkId);

    public void UpgradeAbility(int slot)
    {
        if (slot < 0 || slot >= 4) return;
        if (_playerStats.AbilityPoints <= 0) return;
        if (_playerStats.AbilityLevels[slot] >= 6) return;

        _playerStats.AbilityPoints--;
        _playerStats.AbilityLevels[slot]++;

        int newLevel = _playerStats.AbilityLevels[slot];
        bool perkTriggered = (newLevel % 2 == 0);

        GD.Print($"[StatsService] Ability {slot + 1} upgraded to Level {newLevel}. Points left: {_playerStats.AbilityPoints}");
        EmitSignal(SignalName.AbilityUpgraded, slot, newLevel, perkTriggered);
    }

    public void SelectPerk(string perkId)
    {
        if (!_playerStats.SelectedPerks.Contains(perkId))
        {
            _playerStats.SelectedPerks.Add(perkId);
            GD.Print($"[StatsService] Perk selected: {perkId}");
            EmitSignal(SignalName.PerkSelected, perkId);
        }
    }
    // Riverside logic for objective kills.
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _playerStats.Gold += amount;
        GD.Print($"[StatsService] {amount} Gold added. Total: {_playerStats.Gold}");
    }

    public void UpdateAnger(float accuracyError)
    {
        // Legacy anger mechanic from golf era - disabled for Arch Hurry
    }
}
