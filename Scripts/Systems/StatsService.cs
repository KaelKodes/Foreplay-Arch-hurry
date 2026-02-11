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
                command.CommandText = "SELECT Level, Experience, Gold, Strength, Agility, Wisdom, Vitality, Intelligence, Haste, Concentration, Stance, DamageType, ResourceType, MaxHealth, CurrentHealth, MaxStamina, CurrentStamina, MaxMana, CurrentMana, MaxFury, CurrentFury, IsRightHanded FROM Characters WHERE Name = @name";
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
                        _playerStats.Wisdom = reader.GetInt32(5);
                        _playerStats.Vitality = reader.GetInt32(6);
                        _playerStats.Intelligence = reader.GetInt32(7);
                        _playerStats.Haste = reader.GetInt32(8);
                        _playerStats.Concentration = reader.GetInt32(9);
                        _playerStats.Stance = (HeroStance)reader.GetInt32(10);
                        _playerStats.DamageType = (DamageType)reader.GetInt32(11);
                        _playerStats.ResourceType = (ResourceType)reader.GetInt32(12);
                        _playerStats.MaxHealth = reader.GetInt32(13);
                        _playerStats.CurrentHealth = reader.GetInt32(14);
                        _playerStats.MaxStamina = reader.GetInt32(15);
                        _playerStats.CurrentStamina = reader.GetInt32(16);
                        _playerStats.MaxMana = reader.GetInt32(17);
                        _playerStats.CurrentMana = reader.GetInt32(18);
                        _playerStats.MaxFury = reader.GetInt32(19);
                        _playerStats.CurrentFury = reader.GetInt32(20);
                        _playerStats.IsRightHanded = reader.GetInt32(21) == 1;
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
                    Strength = @str, Agility = @agi, Wisdom = @wis,
                    Vitality = @vit, Intelligence = @int,
                    Haste = @haste, Concentration = @conc,
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
                command.Parameters.AddWithValue("@wis", _playerStats.Wisdom);
                command.Parameters.AddWithValue("@vit", _playerStats.Vitality);
                command.Parameters.AddWithValue("@int", _playerStats.Intelligence);
                command.Parameters.AddWithValue("@haste", _playerStats.Haste);
                command.Parameters.AddWithValue("@conc", _playerStats.Concentration);
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
        // +2 to core attributes per level
        _playerStats.Strength += 2;
        _playerStats.Intelligence += 2;
        _playerStats.Vitality += 2;
        _playerStats.Wisdom += 2;
        _playerStats.Agility += 2;
        // Haste and Concentration do NOT increase on level-up (items/buffs only)

        // Recalculate derived vitals from stats
        _playerStats.MaxHealth = 20 * _playerStats.Vitality;
        _playerStats.CurrentHealth = _playerStats.MaxHealth; // Full heal

        _playerStats.MaxStamina = 100 + (5 * _playerStats.Agility);
        _playerStats.CurrentStamina = _playerStats.MaxStamina;

        _playerStats.MaxMana = 15 * _playerStats.Wisdom;
        _playerStats.CurrentMana = _playerStats.MaxMana;

        // Grant points
        _playerStats.AbilityPoints += 1;
        _playerStats.AttributePoints += 5;

        GD.Print($"[StatsService] Level {_playerStats.Level}! STR:{_playerStats.Strength} INT:{_playerStats.Intelligence} VIT:{_playerStats.Vitality} WIS:{_playerStats.Wisdom} AGI:{_playerStats.Agility}");
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

    // Riverside logic for objective kills.
}
