using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Central registry for Hero Perks (Step 4).
/// </summary>
public static class PerkRegistry
{
    private static Dictionary<string, List<AbilityPerk>> _perkPools = new();

    static PerkRegistry()
    {
        InitializeRangerPerks();
        InitializeWarriorPerks();
    }

    private static void InitializeRangerPerks()
    {
        // ═══════════════════════════════════════════════════════════
        // RAPID FIRE - Channel: +50% Attack Speed for 5s
        // ═══════════════════════════════════════════════════════════
        _perkPools["Ranger_RapidFire"] = new List<AbilityPerk> {
            new AbilityPerk {
                Id = "rf_volley",
                Name = "Volley",
                Description = "Each arrow splits into 3 arrows in a cone",
                EnablesSplitProjectile = true,
                SplitCount = 3
            },
            new AbilityPerk {
                Id = "rf_fleet_footwork",
                Name = "Fleet Footwork",
                Description = "+50% movement speed during channel",
                MovementSpeedBonus = 0.5f
            },
            new AbilityPerk {
                Id = "rf_searing_tips",
                Name = "Searing Tips",
                Description = "Arrows apply 2s burn (10 DPS)",
                DotDamage = 10f,
                DotDuration = 2f,
                DotTickRate = 1f,
                DotType = "burn"
            },
            new AbilityPerk {
                Id = "rf_relentless",
                Name = "Relentless",
                Description = "Channel can't be interrupted by crowd control",
                GrantsCCImmunity = true
            },
            new AbilityPerk {
                Id = "rf_static_link",
                Name = "Static Link",
                Description = "Arrows chain to 1 nearby enemy",
                EnablesChaining = true,
                ChainCount = 1
            },
            new AbilityPerk {
                Id = "rf_focus_pulse",
                Name = "Focus Pulse",
                Description = "Attacking same target grants +5% AS per hit (stacks)",
                StacksOnSameTarget = true,
                StackBonus = 0.05f,
                MaxStacks = 10
            }
        };

        // ═══════════════════════════════════════════════════════════
        // PIERCING SHOT - 120% AD piercing arrow
        // ═══════════════════════════════════════════════════════════
        _perkPools["Ranger_PiercingShot"] = new List<AbilityPerk> {
            new AbilityPerk {
                Id = "ps_long_range_bore",
                Name = "Long-Range Bore",
                Description = "+50% range, projectile speed -25%",
                RangeBonus = 0.5f,
                ProjectileSpeedMod = -0.25f
            },
            new AbilityPerk {
                Id = "ps_shattering_point",
                Name = "Shattering Point",
                Description = "Enemies hit are slowed 40% for 2s",
                SlowAmount = 0.4f,
                SlowDuration = 2f
            },
            new AbilityPerk {
                Id = "ps_serrated_edge",
                Name = "Serrated Edge",
                Description = "Enemies hit bleed for 50% damage over 4s",
                DotDamage = 0.5f, // Will be calculated as 50% of hit damage
                DotDuration = 4f,
                DotTickRate = 0.5f,
                DotType = "bleed"
            },
            new AbilityPerk {
                Id = "ps_split_arrow",
                Name = "Split Arrow",
                Description = "Arrow splits at max range into 3 directions",
                EnablesSplitProjectile = true,
                SplitCount = 3
            },
            new AbilityPerk {
                Id = "ps_resourceful",
                Name = "Resourceful",
                Description = "Cooldown reduced by 3s if hits 3+ enemies",
                CooldownRefundOnMultiHit = true,
                MultiHitThreshold = 3,
                CooldownRefundAmount = 3f
            },
            new AbilityPerk {
                Id = "ps_gravity_core",
                Name = "Gravity Core",
                Description = "Arrow pulls hit enemies 2u toward impact point",
                PullDistance = 2f
            }
        };

        // ═══════════════════════════════════════════════════════════
        // RAIN OF ARROWS - 5u AoE, 20% AD per tick, 3s duration
        // ═══════════════════════════════════════════════════════════
        _perkPools["Ranger_RainOfArrows"] = new List<AbilityPerk> {
            new AbilityPerk {
                Id = "roa_arctic_wind",
                Name = "Arctic Wind",
                Description = "Slows enemies in area by 30%",
                SlowAmount = 0.3f,
                SlowDuration = 3f
            },
            new AbilityPerk {
                Id = "roa_corrosive_rain",
                Name = "Corrosive Rain",
                Description = "Enemies take +15% damage from all sources",
                DamageAmplification = 0.15f
            },
            new AbilityPerk {
                Id = "roa_heavy_impact",
                Name = "Heavy Impact",
                Description = "First tick stuns for 0.5s",
                StunDuration = 0.5f
            },
            new AbilityPerk {
                Id = "roa_scorched_earth",
                Name = "Scorched Earth",
                Description = "Area persists 3s after duration (no damage, grants vision)",
                GrantsVisionInArea = true,
                VisionDuration = 3f
            },
            new AbilityPerk {
                Id = "roa_lingering_gale",
                Name = "Lingering Gale",
                Description = "+2s duration, -25% damage per tick",
                DurationBonus = 2f,
                DamageMultiplier = 0.75f
            },
            new AbilityPerk {
                Id = "roa_marked_for_death",
                Name = "Marked for Death",
                Description = "Enemies hit take +20% crit chance vs them for 6s",
                CritChanceBonus = 0.2f
            }
        };

        // ═══════════════════════════════════════════════════════════
        // VAULT - 8u dash, untargetable, spawn decoy
        // ═══════════════════════════════════════════════════════════
        _perkPools["Ranger_Vault"] = new List<AbilityPerk> {
            new AbilityPerk {
                Id = "vault_aerial_shot",
                Name = "Aerial Shot",
                Description = "Fire an arrow mid-vault (50% AD)",
                EnablesMidAirAttack = true,
                MidAirAttackDamage = 0.5f
            },
            new AbilityPerk {
                Id = "vault_smoke_screen",
                Name = "Smoke Screen",
                Description = "Leave smoke trail that blocks enemy vision for 3s",
                LeavesSmokeTrial = true,
                SmokeTrialDuration = 3f
            },
            new AbilityPerk {
                Id = "vault_explosive_exit",
                Name = "Explosive Exit",
                Description = "Decoy explodes after 2s (80% AD in 3u)",
                DecoyExplodes = true,
                DecoyExplosionDamage = 0.8f,
                DecoyExplosionRadius = 3f
            },
            new AbilityPerk {
                Id = "vault_wind_spirit",
                Name = "Wind Spirit",
                Description = "Gain 2s invisibility after landing",
                GrantsInvisibility = true,
                InvisibilityDuration = 2f
            },
            new AbilityPerk {
                Id = "vault_second_wind",
                Name = "Second Wind",
                Description = "Reset Vault CD if decoy is attacked",
                ResetCooldownOnDecoyHit = true
            },
            new AbilityPerk {
                Id = "vault_mirage",
                Name = "Mirage",
                Description = "Spawn 2 decoys instead of 1",
                SpawnsAdditionalDecoys = true,
                DecoyCount = 2
            }
        };

        // Fallback generic pool for compatibility
        _perkPools["Ranger"] = _perkPools["Ranger_RapidFire"];
    }

