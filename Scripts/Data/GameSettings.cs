using Godot;

namespace Archery;

/// <summary>
/// Static class to hold game settings that can be modified by a settings menu.
/// These are runtime settings - persistence can be added later.
/// </summary>
public static class GameSettings
{
    // --- Combat UI ---

    /// <summary>
    /// If true, enemy health bars appear above enemies. If false, they appear at feet level.
    /// </summary>
    public static bool HealthBarsAboveEnemy { get; set; } = false; // Default: below

    /// <summary>
    /// If true, floating damage numbers are shown when dealing damage.
    /// </summary>
    public static bool ShowDamageNumbers { get; set; } = true;

    /// <summary>
    /// If true, enemy health bars are shown when dealing damage.
    /// </summary>
    public static bool ShowEnemyHealthBars { get; set; } = true;

    // --- Audio ---

    public static float MasterVolume { get; set; } = 1.0f;
    public static float MusicVolume { get; set; } = 0.8f;
    public static float SFXVolume { get; set; } = 1.0f;

    // --- Graphics ---

    public static bool Fullscreen { get; set; } = false;
    public static bool VSync { get; set; } = true;

    // --- Gameplay ---

    public static float CameraSensitivity { get; set; } = 1.0f;
    public static bool InvertYAxis { get; set; } = false;
}
