using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Archery;

public partial class MainHUDController : CanvasLayer
{
	[Export] public NodePath ArcherySystemPath;
	[Export] public NodePath PlayerPath;

	private ArcherySystem _archerySystem;
	private PlayerController _player;

	private Label _modeLabel;
	private Label _modeHint;
	private Label _buildHint1;
	private Label _buildHint2;
	private Label _buildHint3;
	private Label _buildHint4;
	private Label _buildHint5;
	private Label _promptLabel;

	private Control _archeryHUD;
	private Control _buildHUD;
	private Control _walkHUD;

	private Control _toolsPanel;
	private Control _objectGallery;
	private HBoxContainer _categoryContainer; // Kept for legacy reference if needed, but implementation uses specific ones
	private GridContainer _objectGrid;
	private Button _galleryToggleBtn;
	private Control _galleryContent;

	// Category UI
	private HBoxContainer _mainCategoryContainer;
	private HBoxContainer _subCategoryContainer;
	private string _currentMainCategory = "Nature";
	private string _currentSubCategory = "Trees";

	public enum BuildTool { Selection, Survey, NewObject }
	private BuildTool _currentTool = BuildTool.Selection;
	public BuildTool CurrentTool => _currentTool;
	private struct ObjectAsset
	{
		public string Name;
		public string Path;
		public string MainCategory;
		public string SubCategory;
	}
	private List<ObjectAsset> _allAssets = new List<ObjectAsset>();

	public void RegisterPlayer(PlayerController player)
	{
		_player = player;
		UpdateHUDForMode(_player.CurrentState);
		GD.Print("MainHUD: Player Registered");
	}

	public override void _Ready()
	{
		_archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);
		_player = GetNodeOrNull<PlayerController>(PlayerPath);

		// Fallback for player
		if (_player == null)
			_player = GetTree().CurrentScene.FindChild("PlayerPlaceholder", true, false) as PlayerController;

		_modeLabel = GetNode<Label>("ModeLabel");

		var hints = GetNode<VBoxContainer>("BottomLeftHints");
		_modeHint = hints.GetNode<Label>("ModeHint");
		_buildHint1 = hints.GetNode<Label>("BuildHint1");
		_buildHint2 = hints.GetNode<Label>("BuildHint2");
		_buildHint3 = hints.GetNode<Label>("BuildHint3");
		_buildHint4 = hints.GetNode<Label>("BuildHint4");

		// Dynamically add 5th hint
		_buildHint5 = (Label)_buildHint4.Duplicate();
		hints.AddChild(_buildHint5);

		_promptLabel = GetNode<Label>("PromptContainer/PromptLabel");

		_archeryHUD = GetNode<Control>("ArcheryHUD");
		_buildHUD = GetNode<Control>("BuildHUD");
		_walkHUD = GetNode<Control>("WalkHUD");

		_toolsPanel = GetNode<Control>("ToolsPanel");
		_objectGallery = GetNode<Control>("ObjectGallery");
		_galleryContent = GetNode<Control>("ObjectGallery/VBox");
		_objectGrid = GetNode<GridContainer>("ObjectGallery/VBox/Scroll/Grid");
		_objectGrid.Columns = 3;

		// Setup Minimize/Expand Button
		_galleryToggleBtn = new Button();
		_galleryToggleBtn.Text = "▶";
		_galleryToggleBtn.CustomMinimumSize = new Vector2(40, 40);
		_galleryToggleBtn.Size = new Vector2(40, 40);
		_galleryToggleBtn.Position = _objectGallery.Position; // Match gallery position
		_galleryToggleBtn.Pressed += () => SetGalleryExpanded(true);
		AddChild(_galleryToggleBtn); // Add to HUD directly, not inside gallery

		// Setup Category Containers
		var vbox = GetNode<VBoxContainer>("ObjectGallery/VBox");

		// Main Categories (Top Row)
		_mainCategoryContainer = new HBoxContainer();
		_mainCategoryContainer.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(_mainCategoryContainer);
		vbox.MoveChild(_mainCategoryContainer, 1);

