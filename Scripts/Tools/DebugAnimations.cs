using Godot;

/// <summary>
/// Quick debug script to print what animations are available in an AnimationPlayer.
/// Add this to any scene and run to see the animation names.
/// </summary>
public partial class DebugAnimations : Node
{
    public override void _Ready()
    {
        GD.Print("=== DEBUG ANIMATIONS ===");

        // Find AnimationPlayer under Erika
        var animPlayer = GetTree().CurrentScene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

        if (animPlayer == null)
        {
            GD.PrintErr("No AnimationPlayer found!");
            return;
        }

        GD.Print($"AnimationPlayer path: {animPlayer.GetPath()}");

        // Check all animation libraries
        var libraryList = animPlayer.GetAnimationLibraryList();
        GD.Print($"Animation Libraries: {libraryList.Count}");

        foreach (var libName in libraryList)
        {
            var library = animPlayer.GetAnimationLibrary(libName);
            var animNames = library.GetAnimationList();
            GD.Print($"  Library '{libName}': {animNames.Count} animations");

            foreach (var animName in animNames)
            {
                var anim = library.GetAnimation(animName);
                string fullName = string.IsNullOrEmpty(libName) ? animName : $"{libName}/{animName}";
                GD.Print($"    - '{fullName}' (len: {anim.Length:F2}s, loop: {anim.LoopMode})");
            }
        }

        // Also check GetAnimationList() directly
        var directList = animPlayer.GetAnimationList();
        GD.Print($"\nDirect GetAnimationList(): {directList.Length} animations");
        foreach (var name in directList)
        {
            GD.Print($"  - '{name}'");
        }

        // Check AnimationTree
        var animTree = GetTree().CurrentScene.FindChild("AnimationTree", true, false) as AnimationTree;
        if (animTree != null)
        {
            GD.Print($"\nAnimationTree:");
            GD.Print($"  Active: {animTree.Active}");
            GD.Print($"  AnimPlayer Path: {animTree.AnimPlayer}");
            GD.Print($"  Has TreeRoot: {animTree.TreeRoot != null}");

            if (animTree.TreeRoot != null)
            {
                GD.Print($"  TreeRoot Type: {animTree.TreeRoot.GetType().Name}");
            }
        }
        else
        {
            GD.PrintErr("No AnimationTree found!");
        }

        GD.Print("=== END DEBUG ===");
    }
}
