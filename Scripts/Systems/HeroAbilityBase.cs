using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Data structure for an ability perk.
/// </summary>
public class AbilityPerk
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconPath { get; set; } = "";

    // ─── Legacy Simple Effects ───────────────────────────────────
    public float DamageMultiplier { get; set; } = 1.0f;
    public float CooldownReduction { get; set; } = 0.0f;
    public float RadiusBonus { get; set; } = 0.0f;

    // ─── Advanced Numeric Effects ────────────────────────────────
    public float DurationBonus { get; set; } = 0.0f;          // Adds to ability duration (seconds)
    public float MovementSpeedBonus { get; set; } = 0.0f;     // Multiplier (0.5 = +50%)
    public float RangeBonus { get; set; } = 0.0f;             // Multiplier (0.5 = +50%)
    public float ProjectileSpeedMod { get; set; } = 0.0f;     // Multiplier (-0.25 = -25% speed)
    public float SlowAmount { get; set; } = 0.0f;             // Slow % (0.4 = 40% slow)
    public float SlowDuration { get; set; } = 0.0f;           // Slow duration (seconds)

    // ─── DoT/Debuff Effects ──────────────────────────────────────
    public float DotDamage { get; set; } = 0.0f;              // Damage per tick
    public float DotDuration { get; set; } = 0.0f;            // Total duration (seconds)
    public float DotTickRate { get; set; } = 1.0f;           // Ticks per second
    public string DotType { get; set; } = "";                 // "burn", "bleed", "poison", etc.
    public float DamageAmplification { get; set; } = 0.0f;    // Enemies take +X% damage from all sources
    public float CritChanceBonus { get; set; } = 0.0f;        // +X% crit chance vs affected targets

    // ─── CC/Utility Effects ──────────────────────────────────────
    public float StunDuration { get; set; } = 0.0f;           // Stun duration (seconds)
    public float PullDistance { get; set; } = 0.0f;           // Pull enemies X units
    public bool GrantsVisionInArea { get; set; } = false;     // Ability grants vision
    public float VisionDuration { get; set; } = 0.0f;         // Vision duration after ability ends

    // ─── Behavioral Flags ────────────────────────────────────────
    public bool GrantsCCImmunity { get; set; } = false;       // Can't be interrupted
    public bool EnablesChaining { get; set; } = false;        // Projectile chains to nearby enemies
    public int ChainCount { get; set; } = 0;                  // Max chain targets
    public bool EnablesSplitProjectile { get; set; } = false; // Projectile splits
    public int SplitCount { get; set; } = 0;                  // Number of split projectiles
    public bool EnablesMidAirAttack { get; set; } = false;    // Can attack during dash/jump
    public float MidAirAttackDamage { get; set; } = 0.0f;     // Damage multiplier for mid-air attack
    public bool SpawnsAdditionalDecoys { get; set; } = false; // Spawn extra decoys
    public int DecoyCount { get; set; } = 0;                  // Number of decoys
    public bool DecoyExplodes { get; set; } = false;          // Decoy explodes when expired/attacked
    public float DecoyExplosionDamage { get; set; } = 0.0f;   // Explosion damage multiplier
    public float DecoyExplosionRadius { get; set; } = 0.0f;   // Explosion radius
    public bool GrantsInvisibility { get; set; } = false;     // Grant invisibility
    public float InvisibilityDuration { get; set; } = 0.0f;   // Invisibility duration (seconds)
    public bool LeavesSmokeTrial { get; set; } = false;       // Leave smoke trail
    public float SmokeTrialDuration { get; set; } = 0.0f;     // Smoke duration
    public bool ResetCooldownOnDecoyHit { get; set; } = false;// CD reset if decoy attacked

    // ─── Conditional/Dynamic Effects ─────────────────────────────
    public bool StacksOnSameTarget { get; set; } = false;     // Effect stacks when hitting same target
    public float StackBonus { get; set; } = 0.0f;             // Bonus per stack
    public int MaxStacks { get; set; } = 0;                   // Max stacks
    public bool CooldownRefundOnMultiHit { get; set; } = false; // Reduce CD if hits X+ enemies
    public int MultiHitThreshold { get; set; } = 0;            // Required hits for refund
    public float CooldownRefundAmount { get; set; } = 0.0f;    // CD reduction amount
}

/// <summary>
/// Base class for all Hero Abilities (Step 3 & 4).
/// </summary>
public abstract partial class HeroAbilityBase : Node
{
    [Export] public string AbilityName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public AbilityType Type { get; set; } = AbilityType.Auto;
    [Export] public float BaseDamage { get; set; } = 10f;
    [Export] public float BaseCooldown { get; set; } = 5f;
    [Export] public float BaseManaCost { get; set; } = 20f;

    // Multipliers/Bonuses (Step 4 Perks)
    public float DamageMultiplier { get; set; } = 1.0f;
    public float CooldownReduction { get; set; } = 0.0f;
    public float RadiusBonus { get; set; } = 0.0f;

    // Derived stats
    public float CurrentDamage => BaseDamage * DamageMultiplier;
    public float CurrentCooldown => Mathf.Max(0.1f, BaseCooldown - CooldownReduction);

    // ─── Runtime Cooldown Tracking ────────────────────────────────
    public float CooldownRemaining { get; private set; } = 0f;
    public bool IsOnCooldown => CooldownRemaining > 0f;

    /// <summary>
    /// Starts the cooldown timer using CurrentCooldown (respects CDR).
    /// </summary>
    public void StartCooldown()
    {
        CooldownRemaining = CurrentCooldown;
    }

    /// <summary>
    /// Starts the cooldown with an additional multiplier (e.g. from Concentration stat).
    /// </summary>
    public void StartCooldown(float cdMultiplier)
    {
        CooldownRemaining = CurrentCooldown * cdMultiplier;
    }

    /// <summary>
    /// Tick down the cooldown. Call from PlayerController._PhysicsProcess.
    /// </summary>
    public void UpdateCooldown(float delta)
    {
        if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= delta;
            if (CooldownRemaining < 0f) CooldownRemaining = 0f;
        }
    }

    public int CurrentLevel { get; set; } = 1;
    public List<AbilityPerk> ActivePerks { get; private set; } = new();
    public int AbilitySlot { get; set; }

    /// <summary>
    /// Executes the ability logic.
    /// </summary>
    /// <param name="caster">The player using the ability.</param>
    public abstract void Execute(PlayerController caster);

    /// <summary>
    /// Returns 3 random perks valid for this ability level-up.
    /// </summary>
    public virtual List<AbilityPerk> GetRandomPerks(int count = 3)
    {
        // To be implemented by subclasses or a central registry
        return new List<AbilityPerk>();
    }

    public void ApplyPerk(AbilityPerk perk)
    {
        ActivePerks.Add(perk);
        GD.Print($"[Ability] {AbilityName} received perk: {perk.Name}");
    }

    protected float GetModifiedDamage()
    {
        float multiplier = 1.0f;
        foreach (var p in ActivePerks) multiplier *= p.DamageMultiplier;
        return BaseDamage * multiplier;
    }

    protected float GetModifiedCooldown()
    {
        float reduction = 0f;
        foreach (var p in ActivePerks) reduction += p.CooldownReduction;
        return Mathf.Max(0.5f, BaseCooldown - reduction);
    }
}
