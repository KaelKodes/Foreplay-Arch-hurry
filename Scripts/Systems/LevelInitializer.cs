using Godot;
using Archery;

public partial class LevelInitializer : Node
{
    public override void _Ready()
    {
        if (NetworkManager.Instance != null)
        {
            GD.Print("[LevelInitializer] Level ready, notifying NetworkManager.");
            NetworkManager.Instance.CallDeferred(nameof(NetworkManager.LevelLoaded), GetParent());
        }
        else
        {
            GD.PrintErr("[LevelInitializer] NetworkManager Instance not found!");
        }
    }
}
