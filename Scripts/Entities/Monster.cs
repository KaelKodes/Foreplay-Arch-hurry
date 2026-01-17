using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public enum MonsterBodyType
{
    Biped,
    Blob,
    Flying,
    FlyingArms,
    Unknown
}

public partial class Monster : InteractableObject
{
    [Export] public float Health = 100.0f;
    [Export] public MonsterBodyType BodyTypeOverride = MonsterBodyType.Unknown;

    private string _species = "";
    [Export]
    public string Species
    {
        get => _species;
        set
        {
            _species = value;
            if (IsInsideTree()) UpdateSpeciesVisuals();
        }
    }

    private AnimationPlayer _animPlayer;
    private bool _isDead = false;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("interactables");
        AddToGroup("monsters");

        // Monsters should be targetable by default
        IsTargetable = true;

        // Prioritize searching children first (where the GLTF anim player lives)
        _animPlayer = FindAnimationPlayerRecursive(this);

        // Fallback to local if not found in children
        if (_animPlayer == null)
        {
            _animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        }

        if (_animPlayer != null)
        {
            var anims = _animPlayer.GetAnimationList();
            // If this player is empty, try to find another one
            if (anims.Length == 0)
            {
                GD.Print($"[Monster] Found empty AnimationPlayer at {_animPlayer.GetPath()}. Searching further...");
                _animPlayer = FindPopulatedAnimationPlayerRecursive(this);
            }
        }

        // NOW update visuals (which relies on _animPlayer for SetupSharedAnimations)
        UpdateSpeciesVisuals();

