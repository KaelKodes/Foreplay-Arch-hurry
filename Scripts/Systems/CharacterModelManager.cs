using Godot;

namespace Archery;

/// <summary>
/// Manages character model swapping, mesh updates, and bow attachment for PlayerController.
/// Extracted from PlayerController to reduce file size.
/// </summary>
public partial class CharacterModelManager : Node
{
    private PlayerController _player;
    private Node3D _meleeModel;
    private Node3D _archeryModel;
    private AnimationTree _animTree;
    private AnimationPlayer _meleeAnimPlayer;
    private AnimationPlayer _archeryAnimPlayer;
    private Mesh _cachedBowMesh;
    private string _currentModelId = "erika";

    public string CurrentModelId => _currentModelId;

    public void Initialize(PlayerController player, Node3D meleeModel, Node3D archeryModel,
                          AnimationTree animTree, AnimationPlayer meleeAnimPlayer, AnimationPlayer archeryAnimPlayer)
    {
        _player = player;
        _meleeModel = meleeModel;
        _archeryModel = archeryModel;
        _animTree = animTree;
        _meleeAnimPlayer = meleeAnimPlayer;
        _archeryAnimPlayer = archeryAnimPlayer;

        CacheBowMesh();
    }

    /// <summary>
    /// Cycles to the next available character model.
    /// </summary>
    public void CycleCharacterModel()
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null)
        {
            GD.PrintErr("[CharacterModelManager] CharacterRegistry not found!");
            return;
        }

        var nextModel = registry.GetNextModel(_currentModelId);
        if (nextModel != null && nextModel.Id != _currentModelId)
        {
            SetCharacterModel(nextModel.Id);
            GD.Print($"[CharacterModelManager] Switched to model: {nextModel.DisplayName}");
        }
    }

    /// <summary>
    /// Sets the character model by ID.
    /// </summary>
    public void SetCharacterModel(string modelId)
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) return;

        var model = registry.GetModel(modelId);
        if (model == null)
        {
            GD.PrintErr($"[CharacterModelManager] Model not found: {modelId}");
            return;
        }

        _currentModelId = modelId;

        // Swap the mesh on the appropriate model node
        SwapModelMesh(_meleeModel, model.MeleeScenePath);
        SwapModelMesh(_archeryModel, model.ArcheryScenePath);

        GD.Print($"[CharacterModelManager] Model set to: {model.DisplayName}");
    }

    /// <summary>
    /// Swaps the mesh on a model node by loading the new FBX and copying mesh instances.
    /// </summary>
    private void SwapModelMesh(Node3D targetModel, string fbxPath)
    {
        if (targetModel == null) return;

        if (!ResourceLoader.Exists(fbxPath))
        {
            GD.PrintErr($"[CharacterModelManager] Model FBX not found: {fbxPath}");
            return;
        }

        var fbxScene = GD.Load<PackedScene>(fbxPath);
        if (fbxScene == null)
        {
            GD.PrintErr($"[CharacterModelManager] Could not load FBX: {fbxPath}");
            return;
        }

        var newModelInstance = fbxScene.Instantiate<Node3D>();
        var targetSkeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        var newSkeleton = newModelInstance.GetNodeOrNull<Skeleton3D>("Skeleton3D");

        if (targetSkeleton == null || newSkeleton == null)
        {
            GD.PrintErr("[CharacterModelManager] Could not find skeletons for mesh swap");
            newModelInstance.QueueFree();
            return;
        }

        // Remove old mesh instances from target skeleton (but keep BoneAttachments)
        foreach (var child in targetSkeleton.GetChildren())
        {
            if (child is MeshInstance3D oldMesh)
            {
                oldMesh.QueueFree();
            }
        }

        // Copy mesh instances from new skeleton to target skeleton
        foreach (var child in newSkeleton.GetChildren())
        {
            if (child is MeshInstance3D newMesh)
            {
                newMesh.Owner = null;
                newSkeleton.RemoveChild(newMesh);
                targetSkeleton.AddChild(newMesh);
                FixMeshCulling(newMesh);
            }
        }

        newModelInstance.QueueFree();
        GD.Print($"[CharacterModelManager] Swapped mesh from: {fbxPath}");
    }

    /// <summary>
    /// Disables backface culling on all materials of a mesh to prevent "see-through" issues.
    /// </summary>
    private void FixMeshCulling(MeshInstance3D mesh)
    {
        if (mesh == null) return;

        int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
        if (surfaceCount == 0 && mesh.Mesh != null)
        {
            surfaceCount = mesh.Mesh.GetSurfaceCount();
        }

        for (int i = 0; i < surfaceCount; i++)
        {
            var mat = mesh.GetSurfaceOverrideMaterial(i) as BaseMaterial3D;
            if (mat == null && mesh.Mesh != null)
            {
                mat = mesh.Mesh.SurfaceGetMaterial(i) as BaseMaterial3D;
            }

            if (mat != null)
            {
                var uniqueMat = (BaseMaterial3D)mat.Duplicate();
                uniqueMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                mesh.SetSurfaceOverrideMaterial(i, uniqueMat);
            }
        }
    }

    /// <summary>
    /// Caches the bow mesh from ErikaBow skeleton at startup before any mesh swaps.
    /// </summary>
    private void CacheBowMesh()
    {
        if (_archeryModel == null) return;

        var erikaSkeleton = _archeryModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (erikaSkeleton == null) return;

        foreach (var child in erikaSkeleton.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                string meshName = mesh.Name.ToString();
                if (meshName.Contains("Bow") && !meshName.Contains("Arrow"))
                {
                    _cachedBowMesh = mesh.Mesh;
                    GD.Print($"[CharacterModelManager] Cached bow mesh: {meshName}");
                    return;
                }
            }
        }

        GD.PrintErr("[CharacterModelManager] Could not find bow mesh to cache!");
    }

    /// <summary>
    /// Sets up a standalone bow for non-Erika characters by loading Bow.tscn.
    /// </summary>
    public void SetupStandaloneBow(Node3D targetModel)
    {
        if (targetModel == null) return;
        if (_currentModelId == "erika") return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (skeleton == null) return;

        if (skeleton.HasNode("StandaloneBowAttachment")) return;

        var bowScenePath = "res://Scenes/Weapons/Bow.tscn";
        if (!ResourceLoader.Exists(bowScenePath))
        {
            GD.PrintErr($"[CharacterModelManager] Bow scene not found: {bowScenePath}");
            return;
        }

        var bowScene = GD.Load<PackedScene>(bowScenePath);
        if (bowScene == null)
        {
            GD.PrintErr("[CharacterModelManager] Could not load Bow scene");
            return;
        }

        var boneAttachment = new BoneAttachment3D();
        boneAttachment.Name = "StandaloneBowAttachment";
        boneAttachment.BoneName = "mixamorig_LeftHand";
        skeleton.AddChild(boneAttachment);

        var bowInstance = bowScene.Instantiate<Node3D>();
        boneAttachment.AddChild(bowInstance);
        bowInstance.Position = new Vector3(-0.75f, -1.39f, 0.06f);
        bowInstance.RotationDegrees = Vector3.Zero;

        GD.Print($"[CharacterModelManager] Attached standalone Bow.tscn to character");
    }

    /// <summary>
    /// Removes the standalone bow if switching back to Erika.
    /// </summary>
    public void RemoveStandaloneBow(Node3D targetModel)
    {
        if (targetModel == null) return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (skeleton == null) return;

        var bowAttachment = skeleton.GetNodeOrNull("StandaloneBowAttachment");
        if (bowAttachment != null)
        {
            bowAttachment.QueueFree();
            GD.Print("[CharacterModelManager] Removed standalone bow");
        }
    }

    /// <summary>
    /// Updates model visibility based on current tool/mode.
    /// </summary>
    public void SetModelMode(bool isArcheryMode)
    {
        if (_meleeModel != null) _meleeModel.Visible = !isArcheryMode;
        if (_archeryModel != null) _archeryModel.Visible = isArcheryMode;

        if (isArcheryMode)
        {
            if (_animTree != null && _archeryAnimPlayer != null)
            {
                _animTree.RootNode = _animTree.GetPathTo(_archeryModel);
                _animTree.SetAnimationPlayer(_archeryAnimPlayer.GetPath());
            }

            if (_currentModelId != "erika")
            {
                SetupStandaloneBow(_archeryModel);
            }
        }
        else
        {
            if (_animTree != null && _meleeAnimPlayer != null)
            {
                _animTree.RootNode = _animTree.GetPathTo(_meleeModel);
                _animTree.SetAnimationPlayer(_meleeAnimPlayer.GetPath());
            }

            RemoveStandaloneBow(_archeryModel);
        }
    }
}
