using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class InteractableObject : Node3D
{
    [Export] public string ObjectName = "Object";
    [Export] public bool IsMovable = true;
    [Export] public bool IsDeletable = true;
    [Export] public bool IsTargetable = false;
    [Export] public string ModelPath = ""; // Path to the source .gltf or .fbx for texture loading
    public string ScenePath { get; set; } = ""; // Path to the .tscn scene for network spawning (if different from ModelPath)
    public bool IsSelected { get; private set; } = false;

    // Optional: Visual highlight
    private MeshInstance3D _mesh;
    private MeshInstance3D _gizmoRing;

    // Interaction API
    public virtual void OnInteract(PlayerController player) { }
    public virtual string GetInteractionPrompt() { return ""; }
    public virtual void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal) { }

    public override void _Ready()
    {
        // Try to find a mesh for highlighting
        _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (_mesh == null)
        {
            // Fallback: Check children recursively
            _mesh = FindMeshRecursive(this);
        }

        // Create Rotation Gizmo
        CreateGizmo();

        // Cache original material
        if (_mesh != null)
        {
            _originalMaterial = _mesh.GetActiveMaterial(0) as StandardMaterial3D;
        }

        // AUTO-TEXTURE FALLBACK (Fix for grey meshes)
        GD.Print($"[InteractableObject] {Name} ModelPath='{ModelPath}'");
        if (!string.IsNullOrEmpty(ModelPath))
        {
            string lowerPath = ModelPath.ToLower();
            if (lowerPath.EndsWith(".fbx") || lowerPath.EndsWith(".gltf") || lowerPath.EndsWith(".glb"))
            {
                GD.Print($"[InteractableObject] Triggering texture loading for {Name}");
                TryApplyTexturesToModel(this, ModelPath);
            }
        }

        // AUTO-COLLISION FALLBACK (Fix for tiny/missing collision)
        // Check if we have a StaticBody3D child. If not, generate one from mesh bounds.
        if (GetNodeOrNull<StaticBody3D>("StaticBody3D") == null && FindChild("StaticBody3D", false, false) == null)
        {
            AddDynamicCollision();
        }
    }

    /// <summary>
    /// Generates a BoxShape3D based on the combined AABB of all child meshes.
    /// Used when no manual collision is provided (e.g. raw GLTF/FBX imports).
    /// </summary>
    public void AddDynamicCollision()
    {
        Aabb combinedAabb = new Aabb();
        bool hasAabb = false;

        // Helper to recursively calculate accumulated transform
        void Encapsulate(Node node, Transform3D parentTransform)
        {
            Transform3D currentTransform = parentTransform;

            if (node is Node3D node3D)
            {
                currentTransform = parentTransform * node3D.Transform;
            }

            if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
            {
                // Get AABB in local space of the mesh
                Aabb localAabb = meshInstance.GetAabb();

                // Transform it by the accumulated transform relative to the component root (this InteractableObject)
                // Note: transforming an AABB results in a larger AABB that encloses the original
                Aabb transformedAabb = currentTransform * localAabb;

                // DEBUG: Trace individual mesh bounds
                // GD.Print($"[Collision Debug] Found Mesh: {node.Name} | Local AABB: {localAabb} | Transformed AABB: {transformedAabb}");

                if (!hasAabb)
                {
                    combinedAabb = transformedAabb;
                    hasAabb = true;
                }
                else
                {
                    combinedAabb = combinedAabb.Merge(transformedAabb);
                }
            }

            foreach (Node child in node.GetChildren())
            {
                Encapsulate(child, currentTransform);
            }
        }

        // Start recursion with identity transform (relative to self)
        // We iterate over children because 'this' transform is relative to parent, 
        // effectively 'Identity' in its own local space.
        foreach (Node child in GetChildren())
        {
            Encapsulate(child, Transform3D.Identity);
        }

        if (hasAabb)
        {
            var staticBody = new StaticBody3D();
            staticBody.Name = "StaticBody3D"; // Standard name for lookup

            var colShape = new CollisionShape3D();
            var box = new BoxShape3D();
            // Fix: Some FBX files have huge coord systems or tiny scaling.
            // If the size is crazy small, force a minimal size.
            if (combinedAabb.Size.Length() < 0.1f)
            {
                GD.PrintErr($"[Collision Warning] Calculated AABB for {Name} is tiny ({combinedAabb.Size}). Check import scale!");
                combinedAabb.Size = new Vector3(1, 1, 1);
            }

            box.Size = combinedAabb.Size;

            colShape.Shape = box;
            colShape.Position = combinedAabb.GetCenter(); // Center relative to InteractableObject

            staticBody.AddChild(colShape);
            AddChild(staticBody);

            GD.Print($"InteractableObject: Added Dynamic Collision for {Name} (Size: {box.Size})");
        }
        else
        {
            // Fallback for empty/no-mesh objects (like pure markers)
            GD.Print($"InteractableObject: No meshes found for collision on {Name}, skipping.");
        }
    }

    // Track which mesh+param combinations have been textured to prevent overwrites
    private HashSet<string> _appliedTextures = new HashSet<string>();

    /// <summary>
    /// Scans multiple directories for common texture maps and applies them.
    /// Checks: model folder, parent folder, and common Textures/ subfolders.
    /// </summary>
    private void TryApplyTexturesToModel(Node model, string modelPath)
    {
        // Reset tracking for this model
        _appliedTextures.Clear();

        string dirPath = modelPath.Substring(0, modelPath.LastIndexOf("/") + 1);
        string baseName = System.IO.Path.GetFileNameWithoutExtension(modelPath)
            .Replace("_FBX", "").Replace("_mesh", "").Replace("-", "_").ToLower();

        // Also extract a simpler base name for matching (e.g., "monster_1" from "sk_mesh_monster_1")
        string simpleBaseName = baseName.Replace("sk_mesh_", "").Replace("sk_", "");

        GD.Print($"InteractableObject: Auto-texturing '{baseName}' (simple: '{simpleBaseName}') using folder {dirPath}");

        // Build prioritized search list - most specific first
        var searchDirs = new List<string> { dirPath };

        // Add parent directories (go up 3 levels to handle deeply nested model files)
        string currentDir = dirPath.TrimEnd('/');
        for (int i = 0; i < 3; i++)
        {
            int lastSlash = currentDir.LastIndexOf('/');
            if (lastSlash > 0)
            {
                currentDir = currentDir.Substring(0, lastSlash);
                string parentDir = currentDir + "/";
                // Check texture subfolders BEFORE the parent itself (more specific)
                string[] texSubFolders = { "Textures/", "texture/", "Texture/", "textures/" };
                foreach (var sub in texSubFolders)
                {
                    string texDir = parentDir + sub;
                    if (!searchDirs.Contains(texDir))
                        searchDirs.Add(texDir);
                }
                if (!searchDirs.Contains(parentDir))
                    searchDirs.Add(parentDir);
            }
        }

        GD.Print($"[Texture] Searching {searchDirs.Count} directories for textures...");
        foreach (string searchDir in searchDirs)
        {
            ScanDirectoryForTextures(model, searchDir, baseName, simpleBaseName);
        }

        GD.Print($"[Texture] Applied {_appliedTextures.Count} unique mesh+texture combinations");
    }

    private void ScanDirectoryForTextures(Node model, string dirPath, string baseName, string simpleBaseName)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (dir.CurrentIsDir() && fileName != "." && fileName != "..")
            {
                // Recurse into subdirectories (like T_Monster_1_skin_1/)
                ScanDirectoryForTextures(model, dirPath + fileName + "/", baseName, simpleBaseName);
            }
            else
            {
                string lowerFile = fileName.ToLower();
                // Match textures with flexible patterns
                bool nameMatches = lowerFile.Contains(baseName) || lowerFile.Contains(simpleBaseName);

                // Special case: Vampire model "base mesh ground" -> textures are "vampire girl"
                if (baseName.Contains("base mesh") && lowerFile.Contains("vampire"))
                    nameMatches = true;

                // Special case: Crawler model "anim_monster_1" -> textures are "T_Monster_1_*"
                if ((baseName.Contains("monster_1") || simpleBaseName.Contains("monster_1")) && lowerFile.Contains("monster_1"))
                    nameMatches = true;

                // For textures in a dedicated texture folder, be more permissive
                if (dirPath.ToLower().Contains("/texture") && (lowerFile.EndsWith(".tga") || lowerFile.EndsWith(".png") || lowerFile.EndsWith(".jpg")))
                    nameMatches = true;

                if ((lowerFile.EndsWith(".tga") || lowerFile.EndsWith(".png") || lowerFile.EndsWith(".jpg")) && nameMatches)
                {
                    if (lowerFile.Contains("color") || lowerFile.Contains("albedo") || lowerFile.Contains("diffuse") || lowerFile.Contains("basecolor"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.Albedo);
                    else if (lowerFile.Contains("_nm") || lowerFile.Contains("normal"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.Normal);
                    else if (lowerFile.Contains("_ao") || lowerFile.Contains("occlusion"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.AmbientOcclusion);
                    else if (lowerFile.Contains("metal"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.Metallic);
                    else if (lowerFile.Contains("emissive") || lowerFile.Contains("emission"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.Emission);
                }
            }
            fileName = dir.GetNext();
        }
    }

    private void ApplySingleTexture(Node root, string texPath, StandardMaterial3D.TextureParam param)
    {
        var texture = GD.Load<Texture2D>(texPath);
        if (texture == null) return;

        string texFileName = System.IO.Path.GetFileNameWithoutExtension(texPath).ToLower();

        // Extract surface hint from texture name (e.g., "BODY", "ARMOR", "hair", "skin")
        string surfaceHint = "";
        string[] surfaceKeywords = { "body", "armor", "hair", "skin", "head", "face", "hand", "cloth", "eye", "mouth" };
        foreach (var keyword in surfaceKeywords)
        {
            if (texFileName.Contains(keyword))
            {
                surfaceHint = keyword;
                break;
            }
        }

        GD.Print($"[Texture] Applying '{texFileName}' (hint: '{surfaceHint}') type: {param}");

        // Find mesh instances and apply/override material
        void ApplyRecursive(Node node)
        {
            if (node is MeshInstance3D mesh)
            {
                string meshName = mesh.Name.ToString().ToLower();
                string meshPath = mesh.GetPath().ToString();

                // Check if mesh name is generic (Group#### pattern common from FBX imports)
                bool isGenericMeshName = meshName.Contains("group") || meshName.StartsWith("@") ||
                                         System.Text.RegularExpressions.Regex.IsMatch(meshName, @"^models_.*\d+$");

                // If mesh names are generic, apply to ALL meshes (best effort)
                bool shouldApply = isGenericMeshName;

                // Otherwise, use hint matching
                if (!shouldApply)
                {
                    shouldApply = string.IsNullOrEmpty(surfaceHint) ||
                                  meshName.Contains(surfaceHint) ||
                                  (surfaceHint == "body" && (meshName.Contains("skin") || meshName.Contains("mesh"))) ||
                                  (surfaceHint == "skin" && meshName.Contains("body"));
                }

                if (shouldApply)
                {
                    int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
                    if (surfaceCount == 0 && mesh.Mesh != null) surfaceCount = mesh.Mesh.GetSurfaceCount();

                    for (int i = 0; i < surfaceCount; i++)
                    {
                        // Create unique key for this mesh surface + texture param
                        string trackingKey = $"{meshPath}_{i}_{param}";

                        // Skip if we've already applied a texture of this type to this surface
                        if (_appliedTextures.Contains(trackingKey))
                            continue;

                        _appliedTextures.Add(trackingKey);

                        StandardMaterial3D mat = mesh.GetSurfaceOverrideMaterial(i) as StandardMaterial3D;
                        if (mat == null)
                        {
                            // Create new material, copying settings from original if it exists
                            mat = new StandardMaterial3D();

                            var originalMat = mesh.Mesh?.SurfaceGetMaterial(i) as StandardMaterial3D;
                            if (originalMat != null)
                            {
                                mat.Transparency = originalMat.Transparency;
                                mat.CullMode = originalMat.CullMode;
                                mat.AlphaScissorThreshold = originalMat.AlphaScissorThreshold;
                            }

                            // Disable vertex color since we're applying textures
                            mat.VertexColorUseAsAlbedo = false;

                            mesh.SetSurfaceOverrideMaterial(i, mat);
                        }

                        mat.SetTexture(param, texture);
                        if (param == StandardMaterial3D.TextureParam.Normal) mat.NormalEnabled = true;
                        if (param == StandardMaterial3D.TextureParam.AmbientOcclusion) mat.AOEnabled = true;
                        if (param == StandardMaterial3D.TextureParam.Emission) mat.EmissionEnabled = true;
                    }
                }
            }
            foreach (Node child in node.GetChildren()) ApplyRecursive(child);
        }
        ApplyRecursive(root);
    }

    private StandardMaterial3D _originalMaterial;

    private void CreateGizmo()
    {
        _gizmoRing = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 1.8f;
        torus.OuterRadius = 2.0f;
        _gizmoRing.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 1, 1, 0.5f); // Cyan transparent
        mat.EmissionEnabled = true;
        mat.Emission = Colors.Cyan;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.NoDepthTest = true; // Always visible on top
        _gizmoRing.MaterialOverride = mat;

        _gizmoRing.Visible = false;
        AddChild(_gizmoRing);
    }

    private MeshInstance3D FindMeshRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D m) return m;
            var found = FindMeshRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    public void OnHover(bool isHovered)
    {
        if (IsSelected) return; // Selection takes priority visually
        UpdateVisuals(isHovered ? new Color(1.5f, 1.5f, 1.5f) : Colors.White);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateVisuals(selected ? Colors.Cyan : Colors.White, selected);

        if (_gizmoRing != null) _gizmoRing.Visible = selected;
    }

    private void UpdateVisuals(Color color, bool isSelected = false)
    {
        if (_mesh == null) return;

        // Visual Reset
        if (!isSelected && color == Colors.White)
        {
            _mesh.MaterialOverride = null;
            return;
        }

        // Apply Highlight/Selection
        if (_originalMaterial != null)
        {
            var uniqueMat = (StandardMaterial3D)_originalMaterial.Duplicate();
            uniqueMat.AlbedoColor = color;

            if (isSelected)
            {
                uniqueMat.EmissionEnabled = true;
                uniqueMat.Emission = color;
                uniqueMat.EmissionEnergyMultiplier = 2.0f;
            }

            _mesh.MaterialOverride = uniqueMat;
        }
    }

    public override void _Process(double delta)
    {
        // Pulse removed.
    }
}
