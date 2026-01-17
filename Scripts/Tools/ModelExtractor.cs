using Godot;
using System;
using System.Collections.Generic;

namespace Archery.Tools;

[Tool]
public partial class ModelExtractor : Node
{
    [Export] public bool RunExtraction { get; set; } = false;

    [Export] public string SourcePath = "res://Assets/Textures/Monsters/scene.gltf";
    [Export] public string OutputDir = "res://Assets/Monsters/Extracted/";

    public override void _Process(double delta)
    {
        if (RunExtraction)
        {
            RunExtraction = false;
            ExtractModels();
        }
    }

    private void ExtractModels()
    {
        GD.Print("[ModelExtractor] Starting extraction...");

        // Ensure output directory exists
        var dir = DirAccess.Open("res://");
        if (!dir.DirExists(OutputDir))
        {
            dir.MakeDirRecursive(OutputDir);
        }

        // Load the source scene
        PackedScene sourceScene = ResourceLoader.Load<PackedScene>(SourcePath);
        if (sourceScene == null)
        {
            GD.PrintErr($"[ModelExtractor] Failed to load source: {SourcePath}");
            return;
        }

        Node root = sourceScene.Instantiate();
        GD.Print($"[ModelExtractor] Source loaded. Root: {root.Name} ({root.GetChildCount()} children)");

        // Find all nodes that are likely armatures (contain a Skeleton3D)
        List<Node> armatures = new List<Node>();
        FindArmaturesRecursive(root, armatures);

        GD.Print($"[ModelExtractor] Found {armatures.Count} potential armatures/monsters.");

        int count = 0;
        foreach (Node armature in armatures)
        {
            string speciesName = DetectSpeciesInternal(armature);

            if (!string.IsNullOrEmpty(speciesName))
            {
                // Check if file already exists? No, overwrite.
                ExtractSingleMonster(armature, speciesName);
                count++;
            }
            else
            {
                GD.Print($"[ModelExtractor] Skipping armature {armature.Name}: Could not detect species.");
            }
        }

        GD.Print($"[ModelExtractor] Extraction complete. {count} monsters saved to {OutputDir}");
        root.QueueFree();
    }

    private void FindArmaturesRecursive(Node node, List<Node> list)
    {
        // Check if this node has a Skeleton3D child
        bool hasSkeleton = false;
        foreach (Node child in node.GetChildren())
        {
            if (child is Skeleton3D)
            {
                hasSkeleton = true;
                break;
            }
        }

        if (hasSkeleton)
        {
            // This is a monster root!
            list.Add(node);
            // Do not recurse further into this monster (unless nested monsters exist? Unlikely)
            return;
        }

        // Recurse
        foreach (Node child in node.GetChildren())
        {
            FindArmaturesRecursive(child, list);
        }
    }

    private string DetectSpeciesInternal(Node node)
    {
        // Look for meshes within this node's subtree
        string foundName = "";

        // Helper to scan for mesh names
        void Scan(Node n)
        {
            if (!string.IsNullOrEmpty(foundName)) return;

            if (n is MeshInstance3D mesh)
            {
                string name = mesh.Name; // e.g., "Yeti_0"

                // Filter out common generic names
                if (name.Contains("Skeleton") || name.Contains("Armature")) return;

                // Clean up: "Yeti_0" -> "Yeti"
                // Regex: Name_Number
                int underscoreIndex = name.LastIndexOf('_');
                if (underscoreIndex > 0)
                {
                    string suffix = name.Substring(underscoreIndex + 1);
                    if (int.TryParse(suffix, out _))
                    {
                        // Also check for double naming like "Yeti_Blob_001_0"
                        // Just use everything before the last number
                        foundName = name.Substring(0, underscoreIndex);
                        return;
                    }
                }

                // Fallback
                foundName = name;
            }

            foreach (Node c in n.GetChildren()) Scan(c);
        }

        Scan(node);

        // Capitalize / Clean
        if (!string.IsNullOrEmpty(foundName))
        {
            foundName = foundName.Replace("Object_", "");
            // Clean up trailing numbers just in case "Blob_001"
            return foundName;
        }

        return null;
    }

    private void ExtractSingleMonster(Node armatureNode, string speciesName)
    {
        // 1. Duplicate the node to isolate it
        Node dupe = armatureNode.Duplicate();

        // 2. Reset Transform (Spatial/Node3D)
        if (dupe is Node3D n3d)
        {
            n3d.Position = Vector3.Zero;
            n3d.Rotation = Vector3.Zero;
            n3d.Scale = Vector3.One;
        }

        // 3. Name the root node
        dupe.Name = speciesName;

        // 4. Set owner recursively so it saves correctly
        // The root of the packed scene must be the owner of all children
        SetOwnerRecursive(dupe, dupe);

        // 5. Pack and Save
        PackedScene scene = new PackedScene();
        Error err = scene.Pack(dupe);
        if (err == Error.Ok)
        {
            string filename = speciesName + ".tscn";
            // Sanitize filename
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c, '_');
            }

            string fullPath = OutputDir + filename;
            ResourceSaver.Save(scene, fullPath);
            GD.Print($"[ModelExtractor] Saved: {fullPath}");
        }
        else
        {
            GD.PrintErr($"[ModelExtractor] Failed to pack {speciesName}: {err}");
        }

        dupe.QueueFree();
    }

    private void SetOwnerRecursive(Node node, Node owner)
    {
        if (node != owner)
        {
            node.Owner = owner;
        }
        foreach (Node child in node.GetChildren())
        {
            SetOwnerRecursive(child, owner);
        }
    }
}
