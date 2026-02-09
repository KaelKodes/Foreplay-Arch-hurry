using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

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
	private Control _meleeHUD;
	private Control _buildHUD;
	private Control _walkHUD;

	// Pause Menu
	private Control _pauseMenu;
	private Button _resumeBtn;
	private Button _hostBtn;
	private Button _exitBtn;

	private Control _objectGallery;
	private GridContainer _objectGrid;
	private Control _galleryContent;
	private Control _resizeHandle;
	private Button _minimizeBtn;
	private bool _isResizing = false;
	private Vector2 _resizeStartPos;
	private Vector2 _resizeStartSize;

	// Category UI
	private HBoxContainer _mainCategoryContainer;
	private HBoxContainer _subCategoryContainer;
	private string _currentMainCategory = "Nature";
	private string _currentSubCategory = "Trees";

	public enum BuildTool { Selection, Survey, NewObject }
	private BuildTool _currentTool = BuildTool.Selection;
	public BuildTool CurrentTool => _currentTool;
	private List<ObjectGalleryData.ObjectAsset> _allAssets = new List<ObjectGalleryData.ObjectAsset>();

	// File Dialogs
	private FileDialog _saveDialog;
	private FileDialog _loadDialog;

	public void RegisterPlayer(PlayerController player)
	{
		_player = player;
		_archerySystem = _player.GetNodeOrNull<ArcherySystem>("ArcherySystem");
		(_meleeHUD as MeleeHUDController)?.RegisterPlayer(player);
		(_archeryHUD as ArcheryHUDController)?.RegisterPlayer(player);
		(_buildHUD as BuildHUDController)?.RegisterPlayer(player);
		UpdateHUDForMode(_player.CurrentState);
		GD.Print("MainHUD: Player Registered");
	}

	public override void _Ready()
	{
		_archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);
		_player = GetNodeOrNull<PlayerController>(PlayerPath) ?? GetTree().CurrentScene.FindChild("PlayerPlaceholder", true, false) as PlayerController;

		_modeLabel = GetNode<Label>("ModeLabel");
		var hints = GetNode<VBoxContainer>("BottomLeftHints");
		_modeHint = hints.GetNode<Label>("ModeHint");
		_buildHint1 = hints.GetNode<Label>("BuildHint1");
		_buildHint2 = hints.GetNode<Label>("BuildHint2");
		_buildHint3 = hints.GetNode<Label>("BuildHint3");
		_buildHint4 = hints.GetNode<Label>("BuildHint4");
		_buildHint5 = (Label)_buildHint4.Duplicate();
		hints.AddChild(_buildHint5);

		_promptLabel = GetNode<Label>("PromptContainer/PromptLabel");
		_archeryHUD = GetNode<Control>("ArcheryHUD");
		_meleeHUD = GetNodeOrNull<Control>("MeleeHUD");
		_buildHUD = GetNode<Control>("BuildHUD");
		_walkHUD = GetNode<Control>("WalkHUD");

		_pauseMenu = GetNode<Control>("PauseMenu");
		_resumeBtn = GetNode<Button>("PauseMenu/VBox/ResumeBtn");
		_hostBtn = GetNode<Button>("PauseMenu/VBox/HostBtn");
		_exitBtn = GetNode<Button>("PauseMenu/VBox/ExitBtn");

		_resumeBtn.Pressed += () => SetPauseMenuVisible(false);
		_hostBtn.Pressed += OnHostMidGamePressed;
		_exitBtn.Pressed += OnExitToMenuPressed;

		_objectGallery = GetNode<Control>("ObjectGallery");
		_galleryContent = GetNode<Control>("ObjectGallery/Background/MarginContainer/VBox");
		_objectGrid = GetNode<GridContainer>("ObjectGallery/Background/MarginContainer/VBox/Scroll/Grid");
		_objectGrid.Columns = 3;

		_resizeHandle = GetNode<Control>("ObjectGallery/ResizeHandle");
		_resizeHandle.GuiInput += OnResizeHandleGuiInput;
		_minimizeBtn = GetNode<Button>("ObjectGallery/MinimizeBtn");
		_minimizeBtn.Pressed += () => SetGalleryExpanded(false);

		var vbox = GetNode<VBoxContainer>("ObjectGallery/Background/MarginContainer/VBox");
		_mainCategoryContainer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddChild(_mainCategoryContainer);
		vbox.MoveChild(_mainCategoryContainer, 1);

		_subCategoryContainer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddChild(_subCategoryContainer);
		vbox.MoveChild(_subCategoryContainer, 2);

		_allAssets = ObjectGalleryData.GetAssets();
		InitializeCategories();

		if (_archerySystem != null)
			_archerySystem.Connect(ArcherySystem.SignalName.PromptChanged, new Callable(this, MethodName.OnPromptChanged));

		UpdateHUDForMode(PlayerState.WalkMode);
	}

	public override void _Process(double delta)
	{
		if (_player != null) UpdateHUDForMode(_player.CurrentState);
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
			modeText += $" - {_currentTool.ToString().ToUpper().Replace("NEWOBJECT", "NEW OBJECT")}";

		if (_modeLabel != null) _modeLabel.Text = modeText;

		_archeryHUD.Visible = (state == PlayerState.CombatArcher);
		if (_meleeHUD != null) _meleeHUD.Visible = (state == PlayerState.CombatMelee);
		_buildHUD.Visible = (state == PlayerState.BuildMode || state == PlayerState.PlacingObject);
		_walkHUD.Visible = (state == PlayerState.WalkMode);

		if (state != PlayerState.BuildMode && state != PlayerState.PlacingObject) _objectGallery.Visible = false;
		else if (_currentTool == BuildTool.NewObject) SetGalleryExpanded(_objectGallery.Visible);

		UpdateHints(state, currentSelected);
		GD.Print($"MainHUD: Switched to {state}");
	}

	private void UpdateHints(PlayerState state, InteractableObject currentSelected)
	{
		_modeHint.Visible = (state == PlayerState.WalkMode || state == PlayerState.BuildMode || state == PlayerState.PlacingObject);
		if (state == PlayerState.WalkMode)
		{
			_modeHint.Text = "WALK MODE";
			_buildHint1.Visible = _buildHint2.Visible = _buildHint3.Visible = _buildHint4.Visible = _buildHint5.Visible = false;
		}
		else if (state == PlayerState.BuildMode)
		{
			_modeHint.Text = "BUILD MODE";
			if (_currentTool == BuildTool.Selection && currentSelected != null)
			{
				_buildHint1.Visible = true; _buildHint1.Text = "LMB: Select / Drag Rotate";
				_buildHint2.Visible = true; _buildHint2.Text = "X: Delete | C: Move";
				_buildHint3.Visible = true; _buildHint3.Text = "Wheel: Rotate | Shift: Height";
				_buildHint4.Visible = true; _buildHint4.Text = "Ctrl+Wheel: Scale";
				_buildHint5.Visible = false;
			}
			else if (_currentTool != BuildTool.Selection && _currentTool != BuildTool.NewObject)
			{
				_buildHint1.Visible = true; _buildHint1.Text = "LMB: Drop Point";
				_buildHint2.Visible = true; _buildHint2.Text = "X: Delete";
				_buildHint3.Visible = true; _buildHint3.Text = "C: Reposition";
				_buildHint4.Visible = _buildHint5.Visible = false;
			}
			else _buildHint1.Visible = _buildHint2.Visible = _buildHint3.Visible = _buildHint4.Visible = _buildHint5.Visible = false;
		}
		else if (state == PlayerState.PlacingObject)
		{
			_modeHint.Text = "PLACING OBJECT";
			_buildHint1.Visible = true; _buildHint1.Text = "LMB: Place Object";
			_buildHint2.Visible = true; _buildHint2.Text = "RMB: Cancel Placement";
			_buildHint3.Visible = true; _buildHint3.Text = "Wheel: Rotate";
			_buildHint4.Visible = true; _buildHint4.Text = "Shift+Wheel: Adjust Height";
			_buildHint5.Visible = true; _buildHint5.Text = "Ctrl+Wheel: Scale";
		}
		else _buildHint1.Visible = _buildHint2.Visible = _buildHint3.Visible = _buildHint4.Visible = _buildHint5.Visible = false;
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
		if (tool == BuildTool.NewObject) { SetGalleryExpanded(true); PopulateGallery(); }
		else _objectGallery.Visible = false;

		_lastState = (PlayerState)(-1);
		UpdateHUDForMode(_player?.CurrentState ?? PlayerState.BuildMode);
	}
}
