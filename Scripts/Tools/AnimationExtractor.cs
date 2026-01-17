using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class AnimationExtractor : Node
{
    // Configure this path to your GLTF
    [Export] public string GltfPath = "res://Assets/Textures/Monsters/scene.gltf";
    [Export] public string OutputDir = "res://Assets/Animations/Monsters/";
    [Export] public bool RunExtraction = false;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            GD.Print("[Extractor] Ready. Click the 'AnimationExtractor' node and check 'Run Extraction' in the Inspector to start.");
        }
    }

    public override void _Process(double delta)
    {
        if (RunExtraction)
        {
            RunExtraction = false;
            ExtractAnimations();
        }
    }

    private void ExtractAnimations()
    {
        GD.Print("[Extractor] Starting extraction...");

        // Ensure output dir exists
        var dir = DirAccess.Open("res://");
        if (!dir.DirExists(OutputDir))
        {
            dir.MakeDirRecursive(OutputDir);
        }

        // Load the packed scene
        var scene = GD.Load<PackedScene>(GltfPath);
        if (scene == null)
        {
            GD.PrintErr("[Extractor] Failed to load GLTF at " + GltfPath);
            return;
        }

        // Instantiate to inspect structure
        var instance = scene.Instantiate();
        AddChild(instance); // Add to tree momentarily to ensure readiness

        // Find AnimationPlayer
        AnimationPlayer animPlayer = null;
        FindAnimPlayer(instance, ref animPlayer);

        if (animPlayer == null)
        {
            GD.PrintErr("[Extractor] No AnimationPlayer found!");
            instance.QueueFree();
            return;
        }

        // Get "Take 01"
        if (!animPlayer.HasAnimation("Take 01"))
        {
            GD.PrintErr("[Extractor] 'Take 01' not found!");
            instance.QueueFree();
            return;
        }

        var sourceAnim = animPlayer.GetAnimation("Take 01");

        // Groups: ArmatureName -> List of tracks (TrackIdx, NormalizedPath)
        Dictionary<string, List<(int UniqIdx, string NormalizedPath)>> armatureGroups = new Dictionary<string, List<(int, string)>>();

        int trackCount = sourceAnim.GetTrackCount();
        GD.Print($"[Extractor] Processing {trackCount} tracks...");

        for (int i = 0; i < trackCount; i++)
        {
            string path = sourceAnim.TrackGetPath(i).ToString();
            // Example: Sketchfab_model/Root/CharacterArmature_026/Skeleton3D:Head_CharacterArmature.026

            var parts = path.Split('/');
            if (parts.Length < 4) continue;

            string armatureName = parts[2]; // e.g. CharacterArmature_026
            string remaining = parts[3];    // e.g. Skeleton3D:Head_CharacterArmature.026

            // Normalize: Strip the unique suffix (e.g. "_CharacterArmature.026")
            // This turns "Skeleton3D:Head_CharacterArmature.026" into "Skeleton3D:Head"
            string normalizedPath = System.Text.RegularExpressions.Regex.Replace(remaining, @"_CharacterArmature[\._]\d+", "");

            // Clean up potentially left over trailing dots or underscores if the name format varies
            // But strict regex above is safer.

            if (!armatureGroups.ContainsKey(armatureName))
                armatureGroups[armatureName] = new List<(int, string)>();

            armatureGroups[armatureName].Add((i, normalizedPath));
        }

        GD.Print($"[Extractor] Found {armatureGroups.Count} unique armatures.");

        foreach (var kvp in armatureGroups)
        {
            string armatureName = kvp.Key;
            var tracks = kvp.Value;

            // Create new Animation
            var newAnim = new Animation();
            newAnim.LoopMode = Animation.LoopModeEnum.Linear;
            newAnim.Length = sourceAnim.Length;

            foreach (var track in tracks)
            {
                int srcIdx = track.UniqIdx;
                string newPath = track.NormalizedPath; // e.g. "Skeleton3D:Head"

                // Add track
                int dstIdx = newAnim.AddTrack(sourceAnim.TrackGetType(srcIdx));
                newAnim.TrackSetPath(dstIdx, newPath);

                // Copy keys
                int keyCount = sourceAnim.TrackGetKeyCount(srcIdx);
                for (int k = 0; k < keyCount; k++)
                {
                    double time = sourceAnim.TrackGetKeyTime(srcIdx, k);
                    var val = sourceAnim.TrackGetKeyValue(srcIdx, k);
                    double transition = sourceAnim.TrackGetKeyTransition(srcIdx, k);

                    newAnim.TrackInsertKey(dstIdx, time, val, (float)transition);
                }

                // Copy interpolation
                newAnim.TrackSetInterpolationType(dstIdx, sourceAnim.TrackGetInterpolationType(srcIdx));
            }

            // Save resource
            string cleanName = armatureName.Replace("CharacterArmature", "").Replace(".", "").Trim();
            if (string.IsNullOrEmpty(cleanName)) cleanName = "Base";

            // Try to resolve a Monster Name if possible?
            // We'll just save by Armature ID for now: "Anim_030.res"
            // User can rename later or we can map manually.
            string filename = $"{OutputDir}Anim_{cleanName}.res";
            ResourceSaver.Save(newAnim, filename);
            GD.Print($"[Extractor] Saved {filename} ({tracks.Count} tracks)");
        }

        instance.QueueFree();
        GD.Print("[Extractor] DONE.");
    }

    private void FindAnimPlayer(Node node, ref AnimationPlayer found)
    {
        if (found != null) return;
        if (node is AnimationPlayer ap)
        {
            found = ap;
            return;
        }
        foreach (Node child in node.GetChildren())
        {
            FindAnimPlayer(child, ref found);
        }
    }
}
