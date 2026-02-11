namespace Archery;

/// <summary>
/// Difficulty tiers for AI heroes.
/// </summary>
public enum BotDifficulty
{
    Beginner,
    Intermediate,
    Elite
}

/// <summary>
/// Static tuning parameters for each difficulty tier.
/// Controls reaction time, aim accuracy, retreat thresholds, etc.
/// </summary>
public static class BotDifficultyProfile
{
    // ── Reaction Delay (seconds) ──────────────────────
    public static float GetReactionDelayMin(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 0.4f,
        BotDifficulty.Intermediate => 0.2f,
        BotDifficulty.Elite => 0.08f,
        _ => 0.4f
    };

    public static float GetReactionDelayMax(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 0.6f,
        BotDifficulty.Intermediate => 0.3f,
        BotDifficulty.Elite => 0.15f,
        _ => 0.6f
    };

    // ── Aim scatter (degrees of random offset) ────────
    public static float GetAimScatter(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 15f,
        BotDifficulty.Intermediate => 8f,
        BotDifficulty.Elite => 2f,
        _ => 15f
    };

    // ── Retreat HP threshold (fraction of max HP) ─────
    public static float GetRetreatHpPercent(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 0.30f,
        BotDifficulty.Intermediate => 0.25f,
        BotDifficulty.Elite => 0.20f,
        _ => 0.30f
    };

    // ── Recall HP threshold (fraction of max HP) ──────
    public static float GetRecallHpPercent(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 0.20f,
        BotDifficulty.Intermediate => 0.18f,
        BotDifficulty.Elite => 0.15f,
        _ => 0.20f
    };

    // ── Engagement range (units to start fighting) ────
    public static float GetEngagementRange(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 12f,
        BotDifficulty.Intermediate => 15f,
        BotDifficulty.Elite => 18f,
        _ => 12f
    };

    // ── Ability usage cooldown multiplier ─────────────
    // Beginner: waits longer after cooldown is ready
    // Elite: uses almost immediately when ready
    public static float GetAbilityDelayMultiplier(BotDifficulty d) => d switch
    {
        BotDifficulty.Beginner => 2.0f,  // Waits 2x the reaction delay
        BotDifficulty.Intermediate => 1.0f,
        BotDifficulty.Elite => 0.5f,
        _ => 2.0f
    };
}
