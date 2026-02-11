using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

[Tool]
public partial class InteractableObject : Node3D
{
    [Export] public string ObjectName = "Object";
    [Export] public bool IsMovable = true;
    [Export] public bool IsDeletable = true;
    [Export] public bool IsTargetable = false;
    [Export] public MobaTeam Team = MobaTeam.None;
    [Export] public string ModelPath = "";

    [ExportGroup("Targeting Visualization")]
    [Export] public float SelectionRingRadius = 0.8f;
    [Export] public float SelectionRingTolerance = 0.1f;

    [ExportGroup("Collision")]
    [Export] public bool AutoGenerateCollision = true;
    [Export] public bool RebuildCollisionTrigger { get => false; set { if (value) CallDeferred(nameof(AddDynamicCollision)); } }
    public string ScenePath { get; set; } = "";
    public bool IsSelected { get; private set; } = false;

    private MeshInstance3D _mesh;
    private MeshInstance3D _gizmoRing;
    protected StandardMaterial3D _originalMaterial;

    public virtual void OnInteract(PlayerController player) { }
    public virtual string GetInteractionPrompt() { return ""; }
    public virtual void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal, Node attacker = null) { }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            // In editor, we only auto-generate if requested and missing
            if (AutoGenerateCollision)
            {
                var existing = GetNodeOrNull<StaticBody3D>("StaticBody3D");
                if (existing == null) AddDynamicCollision();
            }
            return;
        }

        if (IsTargetable) AddToGroup("targetables");

        _mesh = FindMeshRecursive(this);
        CreateGizmo();

        if (_mesh != null)
        {
            _originalMaterial = _mesh.GetActiveMaterial(0) as StandardMaterial3D;
        }

        if (!string.IsNullOrEmpty(ModelPath))
        {
            TryApplyTexturesToModel(this, ModelPath);
        }

        if (AutoGenerateCollision)
        {
            // Skip auto-collision if this node is already a physics body or has a manual shape
            bool hasNativeCollision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D") != null ||
                                     GetNodeOrNull<StaticBody3D>("StaticBody3D") != null ||
                                     IsClass("CollisionObject3D");

            if (!hasNativeCollision)
            {
                AddDynamicCollision();
            }
        }
    }

    public void OnHover(bool isHovered)
    {
        if (IsSelected) return;
        UpdateVisuals(isHovered ? new Color(1.5f, 1.5f, 1.5f) : Colors.White);
    }

    public void SetSelected(bool selected, bool isLocked = false)
    {
        IsSelected = selected;
        Color ringColor = isLocked ? Colors.Cyan : new Color(1, 1, 1, 0.8f);
        UpdateVisuals(selected ? ringColor : Colors.White, selected);

        if (_gizmoRing != null)
        {
            _gizmoRing.Visible = selected;
            if (selected && _gizmoRing.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = ringColor;
            }
        }
    }

    public void UpdateVisuals(Color color, bool isSelected = false)
    {
        if (_mesh == null) return;
        if (!isSelected && color == Colors.White)
        {
            _mesh.MaterialOverride = null;
            return;
        }

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

    public void AddDynamicCollision()
    {
        // Clean up old generated body if it exists
        var existing = GetNodeOrNull<StaticBody3D>("StaticBody3D");
        if (existing != null)
        {
            existing.QueueFree();
            // In editor, we need to handle this immediately if possible, or wait for next frame
        }

        Aabb combinedAabb = new Aabb();
        bool hasAabb = false;

        void Encapsulate(Node node, Transform3D localTransform)
        {
            Transform3D currentTransform = localTransform;
            if (node is Node3D node3D) currentTransform = localTransform * node3D.Transform;

            if (node is VisualInstance3D vi && vi.Visible)
            {
                Aabb transformedAabb = currentTransform * vi.GetAabb();
                if (!hasAabb) { combinedAabb = transformedAabb; hasAabb = true; }
                else combinedAabb = combinedAabb.Merge(transformedAabb);
            }
            foreach (Node child in node.GetChildren()) Encapsulate(child, currentTransform);
        }

        foreach (Node child in GetChildren())
        {
            if (child.Name == "StaticBody3D") continue; // Don't include the collider in its own bounds
            Encapsulate(child, Transform3D.Identity);
        }

        if (hasAabb)
        {
            var staticBody = new StaticBody3D();
            staticBody.Name = "StaticBody3D";
            AddChild(staticBody);

            var colShape = new CollisionShape3D();
            colShape.Name = "CollisionShape3D";
            staticBody.AddChild(colShape);

            var box = new BoxShape3D();
            if (combinedAabb.Size.Length() < 0.1f) combinedAabb.Size = new Vector3(1, 1, 1);
            box.Size = combinedAabb.Size;
            colShape.Shape = box;
            colShape.Position = combinedAabb.GetCenter();

            // CRITICAL: Set owner AFTER adding to tree so they persist in the editor
            if (Engine.IsEditorHint() && GetTree() != null)
            {
                var root = GetTree().EditedSceneRoot;
                staticBody.Owner = root;
                colShape.Owner = root;
            }

            GD.Print($"[InteractableObject] Generated collision for {ObjectName} ({combinedAabb.Size})");
        }
    }

    private void TryApplyTexturesToModel(Node model, string modelPath)
    {
        string dirPath = modelPath.Substring(0, modelPath.LastIndexOf("/") + 1);
        string baseName = System.IO.Path.GetFileNameWithoutExtension(modelPath).ToLower();
        ScanDirectoryForTextures(model, dirPath, baseName);
    }

    private void ScanDirectoryForTextures(Node model, string dirPath, string baseName)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return;
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir())
            {
                string lowerFile = fileName.ToLower();
                if (lowerFile.EndsWith(".tga") || lowerFile.EndsWith(".png") || lowerFile.EndsWith(".jpg"))
                {
                    if (lowerFile.Contains("color") || lowerFile.Contains("albedo"))
                        ApplySingleTexture(model, dirPath + fileName, StandardMaterial3D.TextureParam.Albedo);
                }
            }
            fileName = dir.GetNext();
        }
    }

    private void ApplySingleTexture(Node root, string texPath, StandardMaterial3D.TextureParam param)
    {
        var texture = GD.Load<Texture2D>(texPath);
        if (texture == null) return;
        void ApplyRecursive(Node node)
        {
            if (node is MeshInstance3D mesh)
            {
                StandardMaterial3D mat = mesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
                if (mat == null) { mat = new StandardMaterial3D(); mesh.SetSurfaceOverrideMaterial(0, mat); }
                mat.SetTexture(param, texture);
            }
            foreach (Node child in node.GetChildren()) ApplyRecursive(child);
        }
        ApplyRecursive(root);
    }

    private void CreateGizmo()
    {
        _gizmoRing = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = SelectionRingRadius;
        torus.OuterRadius = SelectionRingRadius + SelectionRingTolerance;
        _gizmoRing.Mesh = torus;
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1, 1, 1, 0.8f); // Default to white/soft
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // Make it glow properly
        _gizmoRing.MaterialOverride = mat;
        _gizmoRing.Visible = false;
        AddChild(_gizmoRing);
    }

    public virtual Vector3 GetTargetCenter()
    {
        // Default to a 1.2m offset for humanoids if no mesh is found
        if (_mesh == null) return GlobalPosition + new Vector3(0, 1.2f, 0);

        // Try to find the center of the visual AABB
        Aabb aabb = _mesh.GetAabb();
        Vector3 center = aabb.GetCenter();

        // Return global position of the center
        return _mesh.ToGlobal(center);
    }

    private float _flashTimer = 0f;

    public void FlashRed(float duration = 0.2f)
    {
        _flashTimer = duration;
        UpdateVisuals(Colors.Red, true);
    }

    public override void _Process(double delta)
    {
        if (_flashTimer > 0)
        {
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0)
            {
                _flashTimer = 0;
                // Restore visuals based on current selection state
                SetSelected(IsSelected);
            }
        }
    }

    protected MeshInstance3D FindMeshRecursive(Node node)
    {
        if (node is MeshInstance3D m) return m;
        foreach (Node child in node.GetChildren())
        {
            var found = FindMeshRecursive(child);
            if (found != null) return found;
        }
        return null;
    }
}
