using Godot;

namespace Archery;

/// <summary>
/// Controller for the standalone bow that can attach to any character's hand.
/// </summary>
public partial class BowController : Node3D
{
    private MeshInstance3D _bowMesh;
    private MeshInstance3D _stringMesh;

    public override void _Ready()
    {
        _bowMesh = GetNodeOrNull<MeshInstance3D>("BowBody");
        _stringMesh = GetNodeOrNull<MeshInstance3D>("BowString");
    }

    /// <summary>
    /// Sets the draw amount (0 = relaxed, 1 = fully drawn).
    /// Can be used to animate string tension.
    /// </summary>
    public void SetDrawAmount(float amount)
    {
        // Future: Animate string position based on draw amount
        // For now, just a placeholder
    }
}
