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
        // General Movement (Shared)
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

         // Melee Animations
        { "melee idle", "res://Assets/Erika/sword and shield idle (4).fbx" },
        { "melee walk forward", "res://Assets/Erika/sword and shield walk.fbx" },
        { "melee walk back", "res://Assets/Erika/sword and shield walk (2).fbx" },
        { "melee walk right", "res://Assets/Erika/sword and shield strafe.fbx" },
        { "melee walk left", "res://Assets/Erika/sword and shield strafe (2).fbx" },
        { "melee run forward", "res://Assets/Erika/sword and shield run.fbx" },
        { "melee run back", "res://Assets/Erika/sword and shield run (2).fbx" },
        { "melee run right", "res://Assets/Erika/sword and shield strafe (3).fbx" },
        { "melee run left", "res://Assets/Erika/sword and shield strafe (4).fbx" },
        { "melee attack", "res://Assets/Erika/sword and shield slash.fbx" },
        { "melee perfect attack", "res://Assets/Erika/sword and shield slash (2).fbx" },
        { "melee power attack", "res://Assets/Erika/sword and shield slash (4).fbx" },

        // ErikaBow Animations
        { "archery draw", "res://Assets/ErikaBow/standing draw arrow.fbx" },
        { "archery aim idle", "res://Assets/ErikaBow/standing aim overdraw.fbx" },
        { "archery recoil", "res://Assets/ErikaBow/standing aim recoil.fbx" },
        { "archery walk forward", "res://Assets/ErikaBow/standing aim walk forward.fbx" },
        { "archery walk back", "res://Assets/ErikaBow/standing aim walk back.fbx" },
        { "archery walk left", "res://Assets/ErikaBow/standing aim walk left.fbx" },
        { "archery walk right", "res://Assets/ErikaBow/standing aim walk right.fbx" },
        { "archery idle normal", "res://Assets/ErikaBow/standing idle 01.fbx" },
        { "archery walk forward normal", "res://Assets/ErikaBow/standing walk forward.fbx" },
        { "archery walk back normal", "res://Assets/ErikaBow/standing walk back.fbx" },
        { "archery walk left normal", "res://Assets/ErikaBow/standing walk left.fbx" },
        { "archery walk right normal", "res://Assets/ErikaBow/standing walk right.fbx" },
        { "archery turn left", "res://Assets/ErikaBow/standing turn 90 left.fbx" },
        { "archery turn right", "res://Assets/ErikaBow/standing turn 90 right.fbx" },
        { "archery unequip", "res://Assets/ErikaBow/standing disarm bow.fbx" },
        { "archery equip", "res://Assets/ErikaBow/standing equip bow.fbx" }
    };

    public override void _Ready()
    {
        GD.Print("[SetupErikaAnimations] Starting animation setup...");

        // Find Players
        var meleePlayer = GetNodeOrNull<AnimationPlayer>("../Erika/AnimationPlayer");
        var archeryPlayer = GetNodeOrNull<AnimationPlayer>("../ErikaBow/AnimationPlayer");

        if (meleePlayer != null) LoadForPlayer(meleePlayer, true, false);
        else GD.PrintErr("[SetupErikaAnimations] Could not find Erika/AnimationPlayer");

        if (archeryPlayer != null) LoadForPlayer(archeryPlayer, false, true);
        else GD.PrintErr("[SetupErikaAnimations] Could not find ErikaBow/AnimationPlayer");
    }

    private void LoadForPlayer(AnimationPlayer animPlayer, bool allowMelee, bool allowArchery)
    {
        GD.Print($"[SetupErikaAnimations] Loading for {animPlayer.GetParent().Name}...");

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

        foreach (var kvp in AnimationFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            bool isMelee = animName.Contains("melee");
            bool isArchery = animName.Contains("archery");

            // Filter
            if (isMelee && !allowMelee) continue;
            if (isArchery && !allowArchery) continue;

            // Check if animation already exists - don't overwrite user's manual settings
            if (library.HasAnimation(animName))
            {
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
                //GD.PrintErr($"  No AnimationPlayer in FBX: {fbxPath}");
                instance.QueueFree();
                continue;
            }

            // Get the animation from the FBX's AnimationPlayer
            var animList = fbxAnimPlayer.GetAnimationList();
            if (animList.Length == 0)
            {
                instance.QueueFree();
                continue;
            }

            // Extract and duplicate
            var srcAnim = fbxAnimPlayer.GetAnimation(animList[0]);
            var newAnim = srcAnim.Duplicate() as Animation;

            // Loop settings
            SetLoopMode(animName, newAnim);

            RemoveRootMotion(newAnim);

            library.AddAnimation(animName, newAnim);
            added++;
            instance.QueueFree();
        }

        GD.Print($"[SetupErikaAnimations] Added {added} animations to {animPlayer.GetParent().Name}");
    }

    private void SetLoopMode(string animName, Animation anim)
    {
        if (animName.Contains("attack", System.StringComparison.OrdinalIgnoreCase) ||
            (animName.Contains("jump", System.StringComparison.OrdinalIgnoreCase) && !animName.Contains("running jump")) ||
            animName.Contains("recoil", System.StringComparison.OrdinalIgnoreCase) ||
            animName.Contains("draw", System.StringComparison.OrdinalIgnoreCase) ||
            animName.Contains("equip", System.StringComparison.OrdinalIgnoreCase) ||
            animName.Contains("turn", System.StringComparison.OrdinalIgnoreCase))
        {
            anim.LoopMode = Animation.LoopModeEnum.None;
        }
        else if (animName == "archery idle normal" || animName == "archery aim idle")
        {
            GD.Print($"[SetupErikaAnimations] SEETING PINGPONG for {animName}!");
            anim.LoopMode = Animation.LoopModeEnum.Pingpong;
        }
        else
        {
            anim.LoopMode = Animation.LoopModeEnum.Linear;
        }
    }

    private void RemoveRootMotion(Animation anim)
    {
        int trackCount = anim.GetTrackCount();
        for (int i = trackCount - 1; i >= 0; i--)
        {
            string trackPath = anim.TrackGetPath(i).ToString();
            // Assuming Mixamo naming: mixamorig_Hips is often the root for motion
            if (trackPath.Contains("Hips") && anim.TrackGetType(i) == Animation.TrackType.Position3D)
            {
                anim.RemoveTrack(i);
            }
        }
    }
}
