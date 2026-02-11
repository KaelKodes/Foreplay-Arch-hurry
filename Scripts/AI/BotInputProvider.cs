using Godot;

namespace Archery;

/// <summary>
/// Virtual input state that HeroBrain writes each frame.
/// Replaces Input.IsKeyPressed() for bot-controlled PlayerControllers.
/// </summary>
public class BotInputProvider
{
    // ── Movement ──────────────────────────────────────
    public Vector3 MoveDirection = Vector3.Zero;  // Normalized, world-space
    public bool WantJump = false;
    public bool WantSprint = false;

    // ── Combat ────────────────────────────────────────
    public bool WantAttackPress = false;   // Left-click press this frame
    public bool WantAttackHold = false;    // Left-click held
    public bool WantAttackRelease = false; // Left-click released this frame
    public bool WantRightClick = false;    // Right-click (target lock)
    public Node3D DesiredTarget = null;    // Who to aim at
    public int WantAbility = -1;           // Ability slot (0-3), -1 = none

    // ── Navigation ────────────────────────────────────
    public bool WantRecall = false;
    public string WantBuyItemId = null;

    // ── Facing ────────────────────────────────────────
    /// <summary>
    /// Optional look direction override. If set, bot will face this direction
    /// instead of just facing movement direction.
    /// </summary>
    public Vector3? LookDirection = null;

    /// <summary>
    /// Clear all inputs. Called at the start of each HeroBrain tick
    /// before the brain sets new values.
    /// </summary>
    public void Clear()
    {
        MoveDirection = Vector3.Zero;
        WantJump = false;
        WantSprint = false;
        WantAttackPress = false;
        WantAttackHold = false;
        WantAttackRelease = false;
        WantRightClick = false;
        DesiredTarget = null;
        WantAbility = -1;
        WantRecall = false;
        WantBuyItemId = null;
        LookDirection = null;
    }
}
