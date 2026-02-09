using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

/// <summary>
/// Developer utility to preview character animations and test mappings.
/// Accessed via the Character Import button on the Main Menu.
/// </summary>
public partial class AnimationPreviewController : Control
{
    private CharacterRegistry.CharacterModel _currentModel;
    private Node3D _modelInstance;
    private AnimationPlayer _animPlayer;
    private Skeleton3D _skeleton;

    // UI References
    private ItemList _modelList;
    private GridContainer _animGrid;
    private Label _modelLabel;
    private SubViewport _previewViewport;
    private Camera3D _camera;

    // Mapping State
    private string _selectedAnimInGrid = "";
    private readonly string[] _targetSlots = {
        "MeleeAttack1", "PowerSlash", "SlashCombo",
        "Kick", "MeleeAttack3", "PowerUp", "Casting",
        "Death", "Block", "Impact"
    };

    public override void _Ready()
    {
        SetupUI();
        PopulateModelList();

        // Default to Warrior if found
        SelectModel("Warrior");
    }

    private void SetupUI()
    {
        _modelList = GetNode<ItemList>("%ModelList");
        _animGrid = GetNode<GridContainer>("%AnimGrid");
        _modelLabel = GetNode<Label>("%ModelLabel");
        _previewViewport = GetNode<SubViewport>("%PreviewViewport");
        _camera = GetNode<Camera3D>("%PreviewCamera");

        _modelList.ItemSelected += (idx) => SelectModel(_modelList.GetItemText((int)idx));

        GetNode<Button>("%BackBtn").Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");

        // Setup Slot Buttons
        var slotContainer = GetNode<VBoxContainer>("%SlotContainer");
        foreach (var slot in _targetSlots)
        {
            var hBox = new HBoxContainer();
            slotContainer.AddChild(hBox);

            var label = new Label { Text = slot + ":", CustomMinimumSize = new Vector2(100, 0) };
            hBox.AddChild(label);

            var valueLabel = new Label { Name = "Value_" + slot, Text = "[Unmapped]", Modulate = new Color(0.7f, 0.7f, 0.7f) };
            hBox.AddChild(valueLabel);

            var assignBtn = new Button { Text = "Set", CustomMinimumSize = new Vector2(50, 0) };
            assignBtn.Pressed += () => AssignToSlot(slot, valueLabel);
            hBox.AddChild(assignBtn);
        }

        GetNode<Button>("%SaveMappingsBtn").Pressed += OnExportPressed;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var container = GetNode<Node3D>("%ModelContainer");
            if (container != null)
            {
                container.RotateY(-mm.Relative.X * 0.01f);
            }
        }
    }

    private void PopulateModelList()
    {
        _modelList.Clear();
        foreach (var model in CharacterRegistry.Instance.AvailableModels)
        {
            _modelList.AddItem(model.Id);
        }
    }

    private void SelectModel(string modelId)
    {
        var model = CharacterRegistry.Instance.GetModel(modelId);
        if (model == null) return;

        _currentModel = model;
        _modelLabel.Text = $"Preview: {model.DisplayName} ({(model.IsCustomSkeleton ? "Custom Skeleton" : "Shared Skeleton")})";

        LoadModel(model);
        UpdateExistingMappings();
    }

    private void LoadModel(CharacterRegistry.CharacterModel model)
    {
        // Cleanup old
        if (_modelInstance != null)
        {
            _modelInstance.QueueFree();
            _modelInstance = null;
        }

        _animDisplayNames.Clear();

        string path = model.MeleeScenePath;
        if (!ResourceLoader.Exists(path)) return;

        var scn = GD.Load<PackedScene>(path);
        _modelInstance = scn.Instantiate<Node3D>();
        var container = GetNode<Node3D>("%ModelContainer");
        container.AddChild(_modelInstance);

        // Find components
        _animPlayer = FindAnimationPlayerRecursive(_modelInstance);
        _skeleton = FindSkeletonRecursive(_modelInstance);

        // Scan for external animations
        LoadExternalAnimations(model);

        PopulateAnimationGrid();
    }

    private void LoadExternalAnimations(CharacterRegistry.CharacterModel model)
    {
        if (_animPlayer == null || _skeleton == null) return;

        string baseDir = model.MeleeScenePath.GetBaseDir();
        using var dir = DirAccess.Open(baseDir);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && (fileName.EndsWith(".fbx") || fileName.EndsWith(".glb") || fileName.EndsWith(".dae")))
            {
                string fullPath = baseDir + "/" + fileName;
                // Skip the base model itself
                if (fullPath == model.MeleeScenePath) { fileName = dir.GetNext(); continue; }

                LoadAnimationsFromFile(fullPath, fileName);
            }
            fileName = dir.GetNext();
        }
    }

    private void LoadAnimationsFromFile(string path, string filename)
    {
        if (!ResourceLoader.Exists(path)) return;

        var scn = GD.Load<PackedScene>(path);
        if (scn == null) return;

        var inst = scn.Instantiate();
        var srcPlayer = FindAnimationPlayerRecursive(inst);
        if (srcPlayer != null)
        {
            foreach (var animName in srcPlayer.GetAnimationList())
            {
                var srcAnim = srcPlayer.GetAnimation(animName);
                var newAnim = srcAnim.Duplicate() as Animation;

                // Simple Retargeting: try to map tracks to our skeleton
                RetargetAnimation(newAnim);

                // Sanitize name for Godot (no colons/dots in animation names)
                string safeFilename = filename.Replace(".", "_").Replace(":", "_").Replace(" ", "_");
                string safeAnimName = animName.Replace(".", "_").Replace(":", "_").Replace(" ", "_");
                string uniqueName = $"{safeFilename}_{safeAnimName}";

                // In Godot 4, animations live in libraries. Use the default one ("").
                if (!_animPlayer.HasAnimationLibrary(""))
                {
                    _animPlayer.AddAnimationLibrary("", new AnimationLibrary());
                }

                var lib = _animPlayer.GetAnimationLibrary("");
                if (lib.HasAnimation(uniqueName)) lib.RemoveAnimation(uniqueName);
                lib.AddAnimation(uniqueName, newAnim);

                // Store original display name for the button label
                _animDisplayNames[uniqueName] = $"{filename} -> {animName}";
            }
        }
        inst.QueueFree();
    }

    private void RetargetAnimation(Animation anim)
    {
        if (_skeleton == null) return;

        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string path = anim.TrackGetPath(i).ToString();
            // Expected: "Root/Skeleton:Bone" or just "Bone"
            string boneName = path;
            string property = "";

            if (path.Contains(":"))
            {
                var parts = path.Split(':');
                boneName = parts[0];
                if (parts.Length > 1) property = parts[1];
            }

            // Strip hierarchy
            if (boneName.Contains("/")) boneName = boneName.Split('/').Last();

            // Try to find this bone in our skeleton
            int boneIdx = _skeleton.FindBone(boneName);
            if (boneIdx != -1)
            {
                string newPath = $"{_skeleton.Name}:{boneName}";
                if (!string.IsNullOrEmpty(property)) newPath += $":{property}";
                anim.TrackSetPath(i, newPath);
            }
        }
    }

    private void PopulateAnimationGrid()
    {
        foreach (Node child in _animGrid.GetChildren()) child.QueueFree();

        if (_animPlayer == null) return;

        var anims = _animPlayer.GetAnimationList();
        foreach (var animName in anims)
        {
            string displayText = animName;
            if (_animDisplayNames.ContainsKey(animName)) displayText = _animDisplayNames[animName];

            var btn = new Button { Text = displayText, CustomMinimumSize = new Vector2(0, 30) };
            btn.Pressed += () => PlayAnim(animName);
            _animGrid.AddChild(btn);
        }
    }

    private readonly Dictionary<string, string> _animDisplayNames = new();

    private void PlayAnim(string name)
    {
        _selectedAnimInGrid = name;
        if (_animPlayer != null)
        {
            _animPlayer.Play(name);
            GD.Print($"[AnimPreview] Selected/Playing: {name}");
        }

        // Highlight selected btn (Visual hint)
        foreach (Button b in _animGrid.GetChildren().OfType<Button>())
        {
            b.Modulate = (b.Text == name) ? Colors.Gold : Colors.White;
        }
    }

    private void AssignToSlot(string slot, Label displayLabel)
    {
        if (string.IsNullOrEmpty(_selectedAnimInGrid))
        {
            GD.Print("[AnimPreview] Select an animation from the grid first!");
            return;
        }

        displayLabel.Text = _selectedAnimInGrid;
        displayLabel.Modulate = Colors.Green;

        GD.Print($"[AnimPreview] MAPPED: {slot} -> {_selectedAnimInGrid}");
    }

    private void UpdateExistingMappings()
    {
        if (_currentModel == null) return;

        foreach (var slot in _targetSlots)
        {
            var label = GetNodeOrNull<Label>("%SlotContainer/Value_" + slot) ??
                        FindValueLabel(slot);

            if (label != null)
            {
                // Check AnimationMap or AnimationSources
                string mapped = "";
                if (_currentModel.AnimationSources.ContainsKey(slot)) mapped = _currentModel.AnimationSources[slot];
                else if (_currentModel.AnimationMap.ContainsKey(slot)) mapped = _currentModel.AnimationMap[slot];

                if (!string.IsNullOrEmpty(mapped))
                {
                    label.Text = mapped.Split('/').Last(); // Show filename only for sources
                    label.Modulate = Colors.Cyan;
                }
                else
                {
                    label.Text = "[Unmapped]";
                    label.Modulate = new Color(0.7f, 0.7f, 0.7f);
                }
            }
        }
    }

    private Label FindValueLabel(string slot)
    {
        var container = GetNode<VBoxContainer>("%SlotContainer");
        foreach (var child in container.GetChildren())
        {
            if (child is HBoxContainer hBox)
            {
                var label = hBox.GetChildren().OfType<Label>().FirstOrDefault(l => l.Name == "Value_" + slot);
                if (label != null) return label;
            }
        }
        return null;
    }

    private void OnExportPressed()
    {
        GD.Print("\n--- ANIMATION MAPPING EXPORT ---");
        GD.Print($"Model: {_currentModel.Id}");

        var container = GetNode<VBoxContainer>("%SlotContainer");
        foreach (var child in container.GetChildren())
        {
            if (child is HBoxContainer hBox)
            {
                var labels = hBox.GetChildren().OfType<Label>().ToList();
                if (labels.Count >= 2)
                {
                    string slot = labels[0].Text.Replace(":", "");
                    string val = labels[1].Text;
                    if (val != "[Unmapped]")
                    {
                        GD.Print($"  {{ \"{slot}\", \"{val}\" }},");
                    }
                }
            }
        }
        GD.Print("--- END EXPORT ---\n");
        GD.Print("Please copy the above lines and send them back to the assistant!");
    }

    private AnimationPlayer FindAnimationPlayerRecursive(Node node)
    {
        if (node is AnimationPlayer ap && ap.GetAnimationList().Length > 0) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindAnimationPlayerRecursive(child);
            if (found != null) return found;
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
}