        if (_animPlayer != null)
        {
            GD.Print($"[Monster] Using AnimationPlayer: {_animPlayer.GetPath()} ({_animPlayer.GetAnimationList().Length} animations)");
            PlayAnimationRobust("Idle");
        }
        else
        {
            GD.PrintErr("[Monster] No AnimationPlayer found anywhere in hierarchy!");
        }
    }

    private void PlayAnimationRobust(string animName)
    {
        if (_animPlayer == null) return;

        var anims = _animPlayer.GetAnimationList();

        // 1. Try exact match
        if (_animPlayer.HasAnimation(animName))
        {
            _animPlayer.Play(animName);
        }
        // 3. FALLBACK: If we wanted "Idle" or "Walk" but found nothing,
        // play the FIRST available animation (likely "Take 01") so the monster isn't static.
        else if (anims.Length > 0)
        {
            string fallback = anims[0];

            // Ensure it loops
            var animRef = _animPlayer.GetAnimation(fallback);
            if (animRef != null)
            {
                animRef.LoopMode = Animation.LoopModeEnum.Linear;
            }

            // Only print if it's not the one we're already playing to avoid spam
            _animPlayer.Play(fallback);
        }
    }

    private void UpdateSpeciesVisuals()
    {
        if (string.IsNullOrEmpty(Species)) return;

        var scene = GetNodeOrNull("Visuals/scene");
        if (scene == null)
        {
            GD.PrintErr($"[Monster] UpdateSpeciesVisuals: Visuals/scene not found for {Name}");
            return;
        }

        GD.Print($"[Monster] --- Filtering visuals for Species: '{Species}' ---");

        // Reset scene transform before processing new species to avoid offset accumulation
        if (scene is Node3D scene3D) scene3D.Transform = Transform3D.Identity;

        // Step 1: Hide EVERYTHING deep
        HideAllRecursive(scene);

        // Step 2: Show matching nodes and their ancestors
        bool found = false;
        ShowSpeciesRecursive(scene, Species, ref found);

        if (found)
        {
            ObjectName = Species;
            GD.Print($"[Monster] Successfully isolated Species: {Species}");

            // Center and ground
            CenterAndGroundVisuals(scene);
            UpdateCollisionShape();
        }
        else
        {
            GD.PrintErr($"[Monster] FAILED to find any nodes matching Species: '{Species}'. Showing everything as fallback.");
            // Print existing node names to help debug
            GD.Print("[Monster] Available top-level nodes in scene:");
            foreach (Node child in scene.GetChildren())
            {
                GD.Print($"  - {child.Name}");
            }

            ShowAllRecursive(scene);
            if (scene is Node3D n3d) n3d.Transform = Transform3D.Identity;
        }
    }

    private void CenterAndGroundVisuals(Node scene)
    {
        if (!(scene is Node3D scene3D)) return;

        // Start with an empty AABB
        Aabb totalAabb = new Aabb();
        bool first = true;

        CalculateVisibleAabb(scene3D, ref totalAabb, ref first);

        if (first)
        {
            GD.PrintErr($"[Monster] Could not calculate AABB for {Species} - no visible meshes found!");
            return;
        }

        GD.Print($"[Monster] Calculated AABB for {Species}: {totalAabb}");

        // Calculate offset to bring center to (0,0) and bottom to y=0
        // Note: totalAabb is in GLOBAL space if we calculated it using global transforms,
        // but if we use local relative to 'scene', it's easier.
        // Let's assume the AABB we got is relative to the 'Monster' node (this).

        Vector3 centerOffset = -totalAabb.GetCenter();
        centerOffset.Y = -totalAabb.Position.Y; // Bring bottom to 0

        // Apply this offset to the 'scene' node (which is at Visuals/scene)
        // But wait, totalAabb was relative to 'Monster'.
        // The 'scene' node is at Visuals/scene (offset by Visuals transform).
        // Let's just adjust scene.Position directly.

        scene3D.Position += centerOffset;

        // Force update visuals immediately so they don't 'flicker' in the wrong spot
        scene3D.ForceUpdateTransform();
        GD.Print($"[Monster] Grounding offset applied: {centerOffset}");
    }

    private void CalculateVisibleAabb(Node3D node, ref Aabb totalAabb, ref bool first, Transform3D cumulativeTransform = default)
    {
        if (cumulativeTransform == default) cumulativeTransform = Transform3D.Identity;

        if (node is VisualInstance3D vi && node.Visible)
        {
            // Get AABB in local space
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

    private void UpdateCollisionShape()
    {
        var colShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (colShape == null) return;

        // Recalculate AABB relative to the Monster node
        Aabb finalAabb = new Aabb();
        bool first = true;

        // Start from children to get AABB relative to 'this'
        foreach (Node child in GetChildren())
        {
            if (child is Node3D n3d && n3d.Name != "CollisionShape3D")
                CalculateVisibleAabb(n3d, ref finalAabb, ref first, Transform3D.Identity);
        }

        if (first) return;

        // We'll use a BoxShape3D or CapsuleShape3D. Box is easiest to match AABB.
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
            // If it's something else, just switch to Box for accuracy
            var newBox = new BoxShape3D();
            newBox.Size = finalAabb.Size;
            colShape.Shape = newBox;
            colShape.Position = finalAabb.GetCenter();
        }

        GD.Print($"[Monster] Collision adjusted to Size: {finalAabb.Size}, Pos: {colShape.Position}");
    }

    private void HideAllRecursive(Node node)
    {
        if (node is Node3D n3d) n3d.Visible = false;
        foreach (Node child in node.GetChildren()) HideAllRecursive(child);
    }

    private void ShowAllRecursive(Node node)
    {
        if (node is Node3D n3d) n3d.Visible = true;
        foreach (Node child in node.GetChildren()) ShowAllRecursive(child);
    }

    private void ShowSpeciesRecursive(Node node, string species, ref bool found)
    {
        foreach (Node child in node.GetChildren())
        {
            string name = child.Name.ToString();
            bool isMatch = IsStrictSpeciesMatch(name, species);

            if (isMatch)
            {
                if (!found) GD.Print($"  [Monster] MATCH FOUND: {name} for {species}");
                found = true;
                ShowAncestors(child);
                ShowAllRecursive(child);

                // Setup shared animations if this is the armature/root of the monster
                SetupSharedAnimations(child);
            }
            else
            {
                ShowSpeciesRecursive(child, species, ref found);
            }
        }
    }

    private void SetupSharedAnimations(Node matchNode)
    {
        MonsterBodyType type = GetBodyType(Species);

        // Allow manual override for testing specific models
        if (BodyTypeOverride != MonsterBodyType.Unknown) type = BodyTypeOverride;

        GD.Print($"[Monster] SetupSharedAnimations: Species '{Species}' classified as '{type}'");

        string animFile = "";
        switch (type)
        {
            case MonsterBodyType.Biped: animFile = "Anim_Biped.res"; break;
            case MonsterBodyType.Blob: animFile = "Anim_Blob.res"; break;
            case MonsterBodyType.Flying: animFile = "Anim_Flying.res"; break;
            case MonsterBodyType.FlyingArms: animFile = "Anim_FlyingArms.res"; break;
            default:
                break;
        }

        if (string.IsNullOrEmpty(animFile))
        {
            // Fallback to the old "ID-based" lookup if no category match?
            // actually the user wants strictly 4 types.
            // Let's try to map dynamically if still unknown.
            GD.Print($"[Monster] No category for {Species}, checking specific ID...");

            Skeleton3D skeleton = FindSkeletonRecursive(matchNode);
            if (skeleton == null && matchNode.GetParent() != null)
                skeleton = FindSkeletonRecursive(matchNode.GetParent());

            if (skeleton == null) return;

            string armatureName = skeleton.GetParent().Name.ToString();
            string animId = armatureName.Replace("CharacterArmature", "").Replace(".", "").Trim();
            string path = "res://Assets/Animations/Monsters/Anim_" + animId + ".res";
            if (!ResourceLoader.Exists(path)) path = "res://Assets/Animations/Monsters/Anim__" + animId + ".res";

            if (ResourceLoader.Exists(path)) PlaySharedAnimation(path, skeleton);
            return;
        }

        string fullPath = "res://Assets/Animations/Monsters/" + animFile;

        Skeleton3D skel = FindSkeletonRecursive(matchNode);
        if (skel == null && matchNode.GetParent() != null) skel = FindSkeletonRecursive(matchNode.GetParent());

        if (skel != null && ResourceLoader.Exists(fullPath))
        {
            PlaySharedAnimation(fullPath, skel);
        }
    }

    private MonsterBodyType GetBodyType(string species)
    {
        // Normalize
        string s = species.ToLower();

        // Explicit Prototypes
        if (s.Contains("yeti")) return MonsterBodyType.Biped;
        if (s.Contains("chicken_blob") || s.Contains("chickenblob")) return MonsterBodyType.Blob;
        if (s.Contains("pigeon")) return MonsterBodyType.Flying;
        if (s.Contains("dragon")) return MonsterBodyType.FlyingArms;

        // Heuristics
        if (s.Contains("blob")) return MonsterBodyType.Blob;
        if (s.Contains("flying") && s.Contains("arms")) return MonsterBodyType.FlyingArms;
        if (s.Contains("flying")) return MonsterBodyType.Flying;
        if (s.Contains("bird") || s.Contains("birb")) return MonsterBodyType.Flying;

        // Biped defaults
        if (s.Contains("orc")) return MonsterBodyType.Biped;
        if (s.Contains("ninja")) return MonsterBodyType.Biped;
        if (s.Contains("wizard")) return MonsterBodyType.Biped;
        if (s.Contains("demon")) return MonsterBodyType.Biped; // Usually biped

        return MonsterBodyType.Unknown;
    }

    private void PlaySharedAnimation(string path, Skeleton3D targetSkeleton = null)
    {
        if (_animPlayer == null) return;

        // If skeleton not provided, try to find the active one for the current Species
        if (targetSkeleton == null)
        {
            // Find the visible mesh first? Hard to track.
            // Better to rely on SetupSharedAnimations passing it, OR cache it.
            // For Debug Shuffle, we might need to find it again.
            // Let's search under Visuals/scene
            var scene = GetNodeOrNull("Visuals/scene");
            if (scene != null)
            {
                // We need to find the one that is Visible!
                targetSkeleton = FindVisibleSkeleton(scene);
            }
        }

        if (targetSkeleton == null)
        {
            GD.PrintErr("[Monster] PlaySharedAnimation: No target skeleton found!");
            return;
        }

        if (!ResourceLoader.Exists(path))
        {
            GD.PrintErr($"[Monster] Resource not found: {path}");
            return;
        }

        var anim = ResourceLoader.Load<Animation>(path);
        if (anim == null) return;

        // 1. Duplicate for runtime modification
        var uniqueAnim = (Animation)anim.Duplicate();
        uniqueAnim.LoopMode = Animation.LoopModeEnum.Linear;

        // 2. Calculate Path from AnimPlayer Root to Skeleton
        // _animPlayer.RootNode is a NodePath, usually relative to _animPlayer.
        // We need the actual Node it points to.
        Node rootNode = _animPlayer.GetNode(_animPlayer.RootNode);
        if (rootNode == null) rootNode = _animPlayer.GetParent(); // Fallback

        NodePath pathFromRootToSkel = rootNode.GetPathTo(targetSkeleton);
        // pathFromRootToSkel might be "Sketchfab_model/Root/etc/Skeleton3D"

        // 3. Detect Bone Suffix
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

        // 4. Retarget Tracks
        int trackCount = uniqueAnim.GetTrackCount();
        for (int i = 0; i < trackCount; i++)
        {
            NodePath trackPath = uniqueAnim.TrackGetPath(i);
            string nodePathStr = trackPath.ToString(); // "Skeleton3D:Bone"

            if (nodePathStr.Contains(":"))
            {
                var parts = nodePathStr.Split(':');
                string nodeName = parts[0]; // "Skeleton3D"
                string boneName = parts[1]; // "Bone"

                // If the track points to "Skeleton3D", we replace it with the ACTUAL path
                if (nodeName == "Skeleton3D")
                {
                    string newBoneName = boneName + suffix;

                    // Reconstruct path: "Path/To/Skeleton3D:Bone_Suffix"
                    uniqueAnim.TrackSetPath(i, new NodePath($"{pathFromRootToSkel}:{newBoneName}"));
                }
            }
        }

        // 5. Play
        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null)
        {
            lib = new AnimationLibrary();
            _animPlayer.AddAnimationLibrary("", lib);
        }

        // Use a unique name so we don't overwrite "Shared" if we alternate rapidly?
        // "Shared" is fine.
        lib.AddAnimation("Shared", uniqueAnim);
        _animPlayer.Play("Shared");
        GD.Print($"[Monster] Playing Shared Anim: {path.GetFile()} on {targetSkeleton.Name} (Suffix: {suffix})");
    }

    private Skeleton3D FindVisibleSkeleton(Node node)
    {
        if (node is Skeleton3D skel && skel.IsVisibleInTree()) return skel;
        foreach (Node c in node.GetChildren())
        {
            var res = FindVisibleSkeleton(c);
            if (res != null) return res;
        }
        return null;
    }

    private Skeleton3D FindSkeletonRecursive(Node node)
    {
        if (node is Skeleton3D skel) return skel;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSkeletonRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private void NormalizeSkeleton(Skeleton3D skeleton)
    {
        // Rename bones: remove `_CharacterArmature.XXX` suffix
        int boneCount = skeleton.GetBoneCount();
        for (int i = 0; i < boneCount; i++)
        {
            string oldName = skeleton.GetBoneName(i);
            // Regex replace
            string newName = System.Text.RegularExpressions.Regex.Replace(oldName, @"_CharacterArmature[\._]\d+", "");
            if (newName != oldName)
            {
                skeleton.SetBoneName(i, newName);
            }
        }
        // GD.Print($"[Monster] Normalized {boneCount} bones for {skeleton.Name}");
    }

    private bool IsStrictSpeciesMatch(string nodeName, string species)
    {
        // Case-insensitive exact match
        if (string.Equals(nodeName, species, StringComparison.OrdinalIgnoreCase)) return true;

        // Handle Godot/GLTF variants like "Species.001" or "Species_0"
        if (nodeName.StartsWith(species, StringComparison.OrdinalIgnoreCase))
        {
            string remainder = nodeName.Substring(species.Length);
            if (string.IsNullOrEmpty(remainder)) return true;

            char sep = remainder[0];
            if (sep == '.' || sep == '_')
            {
                string suffix = remainder.Substring(1);
                if (string.IsNullOrEmpty(suffix)) return true;

                // Allow numerical suffixes (e.g., Yeti.001, Orc_0)
                if (int.TryParse(suffix, out _)) return true;

                // Allow common root identifiers
                if (string.Equals(suffix, "Main", StringComparison.OrdinalIgnoreCase)) return true;

                // Specific check: if we are looking for "Orc", don't match "Orc_Blob"
                // If the suffix starts with another underscore, it's likely a different part/variant
                // unless the species name itself ends in a way that includes it.
            }
        }

        return false;
    }

    private void ShowAncestors(Node node)
    {
        var current = node;
        while (current != null && current.Name != "scene")
        {
            if (current is Node3D n3d) n3d.Visible = true;
            current = current.GetParent();
        }
        // Ensure the scene root itself is visible if it's a Node3D
        if (current is Node3D root) root.Visible = true;
    }

    private void PrintVisibilityRecursive(Node node, int indent)
    {
        string prefix = new string(' ', indent * 2);
        string vis = (node is Node3D n3d) ? (n3d.Visible ? "[V]" : "[H]") : "[?]";
        GD.Print($"{prefix}{vis} {node.Name}");
        if (indent < 3) // Limit depth
        {
            foreach (Node child in node.GetChildren()) PrintVisibilityRecursive(child, indent + 1);
        }
    }

    private bool CheckMeshNames(Node node, string species)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Name.ToString().Contains(species, StringComparison.OrdinalIgnoreCase))
                return true;
            if (CheckMeshNames(child, species)) return true;
        }
        return false;
    }

    private void FilterNodesRecursive(Node node, string species, ref bool found)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Node3D n3d)
            {
                string name = n3d.Name.ToString();

                // If this node explicitly matches our species, show it and stop recursing this branch
                if (name.Contains(species, StringComparison.OrdinalIgnoreCase))
                {
                    n3d.Visible = true;
                    found = true;
                    GD.Print($"  [Monster] SHOWING Root: {name}");
                    continue;
                }

                // If it contains a DIFFERENT known species name, hide it
                string[] otherSpecies = { "Yeti", "Orc", "Wizard", "Pigeon", "Ninja", "Pink", "Warrior" };
                bool isOther = false;
                foreach (var s in otherSpecies)
                {
                    if (s.Equals(species, StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains(s, StringComparison.OrdinalIgnoreCase))
                    {
                        isOther = true;
                        break;
                    }
                }

                if (isOther)
                {
                    n3d.Visible = false;
                    // GD.Print($"  [Monster] HIDING: {name}");
                }
                else
                {
                    // Some intermediate node (like Sketchfab_model), keep visible but recurse
                    n3d.Visible = true;
                    FilterNodesRecursive(n3d, species, ref found);
                }
            }
        }
    }

    private AnimationPlayer FindAnimationPlayerRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is AnimationPlayer ap) return ap;
            var found = FindAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private AnimationPlayer FindPopulatedAnimationPlayerRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is AnimationPlayer ap && ap.GetAnimationList().Length > 0) return ap;
            var found = FindPopulatedAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal)
    {
        if (_isDead) return;

        Health -= damage;
        GD.Print($"[Monster] {ObjectName} hit! Health: {Health}");

        if (Health <= 0)
        {
            Die();
        }
        else
        {
            PlayHitReaction();
        }
    }

    private void PlayHitReaction()
    {
        PlayAnimationRobust("Hit");
        // Queue Idle if possible, otherwise it just stays on Hit last frame
        if (_animPlayer != null && _animPlayer.HasAnimation("Idle"))
            _animPlayer.Queue("Idle");
        else if (_animPlayer != null)
        {
            // Try to find a match for Idle to queue
            foreach (var anim in _animPlayer.GetAnimationList())
            {
                if (anim.ToString().Contains("Idle", StringComparison.OrdinalIgnoreCase))
                {
                    _animPlayer.Queue(anim);
                    break;
                }
            }
        }

        // Visual feedback: brief flash or shake
        Tween tween = GetTree().CreateTween();
        tween.TweenProperty(this, "scale", new Vector3(1.2f, 1.2f, 1.2f), 0.1f);
        tween.TweenProperty(this, "scale", Vector3.One, 0.1f);
    }

    private void Die()
    {
        _isDead = true;
        GD.Print($"[Monster] {ObjectName} died!");

        PlayAnimationRobust("Death");

        // Handle cleanup after a delay
        SceneTreeTimer timer = GetTree().CreateTimer(2.0f);
        timer.Timeout += () => QueueFree();
    }

    public override void _Process(double delta)
    {
        if (Input.IsKeyPressed(Key.T))
        {
            DebugShuffleAnimation();
        }
    }

    private double _lastShuffleTime = 0;
    private void DebugShuffleAnimation()
    {
        // Debounce
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastShuffleTime < 0.2) return;
        _lastShuffleTime = now;

        if (_animPlayer == null) return;

        // Load a random animation from the folder
        string dirPath = "res://Assets/Animations/Monsters/";
        var dir = DirAccess.Open(dirPath);
        if (dir != null)
        {
            dir.ListDirBegin();
            List<string> files = new List<string>();
            string file = dir.GetNext();
            while (file != "")
            {
                if (file.EndsWith(".res") || file.EndsWith(".tres")) files.Add(file);
                file = dir.GetNext();
            }

            if (files.Count > 0)
            {
                var rnd = new RandomNumberGenerator();
                rnd.Randomize();
                string chosen = files[rnd.RandiRange(0, files.Count - 1)];
                GD.Print($"[Monster] Debug Shuffle: Playing {chosen} on {Species}");
                PlaySharedAnimation(dirPath + chosen);
            }
        }
    }
}
