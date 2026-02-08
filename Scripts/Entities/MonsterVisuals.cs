using Godot;
using System;

namespace Archery;

/// <summary>
/// Handles monster visual updates, species filtering, AABB calculations, and animation retargeting.
/// Extracted from Monsters.cs to reduce file size.
/// </summary>
public static class MonsterVisuals
{
    /// <summary>
    /// Updates the visuals to show only the specified species.
    /// </summary>
    public static void UpdateSpeciesVisuals(Monsters monster, string species, AnimationPlayer animPlayer, Action onCollisionUpdate)
    {
        if (string.IsNullOrEmpty(species)) return;

        var scene = monster.GetNodeOrNull("Visuals/scene");
        if (scene == null)
        {
            GD.PrintErr($"[MonsterVisuals] Visuals/scene not found for {monster.Name}");
            return;
        }

        // GD.Print($"[MonsterVisuals] --- Filtering visuals for Species: '{species}' ---");

        // Reset scene transform before processing
        if (scene is Node3D scene3D) scene3D.Transform = Transform3D.Identity;

        // Step 1: Hide EVERYTHING deep
        HideAllRecursive(scene);

        // Step 2: Show matching nodes and their ancestors
        bool found = false;
        ShowSpeciesRecursive(scene, species, ref found, animPlayer);

        if (found)
        {
            // GD.Print($"[MonsterVisuals] Successfully isolated Species: {species}");
            CenterAndGroundVisuals(scene, species);
            onCollisionUpdate?.Invoke();
        }
        else
        {
            GD.PrintErr($"[MonsterVisuals] FAILED to find any nodes matching Species: '{species}'. Showing everything as fallback.");
            ShowAllRecursive(scene);
            if (scene is Node3D n3d) n3d.Transform = Transform3D.Identity;
        }
    }

    /// <summary>
    /// Centers and grounds the visual scene based on AABB calculation.
    /// </summary>
    public static void CenterAndGroundVisuals(Node scene, string species)
    {
        if (!(scene is Node3D scene3D)) return;

        Aabb totalAabb = new Aabb();
        bool first = true;
        CalculateVisibleAabb(scene3D, ref totalAabb, ref first);

        if (first)
        {
            GD.PrintErr($"[MonsterVisuals] Could not calculate AABB for {species}");
            return;
        }

        Vector3 centerOffset = -totalAabb.GetCenter();
        centerOffset.Y = -totalAabb.Position.Y;

        scene3D.Position += centerOffset;
        scene3D.ForceUpdateTransform();
        // GD.Print($"[MonsterVisuals] Grounding offset applied: {centerOffset}");
    }

    /// <summary>
    /// Recursively calculates the AABB of all visible meshes.
    /// </summary>
    public static void CalculateVisibleAabb(Node3D node, ref Aabb totalAabb, ref bool first, Transform3D cumulativeTransform = default)
    {
        if (cumulativeTransform == default) cumulativeTransform = Transform3D.Identity;

        if (node is VisualInstance3D vi && node.Visible)
        {
            Aabb localAabb = vi.GetAabb();
            Aabb relativeAabb = (cumulativeTransform * node.Transform) * localAabb;

            if (first)
            {
                totalAabb = relativeAabb;
                first = false;
            }
            else
            {
                totalAabb = totalAabb.Merge(relativeAabb);
            }
        }

        foreach (Node child in node.GetChildren())
        {
            if (child is Node3D n3d)
            {
                CalculateVisibleAabb(n3d, ref totalAabb, ref first, cumulativeTransform * node.Transform);
            }
        }
    }

    /// <summary>
    /// Updates collision shape based on current AABB.
    /// </summary>
    public static void UpdateCollisionShape(Monsters monster)
    {
        var colShape = monster.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")
                    ?? monster.GetNodeOrNull<CollisionShape3D>("StaticBody3D/CollisionShape3D")
                    ?? FindCollisionShapeRecursive(monster);

        if (colShape == null) return;

        Aabb finalAabb = new Aabb();
        bool first = true;

        foreach (Node child in monster.GetChildren())
        {
            if (child is Node3D n3d && n3d.Name != "CollisionShape3D")
                CalculateVisibleAabb(n3d, ref finalAabb, ref first, Transform3D.Identity);
        }

        if (first) return;

        if (colShape.Shape is BoxShape3D box)
        {
            box.Size = finalAabb.Size;
            colShape.Position = finalAabb.GetCenter();
        }
        else if (colShape.Shape is CapsuleShape3D capsule)
        {
            capsule.Radius = Math.Max(finalAabb.Size.X, finalAabb.Size.Z) / 2.0f;
            capsule.Height = finalAabb.Size.Y;
            colShape.Position = finalAabb.GetCenter();
        }
        else
        {
            var newBox = new BoxShape3D();
            newBox.Size = finalAabb.Size;
            colShape.Shape = newBox;
            colShape.Position = finalAabb.GetCenter();
        }

        // GD.Print($"[MonsterVisuals] Collision adjusted to Size: {finalAabb.Size}");
    }

