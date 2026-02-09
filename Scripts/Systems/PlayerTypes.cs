namespace Archery;

public enum PlayerState
{
    WalkMode,       // Default Exploration
    CombatMelee,    // Melee Combat
    CombatArcher,   // Archery
    BuildMode,      // Town Building / Placement
    DriveMode,      // In a vehicle
    SpectateMode,   // Free-cam
    PlacingObject   // Manipulating objects
}
