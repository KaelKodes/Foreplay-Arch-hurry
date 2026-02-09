using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager
{
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
        var registry = CharacterRegistry.Instance;
        var model = registry?.GetModel(_currentModelId);
        bool isCustom = model?.IsCustomSkeleton ?? false;

        if (isCustom)
        {
            if (_meleeModel != null) _meleeModel.Visible = false;
            if (_archeryModel != null) _archeryModel.Visible = false;
            if (_currentCustomModel != null) _currentCustomModel.Visible = true;
            if (_animTree != null) _animTree.Active = false;

            // Apply Dynamic Visibility (Custom Mesh Weapons)
            UpdateCustomWeaponVisibility(isArcheryMode);
            return;
        }

        // --- Shared Skeleton Logic (Ranger, Warrior, Cleric) ---
        bool isRanger = _currentModelId.ToLower() == "ranger" || _currentModelId.ToLower() == "erika";

        // Check for RPG mode persistent bow
        bool forceBow = isRanger && ToolManager.Instance != null && ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;

        if (_meleeModel != null) _meleeModel.Visible = !isArcheryMode;
        if (_archeryModel != null) _archeryModel.Visible = isArcheryMode;
        if (_currentCustomModel != null) _currentCustomModel.Visible = false;

        if (isArcheryMode || forceBow)
        {
            if (isArcheryMode && _animTree != null && _archeryAnimPlayer != null)
            {
                _animTree.Active = true;
                _animTree.RootNode = _animTree.GetPathTo(_archeryModel);
                _animTree.SetAnimationPlayer(_archeryAnimPlayer.GetPath());
            }

            // If we are in Melee but forced to show bow (RPG mode), attach to Melee model
            if (forceBow && !isArcheryMode)
            {
                SetupStandaloneBow(_meleeModel);
            }
            else if (isArcheryMode && !isRanger)
            {
                SetupStandaloneBow(_archeryModel);
            }
        }
        else
        {
            if (_animTree != null && _meleeAnimPlayer != null)
            {
                _animTree.Active = true;
                _animTree.RootNode = _animTree.GetPathTo(_meleeModel);
                _animTree.SetAnimationPlayer(_meleeAnimPlayer.GetPath());
            }

            RemoveStandaloneBow(_meleeModel);
            RemoveStandaloneBow(_archeryModel);
        }
    }

    /// <summary>
    /// Sets up a standalone sword for non-Erika characters by loading Sword.tscn.
    /// </summary>
    public void SetupStandaloneSword(Node3D targetModel)
    {
        if (targetModel == null) return;
        if (_currentModelId == "erika") return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(targetModel);
        if (skeleton == null) return;

        if (skeleton.HasNode("StandaloneSwordAttachment")) return;

        var swordScenePath = "res://Scenes/Entities/Sword.tscn";
        if (!ResourceLoader.Exists(swordScenePath)) return;

        var swordScene = GD.Load<PackedScene>(swordScenePath);
        if (swordScene == null) return;

        // Try to find a good bone for the sword
        string boneName = "mixamorig_RightHand";
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
        {
            if (skeleton.GetBoneName(i).Contains("RightHand"))
            {
                boneName = skeleton.GetBoneName(i);
                break;
            }
        }

        var boneAttachment = new BoneAttachment3D();
        boneAttachment.Name = "StandaloneSwordAttachment";
        boneAttachment.BoneName = boneName;
        skeleton.AddChild(boneAttachment);

        var swordInstance = swordScene.Instantiate<Node3D>();
        boneAttachment.AddChild(swordInstance);

        // Adjust for typical Mixamo weapon positioning
        swordInstance.Position = new Vector3(0, 0, 0);
        swordInstance.RotationDegrees = new Vector3(-90, 0, 0);

        // Fix scaling for models that are scaled significantly
        var skelScale = skeleton.GlobalTransform.Basis.Scale;
        if (skelScale.X != 0)
        {
            swordInstance.Scale = Vector3.One / skelScale;
        }
        else
        {
            swordInstance.Scale = Vector3.One;
        }

        if (swordInstance is SwordController sc)
        {
            var melee = _player.GetNodeOrNull<MeleeSystem>("MeleeSystem");
            if (melee != null) sc.ConnectToMeleeSystem(melee);
        }

        GD.Print($"[CharacterModelManager] Attached standalone Sword.tscn to {boneName}");
    }

    public void RemoveStandaloneSword(Node3D targetModel)
    {
        if (targetModel == null) return;
        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(targetModel);
        if (skeleton == null) return;

        var attachment = skeleton.GetNodeOrNull("StandaloneSwordAttachment");
        if (attachment != null) attachment.QueueFree();
    }

    private void UpdateCustomWeaponVisibility(bool isArcheryMode)
    {
        var registry = CharacterRegistry.Instance;
        var model = registry?.GetModel(_currentModelId);
        if (model == null || _currentCustomModel == null) return;

        bool hasCustomSword = HasVisibleMeshInCategory(model, CharacterConfig.MeshConfig.Categories.WeaponMain);
        bool hasCustomBow = HasVisibleMeshInCategory(model, CharacterConfig.MeshConfig.Categories.WeaponBow);

        if (isArcheryMode)
        {
            // --- BOW ---
            if (hasCustomBow)
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, true);
                RemoveStandaloneBow(_currentCustomModel);
            }
            else
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, false);
                SetupStandaloneBow(_currentCustomModel);
            }

            // --- SWORD ---
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, false);
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, false);
            RemoveStandaloneSword(_currentCustomModel);
        }
        else // Melee Mode
        {
            // --- BOW ---
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, false);
            RemoveStandaloneBow(_currentCustomModel);

            // --- SWORD ---
            bool showSword = (_player.CurrentState == PlayerState.CombatMelee);

            if (hasCustomSword)
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, showSword);
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, showSword);
                RemoveStandaloneSword(_currentCustomModel);
            }
            else
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, false);
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, false);

                if (showSword) SetupStandaloneSword(_currentCustomModel);
                else RemoveStandaloneSword(_currentCustomModel);
            }
        }
    }

    private bool HasVisibleMeshInCategory(CharacterRegistry.CharacterModel model, string category)
    {
        if (model.Meshes == null) return false;
        foreach (var mesh in model.Meshes.Values)
        {
            if (mesh.Category == category && mesh.IsVisible) return true;
        }
        return false;
    }

    private void SetCategoryVisibility(CharacterRegistry.CharacterModel model, string category, bool visible)
    {
        if (model.Meshes == null || _currentCustomModel == null) return;
        foreach (var kvp in model.Meshes)
        {
            if (kvp.Value.Category == category && kvp.Value.IsVisible)
            {
                var node = _currentCustomModel.FindChild(kvp.Key, true, false) as Node3D;
                if (node != null) node.Visible = visible;
            }
        }
    }
}
