using Godot;
using System;
using Archery;

public partial class BuildHUDController : Control
{
	[Export] public NodePath ArcherySystemPath;

	private ArcherySystem _archerySystem;
	private MainHUDController _mainHUD;

	private Button _selectionBtn;
	private Button _newObjectBtn;
	private Button _surveyDoneBtn;

	private Button _surveyConfirmBtn;
	private OptionButton _terrainPicker;
	private Button _raiseBtn;
	private Button _lowerBtn;
	private HBoxContainer _fillPanel;
	private SpinBox _fillSlider;
	private HBoxContainer _smoothingPanel;
	private SpinBox _smoothingSlider;

	// Lazy getter for ArcherySystem (player is spawned after HUD)
	private ArcherySystem GetArcherySystem()
	{
		if (_archerySystem == null)
		{
			_archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);
			if (_archerySystem == null)
				_archerySystem = GetTree().CurrentScene.FindChild("ArcherySystem", true, false) as ArcherySystem;

			// Connect signal if we just found it
			if (_archerySystem?.BuildManager != null && !_signalConnected)
			{
				_archerySystem.BuildManager.Connect(BuildManager.SignalName.SurveyUpdated, new Callable(this, MethodName.UpdateSurveyButton));
				_signalConnected = true;
			}
		}
		return _archerySystem;
	}
	private bool _signalConnected = false;

	public override void _Ready()
	{
		_mainHUD = GetNodeOrNull<MainHUDController>("..");

		// Connect to Scene Buttons
		_selectionBtn = GetNode<Button>("SelectionBtn");
		_newObjectBtn = GetNode<Button>("NewObjectBtn");
		_surveyDoneBtn = GetNode<Button>("FinishShapeBtn");

		if (_mainHUD != null)
		{
			_selectionBtn.Pressed += () => _mainHUD.SetBuildTool(MainHUDController.BuildTool.Selection);
			_newObjectBtn.Pressed += () => _mainHUD.SetBuildTool(MainHUDController.BuildTool.NewObject);
		}

		_surveyDoneBtn.Pressed += OnSurveyDonePressed;

		// --- Terrain Panels (Still code-gen for now, as requested only buttons moved) ---
		_terrainPicker = new OptionButton();
		_terrainPicker.AddItem("Fairway");
		_terrainPicker.AddItem("Rough");
		_terrainPicker.AddItem("Deep Rough");
		_terrainPicker.AddItem("Green");
		_terrainPicker.AddItem("Sand");
		_terrainPicker.AddItem("Water");
		_terrainPicker.Visible = false;
		_terrainPicker.CustomMinimumSize = new Vector2(200, 40);
		AddChild(_terrainPicker);
		_terrainPicker.ItemSelected += (idx) => UpdatePreview();

		_surveyConfirmBtn = new Button();
		_surveyConfirmBtn.Text = "BAKE TERRAIN";
		_surveyConfirmBtn.Visible = false;
		_surveyConfirmBtn.Modulate = Colors.Green;
		_surveyConfirmBtn.CustomMinimumSize = new Vector2(200, 60);
		AddChild(_surveyConfirmBtn);
		_surveyConfirmBtn.Pressed += OnConfirmPressed;

		_raiseBtn = ToolButton("RAISE [↑]", () => GetArcherySystem()?.BuildManager?.ModifyElevation(0.5f));
		_lowerBtn = ToolButton("LOWER [↓]", () => GetArcherySystem()?.BuildManager?.ModifyElevation(-0.5f));

		_fillPanel = new HBoxContainer();
		_fillPanel.Visible = false;
		_fillPanel.Alignment = BoxContainer.AlignmentMode.Center;
		AddChild(_fillPanel);

		_fillSlider = new SpinBox();
		_fillSlider.MinValue = 0;
		_fillSlider.MaxValue = 100;
		_fillSlider.Value = 80;
		_fillSlider.ValueChanged += (val) => GetArcherySystem()?.BuildManager?.SetFillPercentage((float)val);
		_fillPanel.AddChild(new Label { Text = "Fill %: " });
		_fillPanel.AddChild(_fillSlider);

		_smoothingPanel = new HBoxContainer();
		_smoothingPanel.Visible = false;
		_smoothingPanel.Alignment = BoxContainer.AlignmentMode.Center;
		AddChild(_smoothingPanel);

		_smoothingSlider = new SpinBox();
		_smoothingSlider.MinValue = 0;
		_smoothingSlider.MaxValue = 4;
		_smoothingSlider.Value = 0;
		_smoothingSlider.ValueChanged += (val) => { var sys = GetArcherySystem(); if (sys?.BuildManager != null) sys.BuildManager.SmoothingIterations = (int)val; };
		_smoothingPanel.AddChild(new Label { Text = "Smoothing: " });
		_smoothingPanel.AddChild(_smoothingSlider);

		// Signal connection is now handled lazily in GetArcherySystem()
		ResetUI();
	}

	private Button ToolButton(string text, Action action)
	{
		var btn = new Button { Text = text, Visible = false, CustomMinimumSize = new Vector2(200, 40), FocusMode = FocusModeEnum.None };
		btn.Pressed += action;
		AddChild(btn);
		return btn;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged)
		{
			if (Visible) ResetUI();
		}
	}

	public override void _Process(double delta)
	{
		if (Visible && _mainHUD != null)
		{
			var tool = _mainHUD.CurrentTool;
			bool isSurvey = tool == MainHUDController.BuildTool.Survey;

			// Hammer (Selection/NewObject) vs Shovel (Survey)
			_selectionBtn.Visible = !isSurvey;
			_newObjectBtn.Visible = !isSurvey;

			// Survey Button logic
			if (isSurvey)
			{
				// If we are showing the Terrain Picker (Baking Mode), hide the Finish button
				if (_terrainPicker.Visible)
				{
					_surveyDoneBtn.Hide();
				}
				else
				{
					// Always show the button when in Survey mode (not baking)
					var archery = GetArcherySystem();
					int pointCount = archery?.BuildManager?.PointCount ?? 0;

					// Update the button state
					_surveyDoneBtn.Show();
					if (pointCount < 3)
					{
						_surveyDoneBtn.Text = "Place More Nodes...";
						_surveyDoneBtn.Modulate = new Color(1, 1, 1, 0.5f);
						_surveyDoneBtn.Disabled = true;
					}
					else
					{
						_surveyDoneBtn.Text = "Finish Survey";
						_surveyDoneBtn.Modulate = Colors.White;
						_surveyDoneBtn.Disabled = false;
					}
				}
			}
			else
			{
				_surveyDoneBtn.Hide();
			}
		}
	}

	public void ResetUI()
	{
		if (!IsNodeReady()) return;

		// Default clean slate
		_terrainPicker?.Hide();
		_surveyConfirmBtn?.Hide();
		_raiseBtn?.Hide();
		_lowerBtn?.Hide();
		_fillPanel?.Hide();
		_smoothingPanel?.Hide();

		UpdateSurveyButton(GetArcherySystem()?.BuildManager?.PointCount ?? 0);
	}

	private void UpdateSurveyButton(int pointCount)
	{
		var archery = GetArcherySystem();
		if (archery?.BuildManager == null)
		{
			// ArcherySystem not ready yet - just show the button with default text
			if (_surveyDoneBtn != null && _mainHUD?.CurrentTool == MainHUDController.BuildTool.Survey)
			{
				_surveyDoneBtn.Show();
				_surveyDoneBtn.Text = "Place More Nodes...";
				_surveyDoneBtn.Modulate = new Color(1, 1, 1, 0.5f);
				_surveyDoneBtn.Disabled = true;
			}
			return;
		}
		if (_surveyDoneBtn == null) return;

		// Note: _Process handles hiding this if we aren't in Survey mode
		// This function focuses on state/text when valid.

		_surveyDoneBtn.Show();
		_surveyDoneBtn.Modulate = Colors.White;

		bool isPicking = archery.BuildManager.IsPickingTerrain;

		if (isPicking)
		{
			_surveyDoneBtn.Text = "RESET SHAPE";
			_surveyDoneBtn.Modulate = Colors.Salmon;
			_surveyDoneBtn.Disabled = false;
		}
		else if (pointCount < 3)
		{
			_surveyDoneBtn.Text = "Place More Nodes...";
			_surveyDoneBtn.Modulate = new Color(1, 1, 1, 0.5f);
			_surveyDoneBtn.Disabled = true;
		}
		else
		{
			_surveyDoneBtn.Text = "Finish Survey";
			_surveyDoneBtn.Modulate = Colors.White;
			_surveyDoneBtn.Disabled = false;
		}

		// Final check: if main HUD says we aren't in survey, enforce hide
		if (_mainHUD != null && _mainHUD.CurrentTool != MainHUDController.BuildTool.Survey)
		{
			_surveyDoneBtn.Hide();
		}
	}

	private void OnSurveyDonePressed()
	{
		var archery = GetArcherySystem();
		if (archery?.BuildManager == null) return;

		if (archery.BuildManager.IsPickingTerrain)
		{
			archery.BuildManager.ClearSurvey();
			ResetUI();
			return;
		}

		_terrainPicker.Show();
		_surveyConfirmBtn.Show();
		_raiseBtn.Show();
		_lowerBtn.Show();
		_smoothingPanel.Show();
		UpdatePreview();
		UpdateSurveyButton(GetArcherySystem()?.BuildManager?.PointCount ?? 0);
	}

	private void UpdatePreview()
	{
		var archery = GetArcherySystem();
		if (archery?.BuildManager == null) return;
		int type = _terrainPicker.Selected;
		archery.BuildManager.SetPreviewTerrain(type);

		bool isHole = archery.BuildManager.CurrentElevation < 0;
		_fillPanel.Visible = isHole && (type == 4 || type == 5);
		_smoothingPanel.Show();

		_smoothingSlider.Value = archery.BuildManager.SmoothingIterations;
	}

	private void OnConfirmPressed()
	{
		var archery = GetArcherySystem();
		if (archery?.BuildManager == null) return;
		archery.BuildManager.BakeTerrain(_terrainPicker.Selected);
		archery.ExitBuildMode();
		ResetUI();
	}
}
