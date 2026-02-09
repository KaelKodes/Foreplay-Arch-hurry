namespace Archery;

public enum PlayerState
{
    WalkMode,       // Default Exploration
    CombatMelee,    // Melee Combat
    CombatArcher,   // Archery
    BuildMode,      // Town Building / Placement

    SpectateMode,   // Free-cam
    PlacingObject   // Manipulating objects
}

public enum AbilityType
{
    Auto,    // Seeks smart target or hard lock
    Instant, // Fires straight forward from crosshair
    Aim,     // Hold to preview, release to fire
    Aura     // Effect around the caster
}