    public static void HideAllRecursive(Node node)
    {
        if (node is Node3D n3d) n3d.Visible = false;
        foreach (Node child in node.GetChildren()) HideAllRecursive(child);
    }

    public static void ShowAllRecursive(Node node)
    {
        if (node is Node3D n3d) n3d.Visible = true;
        foreach (Node child in node.GetChildren()) ShowAllRecursive(child);
    }

    private static void ShowSpeciesRecursive(Node node, string species, ref bool found, AnimationPlayer animPlayer)
    {
        // Special case: Vampire and Crawler often have generic mesh names (Group####)
        // that shouldn't be filtered out if we already found the main match or if we want everything.
        bool bypassFilter = string.Equals(species, "Vampire", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(species, "Crawler", StringComparison.OrdinalIgnoreCase);

        foreach (Node child in node.GetChildren())
        {
            string name = child.Name.ToString();
            bool isMatch = bypassFilter || IsStrictSpeciesMatch(name, species);

            if (isMatch)
            {
                // if (!found) GD.Print($"  [MonsterVisuals] MATCH FOUND: {name} for {species} (Bypass: {bypassFilter})");
                found = true;
                ShowAncestors(child);
                ShowAllRecursive(child);
                SetupSharedAnimations(child, species, animPlayer);

                // If we bypassed, we still want to keep looking for other nodes to show
                if (bypassFilter)
                    ShowSpeciesRecursive(child, species, ref found, animPlayer);
            }
            else
            {
                ShowSpeciesRecursive(child, species, ref found, animPlayer);
            }
        }
    }

    private static void ShowAncestors(Node node)
    {
        Node current = node.GetParent();
        while (current != null)
        {
            if (current is Node3D n3d) n3d.Visible = true;
            current = current.GetParent();
        }
    }

    public static bool IsStrictSpeciesMatch(string nodeName, string species)
    {
        if (string.Equals(nodeName, species, StringComparison.OrdinalIgnoreCase)) return true;

        // Special case: Zombie species matches "Parasite" nodes
        if (string.Equals(species, "Zombie", StringComparison.OrdinalIgnoreCase) &&
            nodeName.StartsWith("Parasite", StringComparison.OrdinalIgnoreCase)) return true;

        if (nodeName.StartsWith(species, StringComparison.OrdinalIgnoreCase))
        {
            string remainder = nodeName.Substring(species.Length);
            if (string.IsNullOrEmpty(remainder)) return true;

            char sep = remainder[0];
            if (sep == '.' || sep == '_' || sep == ' ')
            {
                string suffix = remainder.Substring(1);
                if (string.IsNullOrEmpty(suffix)) return true;
                if (int.TryParse(suffix, out _)) return true;
                if (string.Equals(suffix, "Main", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    public static MonsterBodyType GetBodyType(string species)
    {
        string s = species.ToLower();

        if (s.Contains("yeti")) return MonsterBodyType.Biped;
        if (s.Contains("zombie") || s.Contains("parasite")) return MonsterBodyType.Biped;
        if (s.Contains("chicken_blob") || s.Contains("chickenblob")) return MonsterBodyType.Blob;
        if (s.Contains("pigeon")) return MonsterBodyType.Flying;
        if (s.Contains("dragon")) return MonsterBodyType.FlyingArms;

        if (s.Contains("blob")) return MonsterBodyType.Blob;
        if (s.Contains("flying") && s.Contains("arms")) return MonsterBodyType.FlyingArms;
        if (s.Contains("flying")) return MonsterBodyType.Flying;
        if (s.Contains("bird") || s.Contains("birb")) return MonsterBodyType.Flying;

        if (s.Contains("orc")) return MonsterBodyType.Biped;
        if (s.Contains("ninja")) return MonsterBodyType.Biped;
        if (s.Contains("wizard")) return MonsterBodyType.Biped;
        if (s.Contains("demon")) return MonsterBodyType.Biped;
        if (s.Contains("crawler")) return MonsterBodyType.Biped;
        if (s.Contains("vampire")) return MonsterBodyType.FlyingArms; // She has wings!

        return MonsterBodyType.Unknown;
    }

    public static void SetupSharedAnimations(Node matchNode, string species, AnimationPlayer animPlayer, MonsterBodyType? typeOverride = null)
    {
        MonsterBodyType type = typeOverride ?? GetBodyType(species);
        // GD.Print($"[MonsterVisuals] SetupSharedAnimations: Species '{species}' classified as '{type}'");

        string animFile = type switch
        {
            MonsterBodyType.Biped => "Anim_Biped.res",
            MonsterBodyType.Blob => "Anim_Blob.res",
            MonsterBodyType.Flying => "Anim_Flying.res",
            MonsterBodyType.FlyingArms => "Anim_FlyingArms.res",
            _ => ""
        };

        if (string.IsNullOrEmpty(animFile))
        {
            Skeleton3D skeleton = FindSkeletonRecursive(matchNode);
            if (skeleton == null && matchNode.GetParent() != null)
                skeleton = FindSkeletonRecursive(matchNode.GetParent());

            if (skeleton == null) return;

            string armatureName = skeleton.GetParent().Name.ToString();
            string animId = armatureName.Replace("CharacterArmature", "").Replace(".", "").Trim();
            string path = "res://Assets/Animations/Monsters/Anim_" + animId + ".res";
            if (!ResourceLoader.Exists(path)) path = "res://Assets/Animations/Monsters/Anim__" + animId + ".res";

            if (ResourceLoader.Exists(path)) PlaySharedAnimation(path, animPlayer, skeleton);
            return;
        }

        string fullPath = "res://Assets/Animations/Monsters/" + animFile;
        Skeleton3D skel = FindSkeletonRecursive(matchNode);
        if (skel == null && matchNode.GetParent() != null) skel = FindSkeletonRecursive(matchNode.GetParent());

        if (skel != null && ResourceLoader.Exists(fullPath))
        {
            PlaySharedAnimation(fullPath, animPlayer, skel);
        }
    }

    public static void PlaySharedAnimation(string path, AnimationPlayer animPlayer, Skeleton3D targetSkeleton)
    {
        if (animPlayer == null || targetSkeleton == null) return;

        if (!ResourceLoader.Exists(path))
        {
            GD.PrintErr($"[MonsterVisuals] Resource not found: {path}");
            return;
        }

        var anim = ResourceLoader.Load<Animation>(path);
        if (anim == null) return;

        var uniqueAnim = (Animation)anim.Duplicate();
        uniqueAnim.LoopMode = Animation.LoopModeEnum.Linear;

        Node rootNode = animPlayer.GetNode(animPlayer.RootNode) ?? animPlayer.GetParent();
        NodePath pathFromRootToSkel = rootNode.GetPathTo(targetSkeleton);

        // Detect Bone Suffix
        string suffix = "";
        for (int b = 0; b < targetSkeleton.GetBoneCount(); b++)
        {
            string bName = targetSkeleton.GetBoneName(b);
            if (bName.Contains("_CharacterArmature"))
            {
                int idx = bName.IndexOf("_CharacterArmature");
                suffix = bName.Substring(idx);
                break;
            }
        }

        // Retarget Tracks
        int trackCount = uniqueAnim.GetTrackCount();
        for (int i = 0; i < trackCount; i++)
        {
            NodePath trackPath = uniqueAnim.TrackGetPath(i);
            string nodePathStr = trackPath.ToString();

            if (nodePathStr.Contains(":"))
            {
                var parts = nodePathStr.Split(':');
                string nodeName = parts[0];
                string boneName = parts[1];

                if (nodeName == "Skeleton3D")
                {
                    string newBoneName = boneName + suffix;
                    uniqueAnim.TrackSetPath(i, new NodePath($"{pathFromRootToSkel}:{newBoneName}"));
                }
            }
        }

        var lib = animPlayer.GetAnimationLibrary("");
        if (lib == null)
        {
            lib = new AnimationLibrary();
            animPlayer.AddAnimationLibrary("", lib);
        }

        lib.AddAnimation("Shared", uniqueAnim);
        animPlayer.Play("Shared");
        // GD.Print($"[MonsterVisuals] Playing Shared Anim: {path.GetFile()} on {targetSkeleton.Name} (Suffix: {suffix})");
    }

    public static Skeleton3D FindSkeletonRecursive(Node node)
    {
        if (node is Skeleton3D skel) return skel;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSkeletonRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    public static Skeleton3D FindVisibleSkeleton(Node node)
    {
        if (node is Skeleton3D skel && skel.IsVisibleInTree()) return skel;
        foreach (Node c in node.GetChildren())
        {
            var res = FindVisibleSkeleton(c);
            if (res != null) return res;
        }
        return null;
    }

    public static CollisionShape3D FindCollisionShapeRecursive(Node node)
    {
        if (node is CollisionShape3D col) return col;
        foreach (Node child in node.GetChildren())
        {
            var found = FindCollisionShapeRecursive(child);
            if (found != null) return found;
        }
        return null;
    }
}