		// Sub Categories (Second Row) - Add a spacer or separator if needed
		_subCategoryContainer = new HBoxContainer();
		_subCategoryContainer.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(_subCategoryContainer);
		vbox.MoveChild(_subCategoryContainer, 2);

		// Connect Tool Buttons
		GetNode<Button>("ToolsPanel/VBox/SelectionBtn").Pressed += () => SetBuildTool(BuildTool.Selection);
		GetNode<Button>("ToolsPanel/VBox/SurveyBtn").Pressed += () => SetBuildTool(BuildTool.Survey);
		GetNode<Button>("ToolsPanel/VBox/NewObjectBtn").Pressed += () => SetBuildTool(BuildTool.NewObject);

		ScanAssets();
		InitializeCategories();

		if (_archerySystem != null)
		{
			_archerySystem.Connect(ArcherySystem.SignalName.PromptChanged, new Callable(this, MethodName.OnPromptChanged));
		}

		// Initial update
		UpdateHUDForMode(PlayerState.WalkMode);
	}

	public override void _Process(double delta)
	{
		if (_player != null)
		{
			UpdateHUDForMode(_player.CurrentState);
		}
	}

	private PlayerState _lastState = (PlayerState)(-1);
	private InteractableObject _lastSelectedObject = null;

	private void UpdateHUDForMode(PlayerState state)
	{
		var currentSelected = _player?.SelectedObject;
		bool stateChanged = (state != _lastState);
		bool selectionChanged = (state == PlayerState.BuildMode && currentSelected != _lastSelectedObject);

		if (!stateChanged && !selectionChanged) return;

		_lastState = state;
		_lastSelectedObject = currentSelected;

		string modeText = state.ToString().ToUpper().Replace("MODE", " MODE");
		if (state == PlayerState.BuildMode || state == PlayerState.PlacingObject)
		{
			modeText += $" - {_currentTool.ToString().ToUpper().Replace("NEWOBJECT", "NEW OBJECT")}";
		}
		if (_modeLabel == null) return;
		_modeLabel.Text = modeText;

		// Toggle sub-HUDs
		_archeryHUD.Visible = (state == PlayerState.CombatMode);
		_buildHUD.Visible = (state == PlayerState.BuildMode && _currentTool == BuildTool.Survey);
		_walkHUD.Visible = (state == PlayerState.WalkMode);

		_toolsPanel.Visible = (state == PlayerState.BuildMode || state == PlayerState.PlacingObject);
		if (state != PlayerState.BuildMode && state != PlayerState.PlacingObject)
		{
			_objectGallery.Visible = false;
			_galleryToggleBtn.Visible = false;
		}
		else if (_currentTool == BuildTool.NewObject)
		{
			// Visibility handled by SetBuildTool/SetGalleryExpanded state
			bool isExpanded = _objectGallery.Visible;
			SetGalleryExpanded(isExpanded);
		}

		// Update mode prompt hints
		if (state == PlayerState.WalkMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: BUILD MODE | R: COMBAT MODE";
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
			_buildHint4.Visible = false;
			_buildHint5.Visible = false;
		}
		else if (state == PlayerState.BuildMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: WALK MODE";

			// Customize hints based on tool
			if (_currentTool == BuildTool.Selection)
			{
				if (currentSelected != null)
				{
					_buildHint1.Visible = true; _buildHint1.Text = "LMB: Select / Drag Rotate";
					_buildHint2.Visible = true; _buildHint2.Text = "X: Delete | C: Move";
					_buildHint3.Visible = true; _buildHint3.Text = "Wheel: Rotate | Shift: Height";
					_buildHint4.Visible = true; _buildHint4.Text = "Ctrl+Wheel: Scale";
					_buildHint5.Visible = false;
				}
				else
				{
					// Hide all hints when nothing is selected
					_buildHint1.Visible = false;
					_buildHint2.Visible = false;
					_buildHint3.Visible = false;
					_buildHint4.Visible = false;
					_buildHint5.Visible = false;
				}
			}
			else // Turn/Survey
			{
				_buildHint1.Visible = true; _buildHint1.Text = "Spacebar: Drop Point";
				_buildHint2.Visible = true; _buildHint2.Text = "X: Delete";
				_buildHint3.Visible = true; _buildHint3.Text = "C: Reposition";
				_buildHint4.Visible = false;
				_buildHint5.Visible = false;
			}
		}
		else if (state == PlayerState.PlacingObject)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "PLACING OBJECT";
			_buildHint1.Visible = true;
			_buildHint1.Text = "LMB: Place Object";
			_buildHint2.Visible = true;
			_buildHint2.Text = "RMB: Cancel Placement";
			_buildHint3.Visible = true;
			_buildHint3.Text = "Wheel: Rotate";
			_buildHint4.Visible = true;
			_buildHint4.Text = "Shift+Wheel: Adjust Height";
			_buildHint5.Visible = true;
			_buildHint5.Text = "Ctrl+Wheel: Scale";
		}
		else
		{
			_modeHint.Visible = false;
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
			_buildHint4.Visible = false;
			_buildHint5.Visible = false;
		}

		GD.Print($"MainHUD: Switched to {state}");
	}

	private void OnPromptChanged(bool visible, string message)
	{
		if (_promptLabel != null)
		{
			_promptLabel.Visible = visible;
			if (!string.IsNullOrEmpty(message)) _promptLabel.Text = message;
		}
	}

	public void SetBuildTool(BuildTool tool)
	{
		_currentTool = tool;

		if (tool == BuildTool.NewObject)
		{
			SetGalleryExpanded(true);
			PopulateGallery(); // Uses current categories
		}
		else
		{
			_objectGallery.Visible = false;
			_galleryToggleBtn.Visible = false;
		}

		GD.Print($"Build Tool Switched to: {tool}");

		// Update hints based on tool
		_lastState = (PlayerState)(-1); // Force hint refresh
		UpdateHUDForMode(_player?.CurrentState ?? PlayerState.BuildMode);
	}

	private void SelectObjectToPlace(string objectId)
	{
		GD.Print($"Selected Object to Place: {objectId}");

		string scenePath = "";
		bool isDirectGltf = false;

		var asset = _allAssets.Find(a => a.Name == objectId);
		if (!string.IsNullOrEmpty(asset.Path))
		{
			scenePath = asset.Path;
			isDirectGltf = true;
		}
		else
		{
			switch (objectId)
			{
				case "DistanceSign": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
				case "TeePin": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break; // TeePin logic might differ, assuming placeholder
				case "Pin": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
				case "CourseMap": scenePath = "res://Scenes/Environment/CourseMapSign.tscn"; break;
			}
		}

		if (string.IsNullOrEmpty(scenePath)) return;

		if (isDirectGltf)
		{
			var model = GD.Load<PackedScene>(scenePath).Instantiate();
			var obj = new InteractableObject();
			obj.Name = objectId;
			obj.ObjectName = objectId;
			obj.AddChild(model);

			// Add a simple collision if missing
			var staticBody = new StaticBody3D();
			var col = new CollisionShape3D();
			var sphere = new SphereShape3D();
			sphere.Radius = 1.0f;
			col.Shape = sphere;
			staticBody.AddChild(col);
			obj.AddChild(staticBody);

			if (_archerySystem != null && _archerySystem.ObjectPlacer != null)
			{
				_archerySystem.ObjectPlacer.SpawnAndPlace(obj);
			}
		}
		else
		{
			var scene = GD.Load<PackedScene>(scenePath);
			if (_archerySystem != null && _archerySystem.ObjectPlacer != null)
			{
				var obj = scene.Instantiate<InteractableObject>();
				obj.ObjectName = objectId;
				_archerySystem.ObjectPlacer.SpawnAndPlace(obj);
			}
		}

		// Minimize gallery after selection
		SetGalleryExpanded(false);
	}

	private void SetGalleryExpanded(bool expanded)
	{
		_objectGallery.Visible = expanded;
		_galleryToggleBtn.Visible = !expanded;
	}

	private void ScanAssets()
	{
		_allAssets.Clear();
		// Hardcoded Utility items
		_allAssets.Add(new ObjectAsset { Name = "TeePin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
		_allAssets.Add(new ObjectAsset { Name = "Pin", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
		_allAssets.Add(new ObjectAsset { Name = "DistanceSign", MainCategory = "Utility", SubCategory = "Markers", Path = "" });
		_allAssets.Add(new ObjectAsset { Name = "CourseMap", MainCategory = "Utility", SubCategory = "Markers", Path = "" });

		string[] searchPaths = {
			"res://Assets/Textures/NatureObjects/",
			"res://Assets/Textures/ManObjects/",
            "res://Assets/Textures/BuildingObjects/"
		};

		foreach (string path in searchPaths)
		{
			using var dir = DirAccess.Open(path);
			if (dir != null)
			{
				dir.ListDirBegin();
				string fileName = dir.GetNext();
				while (fileName != "")
				{
					if (fileName.EndsWith(".gltf") || fileName.EndsWith(".gltf.remap") || fileName.EndsWith(".gltf.import"))
					{
						string logicalName = fileName.Replace(".remap", "").Replace(".import", "");
						string cleanName = logicalName.Replace(".gltf", "");

						if (!_allAssets.Exists(a => a.Name == cleanName))
						{
							var (main, sub) = GetAssetCategories(cleanName);
							_allAssets.Add(new ObjectAsset
							{
								Name = cleanName,
								Path = path + logicalName,
								MainCategory = main,
								SubCategory = sub
							});
						}
					}
					fileName = dir.GetNext();
				}
			}
		}
		GD.Print($"MainHUD: Scanned {_allAssets.Count} assets.");
	}

	private (string Main, string Sub) GetAssetCategories(string name)
	{
		name = name.ToLower();

		// --- NATURE ---
		if (name.Contains("tree") || name.Contains("pine") || name.Contains("trunk") || name.Contains("log")) return ("Nature", "Trees");
		if (name.Contains("rock") || name.Contains("pebble") || name.Contains("rubble")) return ("Nature", "Rocks");
		if (name.Contains("flower") || name.Contains("bush") || name.Contains("grass") || name.Contains("fern") || name.Contains("clover") || name.Contains("plant") || name.Contains("vine") || name.Contains("leaf")) return ("Nature", "Greenery");
		if (name.Contains("mushroom")) return ("Nature", "Mushrooms");

		// --- STRUCTURES ---
		if (name.Contains("wall") || name.Contains("barrier")) return ("Structures", "Walls");
		if (name.Contains("roof") || name.Contains("overhang")) return ("Structures", "Roofs");
		if (name.Contains("floor") || name.Contains("foundation") || name.Contains("ceiling")) return ("Structures", "Floors");
		if (name.Contains("stair")) return ("Structures", "Stairs");
		if (name.Contains("door") || name.Contains("window") || name.Contains("shutter")) return ("Structures", "Doors/Windows");
		if (name.Contains("column") || name.Contains("pillar") || name.Contains("balcony") || name.Contains("support")) return ("Structures", "Columns");

		// --- FURNITURE ---
		if (name.Contains("chair") || name.Contains("stool") || name.Contains("bench")) return ("Furniture", "Seating");
		if (name.Contains("table") || name.Contains("shelf") || name.Contains("shelves")) return ("Furniture", "Surfaces");
		if (name.Contains("bed")) return ("Furniture", "Sleeping");
		if (name.Contains("box") || name.Contains("crate") || name.Contains("barrel") || name.Contains("keg") || name.Contains("chest")) return ("Furniture", "Storage");

		// --- DECOR ---
		if (name.Contains("torch") || name.Contains("candle") || name.Contains("lantern")) return ("Decor", "Lighting");
		if (name.Contains("banner") || name.Contains("flag") || name.Contains("shield") || name.Contains("sword") || name.Contains("weapon") || name.Contains("keyring")) return ("Decor", "Military");
		if (name.Contains("bottle") || name.Contains("plate") || name.Contains("cup") || name.Contains("coin") || name.Contains("key") || name.Contains("book") || name.Contains("food")) return ("Decor", "Items");
		if (name.Contains("prop") || name.Contains("cart") || name.Contains("wagon")) return ("Decor", "Misc");

		return ("Misc", "General");
	}

	private void InitializeCategories()
	{
		CreateMainCategoryButtons();
		SelectMainCategory("Nature"); // Default
	}

	private void CreateMainCategoryButtons()
	{
		// Clear existing
		foreach (Node child in _mainCategoryContainer.GetChildren()) child.QueueFree();

		string[] categories = { "Nature", "Structures", "Furniture", "Decor", "Utility", "Misc" };
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

		// Auto-select first available subcategory
		var firstSub = _allAssets.Find(a => a.MainCategory == mainCategory).SubCategory;
		if (string.IsNullOrEmpty(firstSub)) firstSub = "General"; // Fallback

		SelectSubCategory(firstSub);
	}

	private void UpdateSubCategoryButtons(string mainCategory)
	{
		foreach (Node child in _subCategoryContainer.GetChildren()) child.QueueFree();

		// Find distinct subcategories for this main category
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
			// Fill width
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			btn.CustomMinimumSize = new Vector2(120, 40); // Slightly smaller min width to fit grid
			btn.Pressed += () => SelectObjectToPlace(asset.Name);
			_objectGrid.AddChild(btn);
		}
	}

	private FileDialog _saveDialog;
	private FileDialog _loadDialog;

	public void ShowSaveLoadMenu()
	{
		if (_saveDialog == null) SetupFileDialogs();

		// Simple Popup Menu to choose Save or Load
		var popup = new PopupMenu();
		popup.AddItem("Save Course");
		popup.AddItem("Load Course");
		popup.IdPressed += (id) =>
		{
			if (id == 0) _saveDialog.PopupCentered(new Vector2I(600, 400));
			else _loadDialog.PopupCentered(new Vector2I(600, 400));
		};

		AddChild(popup);
		popup.PopupCentered(new Vector2I(200, 100)); // Show immediately
	}

	private void SetupFileDialogs()
	{
		_saveDialog = new FileDialog();
		_saveDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		_saveDialog.Access = FileDialog.AccessEnum.Userdata;
		_saveDialog.Filters = new string[] { "*.json" };
		_saveDialog.CurrentDir = "user://courses";
		_saveDialog.FileSelected += OnSaveFileSelected;
		AddChild(_saveDialog);

		_loadDialog = new FileDialog();
		_loadDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		_loadDialog.Access = FileDialog.AccessEnum.Userdata;
		_loadDialog.Filters = new string[] { "*.json" };
		_loadDialog.CurrentDir = "user://courses";
		_loadDialog.FileSelected += OnLoadFileSelected;
		AddChild(_loadDialog);
	}

	private void OnSaveFileSelected(string path)
	{
		// Extract filename from path
		string filename = System.IO.Path.GetFileNameWithoutExtension(path);

		// Get Scene Data
		var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
		var root = GetTree().CurrentScene;

		CoursePersistenceManager.Instance.SaveCourse(filename, terrain, root);

		// Feedback
		if (_archerySystem != null)
		{
			_archerySystem.SetPrompt(true, $"SAVED: {filename}");
			GetTree().CreateTimer(2.0f).Connect("timeout", Callable.From(() => _archerySystem.SetPrompt(false)));
		}
	}

	private void OnLoadFileSelected(string path)
	{
		string filename = System.IO.Path.GetFileNameWithoutExtension(path);

		// Get Scene Data
		var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
		var root = GetTree().CurrentScene;

		bool success = CoursePersistenceManager.Instance.LoadCourse(filename, terrain, root);

		if (_archerySystem != null)
		{
			if (success)
			{
				_archerySystem.SetPrompt(true, $"LOADED: {filename}");
			}
			else
			{
				_archerySystem.SetPrompt(true, $"FAILED TO LOAD: {filename}");
			}
			GetTree().CreateTimer(2.0f).Connect("timeout", Callable.From(() => _archerySystem.SetPrompt(false)));
		}
	}
}
