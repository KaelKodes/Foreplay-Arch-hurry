using Godot;
using Archery;

public static class ProjectilePhysics
{
    public struct ProjectileParams
    {
        public float PowerValue;
        public float AccuracyValue;
        public Vector2 SpinIntent;
        public float PowerOverride;
        public Stats PlayerStats;
        public Vector3 CameraForward;
        public bool IsRightHanded;
    }

    public struct ProjectileResult
    {
        public Vector3 Velocity;
        public Vector3 Spin;
    }

    public static ProjectileResult CalculateProjectile(ProjectileParams p)
    {
        // 1. Initial Launch Angle (Generalised for Archery)
        // Flatten camera direction to horizontal plane (XZ) so looking down doesn't kill the shot
        Vector3 direction = new Vector3(p.CameraForward.X, 0, p.CameraForward.Z).Normalized();

        // Standardized to 12 degrees to match AimAssist and achieve ~50y range
        float launchLoft = 12.0f; // Fixed 12-degree launch relative to horizon
        float loftRad = Mathf.DegToRad(launchLoft);

        // We want the arrow to fly where the camera points, plus a slight upward bias for distance
        // Ensure the launch angle always has a slight upward tilt to prevent instant terrain impact
        // Apply loft directly to the flattened vector
        direction.Y = Mathf.Sin(loftRad);
        direction = direction.Normalized();

        // 2. Power and Velocity
        float powerToUse = (p.PowerOverride > 0) ? p.PowerOverride : p.PlayerStats.Strength;
        float powerStatMult = powerToUse / 10.0f;

        float baseVelocity = ArcheryConstants.BASE_VELOCITY;
        float normalizedPower = p.PowerValue / ArcheryConstants.PEAK_POWER_VALUE;
        float launchPower = normalizedPower * baseVelocity * powerStatMult;

        // 3. Accuracy and Side Spin
        float accuracyError = p.AccuracyValue - ArcheryConstants.PERFECT_ACCURACY_VALUE;

        // Control reduction
        float controlMult = 1.0f / (p.PlayerStats.Agility / 10.0f);
        float timingOffset = -accuracyError * 0.02f * controlMult; // Reduced error influence for archery

        Vector3 velocity = direction * launchPower;
        velocity = velocity.Rotated(Vector3.Up, timingOffset);

        // 4. Spin (Optional for arrows, but kept for "Curve Shots")
        float touchMult = 1.0f; // Flat spin factor (Dexterity removed)
        float baselineBackspin = 50.0f * (normalizedPower * powerStatMult); // Very low backspin for arrows

        float totalBackspin = baselineBackspin + (p.SpinIntent.Y * 20.0f * touchMult);
        float totalSidespin = (p.SpinIntent.X * 30.0f * touchMult);

        Vector3 launchDirHorizontal = new Vector3(velocity.X, 0, velocity.Z).Normalized();
        Vector3 rightDir = launchDirHorizontal.Cross(Vector3.Up).Normalized();

        Vector3 spin = (rightDir * totalBackspin) + (Vector3.Up * totalSidespin);

        return new ProjectileResult { Velocity = velocity, Spin = spin };
    }
}
