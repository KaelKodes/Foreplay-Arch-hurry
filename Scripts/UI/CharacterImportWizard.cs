using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

/// <summary>
/// Controller for the Character Import Wizard (Mesh Swap Workflow - Flash Edition).
/// High-Stakes Fix: Resolves "spindly" distortion by correctly scaling InverseBindPoses.
/// </summary>
public partial class CharacterImportWizard : Control
{
	private int _currentStep = 1;
	private const int MaxSteps = 4;

	// UI References
	private Label _stepIndicator;
	private VBoxContainer _step1, _step2, _step3, _step4;
	private LineEdit _filePathLabel;
	private FileDialog _fileDialog;
	private AcceptDialog _errorDialog;

	private GridContainer _itemGrid;
	private CheckBox _hasOwnWeaponCheck;
	private CheckBox _hasExtraItemsCheck;
	private OptionButton _animSourceDropdown;
	private Label _animDescription;
	private RichTextLabel _summaryText;
	private LineEdit _idEdit, _nameEdit;
	private Button _backButton, _nextButton, _saveButton, _closeButton;

	private Node3D _modelContainer;
	private SubViewportContainer _previewContainer;
	private Camera3D _previewCamera;

	// Logic Data
	private string _selectedFilePath = "";
	private Node3D _loadedModel;
	private SkeletonAnalyzer.AnalysisResult _analysisResult;
	private CharacterConfig _config = new();

	// Preview State
	private bool _isDraggingPreview = false;
	private Vector2 _lastMousePos;
	private float _previewRotationY = 0f;
	private float _cameraDistance = 2.5f;
	private float _cameraHeight = 1.0f;
	private AnimationPlayer _previewAnimPlayer;

	public override void _Ready()
	{
		SetupUI();
		ConnectSignals();
		UpdateStepDisplay();
	}

