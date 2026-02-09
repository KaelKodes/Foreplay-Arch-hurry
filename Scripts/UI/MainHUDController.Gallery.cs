using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class MainHUDController
{
    private void OnResizeHandleGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _isResizing = mb.Pressed;
                if (_isResizing)
                {
                    _resizeStartPos = GetViewport().GetMousePosition();
                    _resizeStartSize = _objectGallery.Size;
                }
            }
        }
        else if (@event is InputEventMouseMotion mm && _isResizing)
        {
            Vector2 currentMousePos = GetViewport().GetMousePosition();
            Vector2 diff = currentMousePos - _resizeStartPos;
            Vector2 newSize = _resizeStartSize + diff;

            newSize.X = Mathf.Max(newSize.X, 400);
            newSize.Y = Mathf.Max(newSize.Y, 300);

            _objectGallery.Size = newSize;
        }
    }

    private void SetGalleryExpanded(bool expanded)
    {
        _objectGallery.Visible = expanded;
    }

    private void InitializeCategories()
    {
        CreateMainCategoryButtons();
        SelectMainCategory("Nature");
    }

    private void CreateMainCategoryButtons()
    {
        foreach (Node child in _mainCategoryContainer.GetChildren()) child.QueueFree();

        string[] categories = ObjectGalleryData.MainCategories;
        foreach (var cat in categories)
        {
            var btn = new Button { Text = cat };
            btn.CustomMinimumSize = new Vector2(90, 35);
            btn.Pressed += () => SelectMainCategory(cat);
            _mainCategoryContainer.AddChild(btn);
        }
    }

    private void SelectMainCategory(string mainCategory)
    {
        _currentMainCategory = mainCategory;
        UpdateSubCategoryButtons(mainCategory);

        var firstSub = _allAssets.Find(a => a.MainCategory == mainCategory).SubCategory;
        if (string.IsNullOrEmpty(firstSub)) firstSub = "General";

        SelectSubCategory(firstSub);
    }

    private void UpdateSubCategoryButtons(string mainCategory)
    {
        foreach (Node child in _subCategoryContainer.GetChildren()) child.QueueFree();

        var subCats = _allAssets
            .FindAll(a => a.MainCategory == mainCategory)
            .Select(a => a.SubCategory)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (subCats.Count == 0) return;

        foreach (var sub in subCats)
        {
            var btn = new Button { Text = sub };
            btn.CustomMinimumSize = new Vector2(80, 30);
            btn.Pressed += () => SelectSubCategory(sub);
            _subCategoryContainer.AddChild(btn);
        }
    }

    private void SelectSubCategory(string subCategory)
    {
        _currentSubCategory = subCategory;
        PopulateGallery();
    }

    private void PopulateGallery()
    {
        foreach (Node child in _objectGrid.GetChildren()) child.QueueFree();

        var filtered = _allAssets.FindAll(a => a.MainCategory == _currentMainCategory && a.SubCategory == _currentSubCategory);
        foreach (var asset in filtered)
        {
            var btn = new Button { Text = asset.Name };
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.CustomMinimumSize = new Vector2(120, 40);
            btn.Pressed += () => SelectObjectToPlace(asset.Name);
            _objectGrid.AddChild(btn);
        }
    }

    private void SelectObjectToPlace(string objectId)
    {
        GD.Print($"Selected Object to Place: {objectId}");

        string scenePath = "";
        bool isModelFile = false;

        var asset = _allAssets.Find(a => a.Name == objectId);
        if (!string.IsNullOrEmpty(asset.Path))
        {
            scenePath = asset.Path;
            isModelFile = asset.Path.EndsWith(".gltf") || asset.Path.EndsWith(".glb") || asset.Path.EndsWith(".fbx");
        }
        else
        {
            if (asset.SubCategory == "Combat") scenePath = "res://Scenes/Entities/Monsters.tscn";
            else
            {
                switch (objectId)
                {
                    case "DistanceSign":
                    case "TeePin":
                    case "Pin": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
                    case "CourseMap": scenePath = "res://Scenes/Environment/CourseMapSign.tscn"; break;
                }
            }
        }

        InteractableObject obj = null;

        if (isModelFile)
        {
            var model = GD.Load<PackedScene>(scenePath).Instantiate();
            obj = new InteractableObject();
            obj.Name = objectId;
            obj.ObjectName = objectId;
            obj.ModelPath = scenePath;
            obj.AddChild(model);
            obj.AddDynamicCollision();
        }
        else
        {
            var scene = GD.Load<PackedScene>(scenePath);
            var instance = scene.Instantiate();

            if (instance is not InteractableObject interactable)
            {
                var wrapper = new InteractableObject();
                wrapper.Name = objectId;
                wrapper.ObjectName = objectId;
                wrapper.ModelPath = scenePath;
                wrapper.ScenePath = scenePath;
                wrapper.AddChild(instance);
                wrapper.AddDynamicCollision();
                obj = wrapper;
            }
            else
            {
                obj = interactable;
                obj.ObjectName = objectId;
                obj.ScenePath = scenePath;
                if (string.IsNullOrEmpty(obj.ModelPath)) obj.ModelPath = scenePath;
                if (obj is Monsters monster) monster.Species = ObjectGalleryData.ResolveMonsterSpecies(objectId);
            }
        }

        if (obj != null && _archerySystem?.ObjectPlacer != null)
        {
            _archerySystem.ObjectPlacer.SpawnAndPlace(obj);
        }
    }
}
