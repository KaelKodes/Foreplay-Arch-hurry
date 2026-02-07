using Godot;

namespace Archery;

/// <summary>
/// Team affiliation for MOBA gameplay.
/// </summary>
public enum MobaTeam
{
    None,
    Red,
    Blue
}

/// <summary>
/// Central team color management with colorblind support.
/// </summary>
public static class TeamSystem
{
    // Standard colors
    private static readonly Color RedTeamColor = new Color(0.9f, 0.2f, 0.2f);
    private static readonly Color BlueTeamColor = new Color(0.2f, 0.4f, 0.9f);

    // Colorblind-friendly alternatives (Orange vs Cyan)
    private static readonly Color RedTeamColorBlind = new Color(0.9f, 0.5f, 0.1f);  // Orange
    private static readonly Color BlueTeamColorBlind = new Color(0.1f, 0.8f, 0.8f); // Cyan

    /// <summary>
    /// Get the display color for a team.
    /// </summary>
    public static Color GetTeamColor(MobaTeam team, bool colorblindMode = false)
    {
        return team switch
        {
            MobaTeam.Red => colorblindMode ? RedTeamColorBlind : RedTeamColor,
            MobaTeam.Blue => colorblindMode ? BlueTeamColorBlind : BlueTeamColor,
            _ => Colors.White
        };
    }

    /// <summary>
    /// Get the opposing team.
    /// </summary>
    public static MobaTeam GetEnemyTeam(MobaTeam team)
    {
        return team switch
        {
            MobaTeam.Red => MobaTeam.Blue,
            MobaTeam.Blue => MobaTeam.Red,
            _ => MobaTeam.None
        };
    }

    /// <summary>
    /// Check if two teams are enemies.
    /// </summary>
    public static bool AreEnemies(MobaTeam a, MobaTeam b)
    {
        if (a == MobaTeam.None || b == MobaTeam.None) return false;
        return a != b;
    }
}