	private void SetupUI()
	{
		_stepIndicator = GetNode<Label>("VBox/StepIndicator");
		_step1 = GetNode<VBoxContainer>("VBox/Content/Step1_FileSelect");
		_step2 = GetNode<VBoxContainer>("VBox/Content/Step2_Analysis");
		_step3 = GetNode<VBoxContainer>("VBox/Content/Step3_BoneMapping");
		_step4 = GetNode<VBoxContainer>("VBox/Content/Step5_Preview");

		_filePathLabel = GetNode<LineEdit>("VBox/Content/Step1_FileSelect/FilePathBox/FilePathLabel");
		_fileDialog = GetNode<FileDialog>("FileDialog");

		// Step 2: Item Config
		foreach (Node child in _step2.GetChildren()) child.QueueFree();
		_step2.AddChild(new Label { Text = "Step 2: Configure Meshes & Items", ThemeTypeVariation = "HeaderLarge" });
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), SizeFlagsVertical = SizeFlags.ExpandFill };
		_step2.AddChild(scroll);
		_itemGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		scroll.AddChild(_itemGrid);
		var optionsBox = new VBoxContainer();
		_step2.AddChild(new HSeparator());
		_step2.AddChild(optionsBox);
		_hasOwnWeaponCheck = new CheckBox { Text = "Include Embedded Weapon?" };
		_hasExtraItemsCheck = new CheckBox { Text = "Model has Extra Items?" };
		optionsBox.AddChild(_hasOwnWeaponCheck);
		optionsBox.AddChild(_hasExtraItemsCheck);

		// Step 3: Animation
		foreach (Node child in _step3.GetChildren()) child.QueueFree();
		_step3.AddChild(new Label { Text = "Step 3: Choose Animation Style", ThemeTypeVariation = "HeaderLarge" });
		var animForm = new GridContainer { Columns = 2 };
		_step3.AddChild(animForm);
		animForm.AddChild(new Label { Text = "Reference Skeleton:" });
		_animSourceDropdown = new OptionButton();
		_animSourceDropdown.AddItem("Erika (Standard Archery)", 0);
		animForm.AddChild(_animSourceDropdown);
		_animDescription = new Label { Text = "Maps mesh to Erika. Perfect for humanoid archery animations.", Modulate = new Color(0.7f, 0.7f, 0.7f) };
		_step3.AddChild(_animDescription);

		_summaryText = GetNode<RichTextLabel>("VBox/Content/Step5_Preview/SummaryText");
		_idEdit = GetNode<LineEdit>("VBox/Content/Step5_Preview/IdInput/IdEdit");
		_nameEdit = GetNode<LineEdit>("VBox/Content/Step5_Preview/NameInput/NameEdit");

		_backButton = GetNode<Button>("VBox/ButtonRow/BackButton");
		_nextButton = GetNode<Button>("VBox/ButtonRow/NextButton");
		_saveButton = GetNode<Button>("VBox/ButtonRow/SaveButton");
		_closeButton = GetNode<Button>("VBox/ButtonRow/CloseButton");

		_previewContainer = GetNode<SubViewportContainer>("ModelPreview");
		_modelContainer = GetNode<Node3D>("ModelPreview/SubViewport/ModelContainer");
		_previewCamera = GetNode<Camera3D>("ModelPreview/SubViewport/PreviewCamera");

		_errorDialog = new AcceptDialog();
		AddChild(_errorDialog);

		// Polish Step 4 UI
		var gridContainer = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_step4.AddChild(gridContainer);
		gridContainer.AddChild(new Label { Text = "Preview Animations:", ThemeTypeVariation = "HeaderSmall" });

		var animScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 150) };
		gridContainer.AddChild(animScroll);

		_step4AnimGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		animScroll.AddChild(_step4AnimGrid);
	}

	private GridContainer _step4AnimGrid;
	private Node3D _previewInstance;
	private Skeleton3D _previewSkeleton;
	// _previewAnimPlayer is already defined

	private void ConnectSignals()
	{
		GetNode<Button>("VBox/Content/Step1_FileSelect/FilePathBox/BrowseButton").Pressed += () => _fileDialog.PopupCentered();
		_filePathLabel.GuiInput += (evt) => { if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) _fileDialog.PopupCentered(); };
		_fileDialog.FileSelected += OnFileSelected;
		_backButton.Pressed += OnBackPressed;
		_nextButton.Pressed += OnNextPressed;
		_saveButton.Pressed += OnSavePressed;
		_closeButton.Pressed += OnClosePressed;
	}

	public override void _Input(InputEvent @event)
	{
		if (_currentStep == 4 && @event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp) _cameraDistance = Mathf.Max(1.0f, _cameraDistance - 0.2f);
			if (mouseButton.ButtonIndex == MouseButton.WheelDown) _cameraDistance = Mathf.Min(10.0f, _cameraDistance + 0.2f);
			UpdateCameraPosition();
			if (mouseButton.ButtonIndex == MouseButton.Left) _isDraggingPreview = mouseButton.Pressed;
		}
		else if (_currentStep == 4 && @event is InputEventMouseMotion mouseMotion && _isDraggingPreview)
		{
			_previewRotationY += mouseMotion.Relative.X * 0.5f;
			if (_modelContainer != null) _modelContainer.RotationDegrees = new Vector3(0, _previewRotationY, 0);
		}
	}

	private void UpdateCameraPosition()
	{
		if (_previewCamera != null) _previewCamera.Position = new Vector3(0, _cameraHeight, _cameraDistance);
	}

	private void UpdateStepDisplay()
	{
		string[] names = { "Select Model", "Configure Items", "Choose Animation", "Preview & Save" };
		_stepIndicator.Text = $"Step {_currentStep} of {MaxSteps}: {names[_currentStep - 1]}";
		_step1.Visible = _currentStep == 1; _step2.Visible = _currentStep == 2;
		_step3.Visible = _currentStep == 3; _step4.Visible = _currentStep == 4;
		_backButton.Visible = _currentStep > 1;
		_nextButton.Visible = _currentStep < MaxSteps;
		_saveButton.Visible = _currentStep == MaxSteps;
	}

	private void OnFileSelected(string path)
	{
		_selectedFilePath = path;
		_filePathLabel.Text = path;
		RunSilentAnalysis();
	}

	private void RunSilentAnalysis()
	{
		if (!ResourceLoader.Exists(_selectedFilePath)) return;
		var scene = GD.Load<PackedScene>(_selectedFilePath);
		if (scene == null) return;

		ClearPreview();
		_loadedModel = scene.Instantiate<Node3D>();
		_modelContainer.AddChild(_loadedModel);

		_analysisResult = SkeletonAnalyzer.AnalyzeModel(_loadedModel);
		SkeletonAnalyzer.AutoMapBones(_analysisResult);

		_config.ModelPath = _selectedFilePath;
		_config.BoneMap = new Dictionary<string, string>(_analysisResult.AutoMappedBones);
		_config.SkeletonSignature = _analysisResult.SkeletonSignature;

		foreach (var mesh in _analysisResult.DetectedMeshes)
		{
			if (!_config.Meshes.ContainsKey(mesh.NodeName))
				_config.Meshes[mesh.NodeName] = new CharacterConfig.MeshConfig { Category = mesh.Category.ToString(), IsVisible = true };
		}
	}

	private void OnNextPressed()
	{
		if (_currentStep == 1 && string.IsNullOrEmpty(_selectedFilePath)) { ShowError("Select a file first."); return; }
		_currentStep++;
		UpdateStepDisplay();
		if (_currentStep == 2) PopulateItemConfigUI();
		if (_currentStep == 4) StartMeshSwapPreview();
	}

	private void OnBackPressed() { if (_currentStep > 1) { _currentStep--; UpdateStepDisplay(); } }

	private void PopulateItemConfigUI()
	{
		foreach (Node child in _itemGrid.GetChildren()) child.QueueFree();
		foreach (var meshName in _config.Meshes.Keys)
		{
			var cfg = _config.Meshes[meshName];
			var card = new PanelContainer();
			card.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.15f), ContentMarginLeft = 5, ContentMarginRight = 5 });
			var vbox = new VBoxContainer(); card.AddChild(vbox);
			vbox.AddChild(new Label { Text = meshName, Modulate = Colors.Gold });

			var typeOpt = new OptionButton();
			foreach (var cat in CharacterConfig.MeshConfig.Categories.All) typeOpt.AddItem(cat);
			typeOpt.Selected = Array.IndexOf(CharacterConfig.MeshConfig.Categories.All, cfg.Category);
			typeOpt.ItemSelected += (idx) => cfg.Category = CharacterConfig.MeshConfig.Categories.All[idx];
			vbox.AddChild(typeOpt);

			var visa = new CheckBox { Text = "Visible", ButtonPressed = cfg.IsVisible };
			visa.Toggled += (b) =>
			{
				cfg.IsVisible = b;
				var node = _loadedModel.FindChild(meshName, true, false) as Node3D;
				if (node != null) node.Visible = b;
			};
			vbox.AddChild(visa);
			_itemGrid.AddChild(card);
		}
	}

	private void StartMeshSwapPreview()
	{
		ClearPreview();
		_previewInstance = null;
		_previewSkeleton = null;
		_previewAnimPlayer = null;

		// 1. Instantiate CUSTOM MODEL directly
		if (!ResourceLoader.Exists(_selectedFilePath)) return;
		var srcScene = GD.Load<PackedScene>(_selectedFilePath);
		_previewInstance = srcScene.Instantiate<Node3D>();
		_modelContainer.AddChild(_previewInstance);

		_previewSkeleton = FindSkeletonRecursive(_previewInstance);
		_previewAnimPlayer = FindAnimationPlayerRecursive(_previewInstance); // We play on the Custom Model now

		if (_previewSkeleton == null)
		{
			ShowError("Selected model has no Skeleton3D.");
			return;
		}

		if (_previewAnimPlayer == null)
		{
			// Try to find one, or create one if missing (though usually imported models have one)
			ShowError("Selected model has no AnimationPlayer. Retargeting requires a player on the target.");
			return;
		}

		// 2. Hide Erika's original meshes (Not needed, we aren't using Erika base anymore)

		// 3. Setup Animation Grid
		SetupAnimationGrid();

		// 4. Play Default Animation
		if (!string.IsNullOrEmpty(_lastPreviewAnim)) PreviewAnimation(_lastPreviewAnim);
		else PreviewAnimation("Idle");

		// 5. Update UI labels
		_summaryText.Text = $"[b]Retarget Preview[/b]\nModel: {_selectedFilePath.GetFile()}";

		_idEdit.Text = System.IO.Path.GetFileNameWithoutExtension(_selectedFilePath).ToLower().Replace(" ", "_");
		_nameEdit.Text = System.IO.Path.GetFileNameWithoutExtension(_selectedFilePath);

		// Shrink Inputs
		_idEdit.CustomMinimumSize = new Vector2(200, 0);
		_idEdit.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		_nameEdit.CustomMinimumSize = new Vector2(200, 0);
		_nameEdit.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

		// 6. Apply Mesh Config (Visibility)
		ApplyMeshConfig(_previewInstance, _config);
	}

	private string _lastPreviewAnim = "Idle";

	private void PreviewAnimation(string standardAnim)
	{
		GD.Print($"[Wizard] PreviewAnimation requested: {standardAnim}");
		_lastPreviewAnim = standardAnim;
		if (_previewAnimPlayer == null) return;

		// Map standard names to what SetupErikaAnimations uses
		var fileMap = new Dictionary<string, string> {
			{ "Idle", "standing idle 01" },
			{ "Walk", "standing walk forward" },
			{ "Run", "standing run forward" },
			{ "Jump", "standing jump" },
			{ "MeleeAttack1", "melee attack" },
			{ "ArcheryIdle", "archery idle normal" },
			{ "ArcheryDraw", "archery draw" },
			{ "ArcheryFire", "archery recoil" },
		};

		if (!fileMap.ContainsKey(standardAnim)) return;
		string fileKey = fileMap[standardAnim];
		if (!SetupErikaAnimations.AnimationFiles.ContainsKey(fileKey)) return;

		string fbxPath = SetupErikaAnimations.AnimationFiles[fileKey];
		if (!ResourceLoader.Exists(fbxPath)) return;

		// Load external animation file (Source: Erika)
		var fbxScene = GD.Load<PackedScene>(fbxPath);
		var instance = fbxScene.Instantiate();
		var srcPlayer = instance.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

		if (srcPlayer != null)
		{
			var srcList = srcPlayer.GetAnimationList();
			if (srcList.Length > 0)
			{
				var srcAnim = srcPlayer.GetAnimation(srcList[0]);
				var newAnim = srcAnim.Duplicate() as Animation;

				// RETARGETING LOGIC (Mirrors CharacterModelManager)
				RetargetAnimationToCustom(newAnim, _previewSkeleton, _config.BoneMap, _previewAnimPlayer);

				newAnim.LoopMode = Animation.LoopModeEnum.Linear;
				if (standardAnim == "Idle" || standardAnim == "ArcheryIdle") newAnim.LoopMode = Animation.LoopModeEnum.Pingpong;

				var lib = new AnimationLibrary();
				if (_previewAnimPlayer.HasAnimationLibrary("")) lib = _previewAnimPlayer.GetAnimationLibrary("");
				else _previewAnimPlayer.AddAnimationLibrary("", lib);

				string animName = "Preview_" + standardAnim;
				if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);

				lib.AddAnimation(animName, newAnim);
				_previewAnimPlayer.Play(animName);
				GD.Print($"[Wizard] Playing {animName} on {_previewAnimPlayer.GetPath()}");
			}
		}
		instance.QueueFree();
	}

	private void RetargetAnimationToCustom(Animation newAnim, Skeleton3D skeleton, Dictionary<string, string> boneMap, AnimationPlayer player)
	{
		// 0. Load Erika's rest pose
        Skeleton3D erikaSkeleton = null;
        const string erikaPath = "res://Assets/Heroes/Ranger/Animations/Erika Archer With Bow Arrow.fbx";
        if (ResourceLoader.Exists(erikaPath))
        {
            var erikaScn = GD.Load<PackedScene>(erikaPath);
            var erikaInst = erikaScn.Instantiate();
            erikaSkeleton = FindSkeletonRecursive(erikaInst);

			// Calculate path relative to AnimationPlayer's root
			Node rootNode = player.GetNode(player.RootNode);
			NodePath skeletonPath = rootNode.GetPathTo(skeleton);

			GD.Print($"[Wizard] Retargeting: Root='{rootNode.Name}' -> Skeleton='{skeleton.Name}' (Path: {skeletonPath})");

			int trackCount = newAnim.GetTrackCount();
			for (int i = trackCount - 1; i >= 0; i--)
			{
				string trackPath = newAnim.TrackGetPath(i).ToString();
				string[] parts = trackPath.Split(':');
				string propertyPart = (parts.Length > 1) ? parts[1] : "";
				string boneInTrack = !string.IsNullOrEmpty(propertyPart) ? propertyPart : parts[0];

				// Handle "Skeleton3D/BoneName" format if present
				int lastSlash = boneInTrack.LastIndexOf('/');
				if (lastSlash != -1) boneInTrack = boneInTrack.Substring(lastSlash + 1);

				string standardBone = boneInTrack.Replace("mixamorig_", "");
				string targetBone = null;

				if (boneMap.ContainsKey(standardBone)) targetBone = boneMap[standardBone];
				else if (boneMap.ContainsKey(boneInTrack)) targetBone = boneMap[boneInTrack];

				if (targetBone != null && skeleton.FindBone(targetBone) != -1)
				{
					// SET CORRECT PATH
					string newPath = $"{skeletonPath}:{targetBone}";
					newAnim.TrackSetPath(i, newPath);

					// REST POSE COMPENSATION
					if (newAnim.TrackGetType(i) == Animation.TrackType.Rotation3D)
					{
						string eBoneName = "mixamorig_" + standardBone;
						int eBoneIdx = erikaSkeleton.FindBone(eBoneName);
						if (eBoneIdx == -1) eBoneIdx = erikaSkeleton.FindBone(standardBone);

						if (eBoneIdx != -1)
						{
							int tBoneIdx = skeleton.FindBone(targetBone);
							Quaternion sRest = erikaSkeleton.GetBoneRest(eBoneIdx).Basis.GetRotationQuaternion();
							Quaternion tRest = skeleton.GetBoneRest(tBoneIdx).Basis.GetRotationQuaternion();

							Quaternion correction = tRest.Inverse() * sRest;

							for (int k = 0; k < newAnim.TrackGetKeyCount(i); k++)
							{
								Quaternion oldQ = (Quaternion)newAnim.TrackGetKeyValue(i, k);
								newAnim.TrackSetKeyValue(i, k, correction * oldQ);
							}
						}
					}
				}
				else
				{
					// GD.Print($"[Wizard] Dropping track {trackPath} (No map for {standardBone})");
					newAnim.RemoveTrack(i);
				}
			}
			erikaInst.QueueFree();
		}
	}

	private void SetupAnimationGrid()
	{
		foreach (Node c in _step4AnimGrid.GetChildren()) c.QueueFree();

		// Use the map keys for standard names
		var anims = new string[] { "Idle", "Walk", "Run", "Jump", "MeleeAttack1", "ArcheryIdle", "ArcheryDraw", "ArcheryFire" };

		foreach (var anim in anims)
		{
			var btn = new Button { Text = anim, CustomMinimumSize = new Vector2(0, 30) };
			btn.Pressed += () => PreviewAnimation(anim);
			_step4AnimGrid.AddChild(btn);
		}
	}

	private void ApplyMeshConfig(Node3D model, CharacterConfig config)
	{
		foreach (var kvp in config.Meshes)
		{
			var meshNode = model.FindChild(kvp.Key, true, false) as Node3D;
			if (meshNode != null) meshNode.Visible = kvp.Value.IsVisible;
		}
	}

	private void HideErikaBaseMeshes(Node3D root)
	{
		string[] keys = { "body", "top", "bottom", "shoes", "hair", "eye", "mouth", "teeth", "tongue", "lash", "brow", "face" };
		foreach (var m in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			string n = m.Name.ToString().ToLower();
			if (keys.Any(k => n.Contains(k))) m.Visible = false;
		}
	}

	private void FindMeshesRecursive(Node n, List<MeshInstance3D> l) { if (n is MeshInstance3D m) l.Add(m); foreach (Node c in n.GetChildren()) FindMeshesRecursive(c, l); }
	private Skeleton3D FindSkeletonRecursive(Node n) { if (n is Skeleton3D s) return s; foreach (Node c in n.GetChildren()) { var r = FindSkeletonRecursive(c); if (r != null) return r; } return null; }
	private AnimationPlayer FindAnimationPlayerRecursive(Node n) { if (n is AnimationPlayer a) return a; foreach (Node c in n.GetChildren()) { var r = FindAnimationPlayerRecursive(c); if (r != null) return r; } return null; }

	private void OnSavePressed()
	{
		if (string.IsNullOrEmpty(_idEdit.Text) || string.IsNullOrEmpty(_nameEdit.Text))
		{
			ShowError("Please enter an ID and Name.");
			return;
		}

		_config.Id = _idEdit.Text;
		_config.DisplayName = _nameEdit.Text;
		_config.AnimationSource = "erika"; // Enforce standard retargeting
		_config.IsValidated = true; // We just previewed it

		if (CharacterConfigManager.SaveConfig(_config))
		{
			GD.Print($"[Wizard] Saved config: {_config.Id}");
			OnClosePressed();
		}
		else
		{
			ShowError("Failed to save configuration.");
		}
	}
	private void OnClosePressed() => Visible = false;
	private void ClearPreview() { foreach (Node c in _modelContainer.GetChildren()) c.QueueFree(); }
	private void ShowError(string msg) { _errorDialog.DialogText = msg; _errorDialog.PopupCentered(); }
}
