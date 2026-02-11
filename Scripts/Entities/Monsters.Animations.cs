using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class Monsters
{
    private void LoadZombieAnimations()
    {
        var zombieAnimFiles = new Dictionary<string, string>
        {
            { "Idle", "res://Assets/Monsters/Zombie/zombie idle.fbx" },
            { "Walk", "res://Assets/Monsters/Zombie/zombie walk.fbx" },
            { "Run", "res://Assets/Monsters/Zombie/zombie run.fbx" },
            { "Attack", "res://Assets/Monsters/Zombie/zombie attack.fbx" },
            { "Death", "res://Assets/Monsters/Zombie/zombie death.fbx" },
            { "Hit", "res://Assets/Monsters/Zombie/zombie scream.fbx" },
            { "Crawl", "res://Assets/Monsters/Zombie/zombie crawl.fbx" }
        };

        LoadExternalAnimations(zombieAnimFiles);
    }

    private void LoadCrawlerAnimations()
    {
        if (_animPlayer == null) return;
        var animList = _animPlayer.GetAnimationList();
        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null) return;

        foreach (var animName in animList)
        {
            string lowerName = animName.ToLower();
            Animation anim = _animPlayer.GetAnimation(animName);
            if (anim == null) continue;

            if (lowerName.Contains("idle") && !lib.HasAnimation("Idle"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Idle", copy);
            }
            else if (lowerName.Contains("walk") && !lib.HasAnimation("Walk"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Walk", copy);
            }
            else if (lowerName.Contains("attack") && !lib.HasAnimation("Attack"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                lib.AddAnimation("Attack", copy);
            }
            else if (lowerName.Contains("death") && !lib.HasAnimation("Death"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                lib.AddAnimation("Death", copy);
            }
        }

        if (!lib.HasAnimation("Idle") && animList.Length > 0)
        {
            var firstAnim = _animPlayer.GetAnimation(animList[0]);
            if (firstAnim != null)
            {
                var copy = (Animation)firstAnim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Idle", copy);
            }
        }
    }

    private void LoadExternalAnimations(Dictionary<string, string> animFiles)
    {
        if (_animPlayer == null) return;

        if (!_animPlayer.HasAnimationLibrary(""))
        {
            _animPlayer.AddAnimationLibrary("", new AnimationLibrary());
        }
        var lib = _animPlayer.GetAnimationLibrary("");

        foreach (var kvp in animFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            if (!ResourceLoader.Exists(fbxPath)) continue;

            try
            {
                var animScene = GD.Load<PackedScene>(fbxPath);
                if (animScene == null) continue;

                var tempInstance = animScene.Instantiate();
                var tempAnimPlayer = FindAnimationPlayerRecursive(tempInstance);

                if (tempAnimPlayer != null)
                {
                    var animList = tempAnimPlayer.GetAnimationList();
                    if (animList.Length > 0)
                    {
                        var srcAnim = tempAnimPlayer.GetAnimation(animList[0]);
                        if (srcAnim != null)
                        {
                            var animCopy = (Animation)srcAnim.Duplicate();
                            RemoveRootMotion(animCopy);
                            bool shouldLoop = animName == "Idle" || animName == "Walk" || animName == "Run" || animName == "Crawl";
                            animCopy.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

                            if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                            lib.AddAnimation(animName, animCopy);
                        }
                    }
                }
                tempInstance.QueueFree();
            }
            catch (Exception e) { GD.PrintErr($"[Monsters] Failed to load animation {animName}: {e.Message}"); }
        }
    }

    private void PlayAnimationRobust(string animName)
    {
        if (_animPlayer == null) return;
        var anims = _animPlayer.GetAnimationList();

        if (_animPlayer.HasAnimation(animName))
        {
            _animPlayer.Play(animName);
        }
        else if (anims.Length > 0)
        {
            string fallback = anims[0];
            var animRef = _animPlayer.GetAnimation(fallback);
            if (animRef != null) animRef.LoopMode = Animation.LoopModeEnum.Linear;
            _animPlayer.Play(fallback);
        }
    }

    public virtual void SetAnimation(string animName)
    {
        PlayAnimationRobust(animName);
    }

    private void LoadSkeletonAnimations()
    {
        // 1. Identify the Players
        var rootPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (rootPlayer == null) return;

        // NEW: Check Registry for AnimationSources
        var registry = CharacterRegistry.Instance;
        var model = registry?.AvailableModels?.Find(m => string.Equals(m.Id, Species, System.StringComparison.OrdinalIgnoreCase));

        if (model != null && model.AnimationSources != null && model.AnimationSources.Count > 0)
        {
            GD.Print($"[Monsters] {Species}: Loading shared animations from {model.AnimationSources.Count} sources.");
            _animPlayer = rootPlayer;

            // Fix: Retarget RootNode so FBX-track paths resolve correctly
            // Most split-file FBXs contain a 'root' or 'Armature' node that tracks are relative to.
            var sceneInstance = GetNodeOrNull("Visuals/scene");
            if (sceneInstance != null)
            {
                _animPlayer.RootNode = _animPlayer.GetPathTo(sceneInstance);
                GD.Print($"[Monsters] {Species}: Retargeted AnimationPlayer RootNode to {_animPlayer.RootNode}");
            }

            LoadExternalAnimations(model.AnimationSources);

            // Fix: Ensured weapon parenting is called before early return
            if (Species.ToLower().Contains("skeleton")) SetupWeaponParenting();
            return;
        }

        // OLD: Fallback to Theft Logic (kept for Models with merged tracks)
        // The 'source' player inside the GLB instance
        var innerPlayer = FindPopulatedAnimationPlayerRecursive(GetNodeOrNull("Visuals"));
        if (innerPlayer == null || innerPlayer == rootPlayer)
        {
            GD.Print($"[Monsters] {Species}: No inner animations for theft (and no sources in registry).");
            return;
        }

        GD.Print($"[Monsters] {Species}: Performing animation theft from {innerPlayer.GetPath()} to {rootPlayer.GetPath()}");

        // 2. Setup the Root Player
        if (!rootPlayer.HasAnimationLibrary(""))
        {
            rootPlayer.AddAnimationLibrary("", new AnimationLibrary());
        }
        var rootLib = rootPlayer.GetAnimationLibrary("");

        // IMPORTANT: Retarget the root player to the same node as the inner player
        // This ensures the animation NodePaths (relative to the root node) still work!
        rootPlayer.RootNode = rootPlayer.GetPathTo(innerPlayer.GetParent());

        // 3. Verbose Inventory: Log exactly what we are thefting from
        var sourceLibNames = innerPlayer.GetAnimationLibraryList();
        GD.Print($"[Monsters] {Species} Theft Audit: Inner Player has {sourceLibNames.Count} libraries.");

        // 4. Extract and Map the Good Stuff
        var mapping = new Dictionary<string, string>
        {
            { "Idle", "idle" },
            { "Walk", "walk" },
            { "Run", "run" },
            { "Attack", "attack" },
            { "Death", "death" },
            { "Hit", "hit" }
        };

        var mappedAnims = new HashSet<string>();

        foreach (var libName in sourceLibNames)
        {
            var sourceLib = innerPlayer.GetAnimationLibrary(libName);
            if (sourceLib == null) continue;

            GD.Print($"  -> Library '{libName}' has {sourceLib.GetAnimationList().Count} animations:");

            foreach (var animName in sourceLib.GetAnimationList())
            {
                string fullName = libName == "" ? animName : $"{libName}/{animName}";
                string lowerName = fullName.ToLower();

                var anim = sourceLib.GetAnimation(animName);
                GD.Print($"     * Found: '{fullName}' ({anim.Length:F2}s)");

                // Block the junk cycle/stack tracks
                if (lowerName.Contains("stack") || lowerName.Contains("everything") || lowerName.Contains("cycle"))
                {
                    GD.Print($"       [SKIP] Junk/Cycle track detected.");
                    continue;
                }

                foreach (var kvp in mapping)
                {
                    if (mappedAnims.Contains(kvp.Key)) continue;

                    if (lowerName.Contains(kvp.Value))
                    {
                        var copy = (Animation)anim.Duplicate();
                        RemoveRootMotion(copy);

                        if (kvp.Key == "Idle" || kvp.Key == "Walk" || kvp.Key == "Run")
                            copy.LoopMode = Animation.LoopModeEnum.Linear;
                        else
                            copy.LoopMode = Animation.LoopModeEnum.None;

                        if (rootLib.HasAnimation(kvp.Key)) rootLib.RemoveAnimation(kvp.Key);
                        rootLib.AddAnimation(kvp.Key, copy);
                        mappedAnims.Add(kvp.Key);
                        GD.Print($"       [MAPPING] '{fullName}' mapped to '{kvp.Key}'");
                        break;
                    }
                }
            }
        }

        // 5. Fallback: If we still have no Idle, take the first reasonable animation
        if (!mappedAnims.Contains("Idle"))
        {
            foreach (var libName in sourceLibNames)
            {
                var sourceLib = innerPlayer.GetAnimationLibrary(libName);
                if (sourceLib == null) continue;
                foreach (var animName in sourceLib.GetAnimationList())
                {
                    string lower = animName.ToString().ToLower();
                    if (lower.Contains("stack") || lower.Contains("all animations") || lower.Contains("everything") || lower.Contains("cycle"))
                        continue;

                    var anim = sourceLib.GetAnimation(animName);
                    var copy = (Animation)anim.Duplicate();
                    RemoveRootMotion(copy);
                    copy.LoopMode = Animation.LoopModeEnum.Linear;
                    rootLib.AddAnimation("Idle", copy);
                    mappedAnims.Add("Idle");
                    GD.Print($"       [FALLBACK] '{animName}' set as 'Idle'");
                    break;
                }
                if (mappedAnims.Contains("Idle")) break;
            }
        }

        // 6. Silence the inner player FOREVER
        innerPlayer.Stop();
        innerPlayer.Autoplay = "";

        // 7. Update the reference and Silencing extraneous players
        _animPlayer = rootPlayer;
        _animPlayer.Stop();
        _animPlayer.Autoplay = "";

        // Double check for any other players that might interfere
        void SilenceRecursive(Node node)
        {
            if (node is AnimationPlayer ap && ap != _animPlayer)
            {
                ap.Stop();
                ap.Autoplay = "";
            }
            foreach (Node child in node.GetChildren()) SilenceRecursive(child);
        }
        SilenceRecursive(this);

        // 6. Weapon Parenting
        if (Species.ToLower().Contains("skeleton"))
        {
            SetupWeaponParenting();
        }
    }

    private void SetupWeaponParenting()
    {
        // CRITICAL: If this is a Skeleton, we are using manual placement in Skeleton.tscn.
        // The code should NOT attempt to auto-parent or move the weapon.
        if (Species.ToLower().Contains("skeleton"))
        {
            GD.Print($"[Monsters] {Species}: Using manual weapon placement. Skipping auto-parenting.");
            return;
        }

        GD.Print($"[Monsters] {Species}: weapon parenting routine check...");

        Skeleton3D skel = MonsterVisuals.FindSkeletonRecursive(this);
        Node3D modelRoot = GetNodeOrNull<Node3D>("Visuals");

        if (skel == null) return;

        // 1. Find all potential candidate meshes deep in the visuals
        var allMeshes = this.FindChildren("*", "MeshInstance3D", true, false);

        foreach (var node in allMeshes)
        {
            if (!(node is MeshInstance3D mesh)) continue;

            // 2. CRITICAL: Is this mesh already manually attached to a bone in the scene?
            bool isManuallyAttached = false;
            Node p = mesh.GetParent();
            while (p != null && p != this)
            {
                if (p is BoneAttachment3D)
                {
                    isManuallyAttached = true;
                    break;
                }
                p = p.GetParent();
            }

            if (isManuallyAttached)
            {
                GD.Print($"[Monsters] {Species}: Mesh '{mesh.Name}' is already in a manual BoneAttachment. LEAVING IT ALONE.");
                continue;
            }

            // 3. Auto-parenting logic for floating meshes
            string lowName = mesh.Name.ToString().ToLower();
            string boneName = "";
            if (lowName.Contains("sword") || lowName.Contains("weapon") || lowName.Contains("falchion") || lowName.Contains("blade")) boneName = "RightHand";
            else if (lowName.Contains("shield")) boneName = "LeftHand";

            if (!string.IsNullOrEmpty(boneName))
            {
                GD.Print($"[Monsters] {Species}: Floating mesh '{mesh.Name}' looks like a {boneName} item. Auto-parenting...");

                int bIdx = skel.FindBone(boneName);
                if (bIdx == -1)
                {
                    string[] variations = boneName == "RightHand"
                        ? new[] { "RightHand", "mixamorig_RightHand", "Hand_R", "hand.r", "hand_r", "r_hand", "R_Hand", "Hand_R_01", "hand_r_01" }
                        : new[] { "LeftHand", "mixamorig_LeftHand", "Hand_L", "hand.l", "hand_l", "l_hand", "L_Hand", "Hand_L_01", "hand_l_01" };

                    foreach (var variant in variations)
                    {
                        bIdx = skel.FindBone(variant);
                        if (bIdx != -1) break;
                    }
                }

                if (bIdx != -1)
                {
                    var attachment = new BoneAttachment3D();
                    attachment.Name = "AutoAttach_" + mesh.Name;
                    attachment.BoneName = skel.GetBoneName(bIdx);
                    skel.AddChild(attachment);

                    // Re-parent the mesh (or its scene root if it's an instance) to the attachment
                    Node moveTarget = mesh;
                    if (mesh.Owner != null && mesh.Owner != this && mesh.Owner.GetParent() == mesh.GetParent())
                        moveTarget = mesh.Owner;

                    moveTarget.GetParent()?.RemoveChild(moveTarget);
                    attachment.AddChild(moveTarget);

                    if (moveTarget is Node3D m3d)
                    {
                        m3d.Position = Vector3.Zero;
                        m3d.Rotation = Vector3.Zero;
                    }
                }
            }
        }
    }

    private void RemoveRootMotion(Animation anim)
    {
        int trackCount = anim.GetTrackCount();
        for (int i = trackCount - 1; i >= 0; i--)
        {
            string trackPath = anim.TrackGetPath(i).ToString();
            if ((trackPath.Contains("Hips") || trackPath.Contains("Root")) &&
                anim.TrackGetType(i) == Animation.TrackType.Position3D)
            {
                anim.RemoveTrack(i);
            }
        }
    }
}