    private static void InitializeWarriorPerks()
    {
        var pools = new List<AbilityPerk> {
            new AbilityPerk { Id = "warrior_dmg_1", Name = "Heavy Strike", Description = "+25% Damage", DamageMultiplier = 1.25f },
            new AbilityPerk { Id = "warrior_cdr_1", Name = "Battle Trance", Description = "-0.8s Cooldown", CooldownReduction = 0.8f },
            new AbilityPerk { Id = "warrior_radius_1", Name = "Wide Arc", Description = "+1.5m Cleave Radius", RadiusBonus = 1.5f }
        };
        _perkPools["Warrior"] = pools;
    }

    public static List<AbilityPerk> GetRandomPerks(string heroClass, string abilityName = "", int count = 3)
    {
        // Try ability-specific pool first (e.g., "Ranger_RapidFire")
        string key = string.IsNullOrEmpty(abilityName) ? heroClass : $"{heroClass}_{abilityName}";

        // Fallback to class-wide pool, then to Ranger default
        if (!_perkPools.ContainsKey(key))
        {
            key = heroClass;
            if (!_perkPools.ContainsKey(key))
                key = "Ranger"; // Final fallback
        }

        var pool = _perkPools[key];
        var shuffled = new List<AbilityPerk>(pool);

        // Simple shuffle
        var rnd = new System.Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int k = rnd.Next(i + 1);
            var value = shuffled[k];
            shuffled[k] = shuffled[i];
            shuffled[i] = value;
        }

        return shuffled.GetRange(0, Mathf.Min(count, shuffled.Count));
    }
}
