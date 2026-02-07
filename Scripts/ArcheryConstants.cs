namespace Archery;

public static class ArcheryConstants
{
    // --- HUD & VISUAL SCALING ---
    public const float UNIT_RATIO = 1.0f; // 1 meter = 1 meter in an RPG context

    // --- PHYSICS BASELINES ---
    public const float BASE_VELOCITY = 90.0f; // Increased to 90 to compensate for low stats (0.4 mult)
    public const float PEAK_POWER_VALUE = 100.0f;
    public const float PERFECT_POWER_VALUE = 94.0f; // Sweet spot
    public const float PERFECT_ACCURACY_VALUE = 25.0f; // Center of the bar

    // Forgiveness Thresholds
    public const float TOLERANCE_POWER = 3.0f;
    public const float TOLERANCE_ACCURACY = 2.5f;

    // Aerodynamic constants for arrows
    public const float ARROW_MASS = 0.045f; // kg
    public const float DRAG_COEFFICIENT = 0.00001f; // Tuned for approx vacuum flight (Force/Mass)
    public const float GRAVITY = 9.81f;
    public const float AIR_DENSITY = 1.225f;

    // --- SWING SYSTEM ---
    public const float SWING_SPEED_MULT = 1.0f;
}
