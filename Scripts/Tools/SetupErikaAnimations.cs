using Godot;
using System.Collections.Generic;

#if TOOLS
[Tool]
#endif
public partial class SetupErikaAnimations : Node
{
    // Run this script once in the editor or at game start to set up animations

    private static readonly Dictionary<string, string> AnimationFiles = new()
    {
        { "standing idle 01", "res://Assets/Erika/standing idle 01.fbx" },
        { "standing walk forward", "res://Assets/Erika/standing walk forward.fbx" },
        { "standing walk back", "res://Assets/Erika/standing walk back.fbx" },
        { "standing walk left", "res://Assets/Erika/standing walk left.fbx" },
        { "standing walk right", "res://Assets/Erika/standing walk right.fbx" },
        { "standing run forward", "res://Assets/Erika/standing run forward.fbx" },
        { "standing run back", "res://Assets/Erika/standing run back.fbx" },
        { "standing run left", "res://Assets/Erika/standing run left.fbx" },
        { "standing run right", "res://Assets/Erika/standing run right.fbx" },
    };

    public override void _Ready()
    {
        GD.Print("[SetupErikaAnimations] Starting animation setup...");

        // Find the Erika AnimationPlayer
        var animPlayer = GetNodeOrNull<AnimationPlayer>("../Erika/AnimationPlayer");
        if (animPlayer == null)
        {
            animPlayer = GetTree().CurrentScene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        }

        if (animPlayer == null)
        {
            GD.PrintErr("[SetupErikaAnimations] Could not find AnimationPlayer!");
            return;
        }

        GD.Print($"[SetupErikaAnimations] Found AnimationPlayer: {animPlayer.GetPath()}");

        // Get or create the default AnimationLibrary
        AnimationLibrary library;
        if (animPlayer.HasAnimationLibrary(""))
        {
            library = animPlayer.GetAnimationLibrary("");
        }
        else
        {
            library = new AnimationLibrary();
            animPlayer.AddAnimationLibrary("", library);
        }

        int added = 0;
        int looped = 0;

        foreach (var kvp in AnimationFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            // Check if animation already exists
            if (library.HasAnimation(animName))
            {
                GD.Print($"  Animation '{animName}' already exists, setting loop...");
                var existingAnim = library.GetAnimation(animName);
                existingAnim.LoopMode = Animation.LoopModeEnum.Linear;
                looped++;
                continue;
            }

            // Load the FBX scene
            if (!ResourceLoader.Exists(fbxPath))
            {
                GD.PrintErr($"  FBX not found: {fbxPath}");
                continue;
            }

            var fbxScene = GD.Load<PackedScene>(fbxPath);
            if (fbxScene == null)
            {
                GD.PrintErr($"  Could not load FBX: {fbxPath}");
                continue;
            }

            // Instance it to extract the animation
            var instance = fbxScene.Instantiate();
            var fbxAnimPlayer = instance.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

            if (fbxAnimPlayer == null)
            {
                GD.PrintErr($"  No AnimationPlayer in FBX: {fbxPath}");
                instance.QueueFree();
                continue;
            }

            // Get the animation from the FBX's AnimationPlayer
            var animList = fbxAnimPlayer.GetAnimationList();
            if (animList.Length == 0)
            {
                GD.PrintErr($"  No animations in FBX: {fbxPath}");
                instance.QueueFree();
                continue;
            }

            // Usually the animation is named "mixamo_com" or similar in Mixamo exports
            string srcAnimName = animList[0];
            var srcAnim = fbxAnimPlayer.GetAnimation(srcAnimName);

            if (srcAnim == null)
            {
                instance.QueueFree();
                continue;
            }

            // Duplicate and rename
            var newAnim = srcAnim.Duplicate() as Animation;
            newAnim.LoopMode = Animation.LoopModeEnum.Linear;

            // Add to our library with the friendly name
            var err = library.AddAnimation(animName, newAnim);
            if (err == Error.Ok)
            {
                GD.Print($"  ✓ Added '{animName}' (looping)");
                added++;
            }
            else
            {
                GD.PrintErr($"  Failed to add '{animName}': {err}");
            }

            instance.QueueFree();
        }

        GD.Print($"[SetupErikaAnimations] Complete! Added: {added}, Loop-fixed: {looped}");

        // Activate the AnimationTree if present
        var animTree = GetNodeOrNull<AnimationTree>("../AnimationTree");
        if (animTree == null)
        {
            animTree = GetTree().CurrentScene.FindChild("AnimationTree", true, false) as AnimationTree;
        }

        if (animTree != null)
        {
            animTree.Active = true;
            GD.Print("[SetupErikaAnimations] AnimationTree activated!");
        }
    }
}
