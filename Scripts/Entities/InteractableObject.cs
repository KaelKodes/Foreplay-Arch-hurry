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
    [Export] public MobaTeam Team = MobaTeam.None;
    [Export] public string ModelPath = "";
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

        // Skip auto-collision if this node is already a physics body or has a manual shape
        bool hasNativeCollision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D") != null ||
                                 GetNodeOrNull<StaticBody3D>("StaticBody3D") != null ||
                                 IsClass("CollisionObject3D");

        if (!hasNativeCollision)
        {
            AddDynamicCollision();
        }
    }

    public void OnHover(bool isHovered)
    {
        if (IsSelected) return;
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
        Aabb combinedAabb = new Aabb();
        bool hasAabb = false;

        void Encapsulate(Node node, Transform3D parentTransform)
        {
            Transform3D currentTransform = parentTransform;
            if (node is Node3D node3D) currentTransform = parentTransform * node3D.Transform;

            // ONLY include visible meshes in the dynamic collision box
            if (node is VisualInstance3D vi && vi.Visible)
            {
                Aabb transformedAabb = currentTransform * vi.GetAabb();
                if (!hasAabb) { combinedAabb = transformedAabb; hasAabb = true; }
                else combinedAabb = combinedAabb.Merge(transformedAabb);
            }
            foreach (Node child in node.GetChildren()) Encapsulate(child, currentTransform);
        }

        foreach (Node child in GetChildren()) Encapsulate(child, Transform3D.Identity);

        if (hasAabb)
        {
            var staticBody = new StaticBody3D();
            staticBody.Name = "StaticBody3D";
            var colShape = new CollisionShape3D();
            var box = new BoxShape3D();
            if (combinedAabb.Size.Length() < 0.1f) combinedAabb.Size = new Vector3(1, 1, 1);
            box.Size = combinedAabb.Size;
            colShape.Shape = box;
            colShape.Position = combinedAabb.GetCenter();
            staticBody.AddChild(colShape);
            AddChild(staticBody);
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
        torus.InnerRadius = 1.8f; torus.OuterRadius = 2.0f;
        _gizmoRing.Mesh = torus;
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 1, 1, 0.5f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _gizmoRing.MaterialOverride = mat;
        _gizmoRing.Visible = false;
        AddChild(_gizmoRing);
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
