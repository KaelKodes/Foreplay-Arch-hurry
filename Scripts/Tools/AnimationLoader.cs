using Godot;
using System.Collections.Generic;

/// <summary>
/// Loads animations from separate FBX files into the target AnimationPlayer at runtime.
/// This fixes the issue where FBX instance animations aren't in the runtime library.
/// </summary>
public partial class AnimationLoader : Node
{
    [Export] public NodePath AnimationPlayerPath = new NodePath("../Erika/AnimationPlayer");
    [Export] public bool LoadMelee = true;
    [Export] public bool LoadArchery = true;

    private static readonly Dictionary<string, string> AnimationSources = new()
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
        { "standing jump", "res://Assets/Erika/sword and shield jump (2).fbx" },
        { "running jump", "res://Assets/Erika/sword and shield jump.fbx" },
        
        // Melee (Sword & Shield) animations
        { "melee idle", "res://Assets/Erika/sword and shield idle (4).fbx" },
        { "melee walk forward", "res://Assets/Erika/sword and shield walk.fbx" },
        { "melee walk back", "res://Assets/Erika/sword and shield walk (2).fbx" },
        { "melee walk right", "res://Assets/Erika/sword and shield strafe.fbx" },
        { "melee walk left", "res://Assets/Erika/sword and shield strafe (2).fbx" },
        { "melee run forward", "res://Assets/Erika/sword and shield run.fbx" },
        { "melee run back", "res://Assets/Erika/sword and shield run (2).fbx" },
        { "melee run right", "res://Assets/Erika/sword and shield strafe (3).fbx" },
        { "melee run left", "res://Assets/Erika/sword and shield strafe (4).fbx" },

        // Melee Attack animations
        { "melee attack", "res://Assets/Erika/sword and shield slash.fbx" },
        { "melee perfect attack", "res://Assets/Erika/sword and shield slash (2).fbx" },
        { "melee power attack", "res://Assets/Erika/sword and shield slash (4).fbx" },

        // Archery Animations
        { "archery draw", "res://Assets/ErikaBow/standing draw arrow.fbx" },
        { "archery aim idle", "res://Assets/ErikaBow/standing aim overdraw.fbx" },
        { "archery recoil", "res://Assets/ErikaBow/standing aim recoil.fbx" },
        { "archery walk forward", "res://Assets/ErikaBow/standing aim walk forward.fbx" },
        { "archery walk back", "res://Assets/ErikaBow/standing aim walk back.fbx" },
        { "archery walk left", "res://Assets/ErikaBow/standing aim walk left.fbx" },
        { "archery walk right", "res://Assets/ErikaBow/standing aim walk right.fbx" }
    };

    public override void _Ready()
    {
        var animPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
        if (animPlayer == null)
        {
            GD.PrintErr("[AnimationLoader] AnimationPlayer not found!");
            return;
        }

        LoadAnimations(animPlayer);
    }

    private void LoadAnimations(AnimationPlayer animPlayer)
    {
        // Get or create the default library
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

        int loaded = 0;
        foreach (var kvp in AnimationSources)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            // Filter based on flags
            if (!LoadMelee && animName.Contains("melee", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!LoadArchery && animName.Contains("archery", System.StringComparison.OrdinalIgnoreCase)) continue;

            // Skip if already exists
            if (library.HasAnimation(animName))
            {
                continue;
            }

            // Load the FBX as a PackedScene
            if (!ResourceLoader.Exists(fbxPath))
            {
                GD.PrintErr($"[AnimationLoader] FBX not found: {fbxPath}");
                continue;
            }

            var fbxScene = GD.Load<PackedScene>(fbxPath);
            if (fbxScene == null)
            {
                GD.PrintErr($"[AnimationLoader] Could not load: {fbxPath}");
                continue;
            }

            // Instance temporarily to extract animations
            var instance = fbxScene.Instantiate();
            var fbxAnimPlayer = instance.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

            if (fbxAnimPlayer == null)
            {
                instance.QueueFree();
                continue;
            }

            // Get the first animation from the FBX
            var animList = fbxAnimPlayer.GetAnimationList();
            if (animList.Length == 0)
            {
                instance.QueueFree();
                continue;
            }

            // Extract and duplicate the animation
            var srcAnim = fbxAnimPlayer.GetAnimation(animList[0]);
            var newAnim = srcAnim.Duplicate() as Animation;

            // Set to loop
            // Set loop mode
            if (animName.Contains("attack", System.StringComparison.OrdinalIgnoreCase) ||
                animName.Contains("jump", System.StringComparison.OrdinalIgnoreCase) ||
                animName.Contains("recoil", System.StringComparison.OrdinalIgnoreCase) ||
                animName.Contains("draw", System.StringComparison.OrdinalIgnoreCase))
            {
                newAnim.LoopMode = Animation.LoopModeEnum.None;
            }
            else
            {
                newAnim.LoopMode = Animation.LoopModeEnum.Linear;
            }

            // Remove root motion (strip position tracks for Hips/root bone)
            RemoveRootMotion(newAnim);

            // Add to our library
            var err = library.AddAnimation(animName, newAnim);
            if (err == Error.Ok)
            {
                loaded++;
                GD.Print($"[AnimationLoader] Loaded: {animName}");
            }

            instance.QueueFree();
        }

        GD.Print($"[AnimationLoader] Complete! Loaded {loaded} animations.");
    }

    /// <summary>
    /// Removes root motion by stripping position tracks from the root/hips bone.
    /// This makes animations play "in place" so the CharacterBody3D handles movement.
    /// </summary>
    private void RemoveRootMotion(Animation anim)
    {
        // Find and remove position tracks for root bones
        // Mixamo uses "mixamorig_Hips" as the root
        var tracksToRemove = new System.Collections.Generic.List<int>();

        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            var path = anim.TrackGetPath(i).ToString();
            var trackType = anim.TrackGetType(i);

            // Remove position tracks for root bone (Hips)
            if (trackType == Animation.TrackType.Position3D &&
                (path.Contains("Hips") || path.Contains("Root") || path.EndsWith("Skeleton3D")))
            {
                tracksToRemove.Add(i);
            }
        }

        // Remove tracks in reverse order to maintain valid indices
        tracksToRemove.Reverse();
        foreach (var trackIdx in tracksToRemove)
        {
            anim.RemoveTrack(trackIdx);
        }
    }
}
