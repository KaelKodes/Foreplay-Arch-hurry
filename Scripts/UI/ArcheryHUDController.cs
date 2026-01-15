using Godot;
using System;
using Archery;

public partial class ArcheryHUDController : Control
{
	[Export] public NodePath ArcherySystemPath;
	[Export] public NodePath WindSystemPath;

	private ArcherySystem _archerySystem;
	private WindSystem _windSystem;

	private ProgressBar _drawBar;
	private ColorRect _accuracyMarker;
	private ColorRect _lockedPowerLine;
	private ColorRect _lockedAccuracyLine;

	private Button _btnNextShot;
	private Label _arrowsLabel;
	private Label _distanceLabel;
	private Label _speedLabel;
	private SpinBox _powerOverrideSpin;

	private TextureRect _windArrow;
	private Label _windLabel;
	private Label _modeLabel;
	private Button _toggleWindBtn;
	private SpinBox _windSpeedSpin;

	public override void _Ready()
	{
		_archerySystem = GetNodeOrNull<ArcherySystem>(ArcherySystemPath);
		if (_archerySystem == null) _archerySystem = GetTree().CurrentScene.FindChild("ArcherySystem", true, false) as ArcherySystem;
		_windSystem = GetNodeOrNull<WindSystem>(WindSystemPath);
		if (_windSystem == null) _windSystem = GetTree().CurrentScene.FindChild("WindSystem", true, false) as WindSystem;

		_drawBar = GetNodeOrNull<ProgressBar>("SwingContainer/PowerBar");
		_accuracyMarker = GetNodeOrNull<ColorRect>("SwingContainer/AccuracyMarker");
		_lockedPowerLine = GetNodeOrNull<ColorRect>("SwingContainer/LockedPowerLine");
		_lockedAccuracyLine = GetNodeOrNull<ColorRect>("SwingContainer/LockedAccuracyLine");

		_windArrow = GetNodeOrNull<TextureRect>("WindContainer/WindArrow");
		_windLabel = GetNodeOrNull<Label>("WindContainer/WindLabel");
		_toggleWindBtn = GetNodeOrNull<Button>("StatsPanel/ToggleWindBtn");
		_windSpeedSpin = GetNodeOrNull<SpinBox>("WindContainer/WindSpeedSpin");

		_btnNextShot = GetNodeOrNull<Button>("SwingContainer/BtnNextShot");
		_arrowsLabel = GetNodeOrNull<Label>("StatsPanel/StrokeLabel"); // Reusing label name for now
		_distanceLabel = GetNodeOrNull<Label>("StatsPanel/DistanceLabel");
		_speedLabel = GetNodeOrNull<Label>("StatsPanel/SpeedLabel");
		_modeLabel = GetNodeOrNull<Label>("StatsPanel/ClubLabel"); // Reusing "ClubLabel" for Mode

		// Connect Signals
		if (_archerySystem != null)
		{
			_archerySystem.Connect(ArcherySystem.SignalName.ArcheryValuesUpdated, new Callable(this, MethodName.OnArcheryValuesUpdated));
			_archerySystem.Connect(ArcherySystem.SignalName.ShotResult, new Callable(this, MethodName.OnShotResult));
			_archerySystem.Connect(ArcherySystem.SignalName.ShotModeChanged, new Callable(this, MethodName.OnShotModeChanged));

			// Update initial mode display
			OnShotModeChanged((int)_archerySystem.CurrentMode);

			// Connect to Arrow signals via ArcherySystem reference
			if (_archerySystem.ArrowPath != null)
			{
				var arrow = _archerySystem.GetNodeOrNull<ArrowController>(_archerySystem.ArrowPath);
				if (arrow != null)
				{
					arrow.Connect(ArrowController.SignalName.ArrowSettled, new Callable(this, MethodName.OnArrowSettled));
					arrow.Connect(ArrowController.SignalName.ArrowCarried, new Callable(this, MethodName.OnArrowCarried));
				}
			}
		}

		if (_windSystem != null)
		{
			_windSystem.WindChanged += OnWindChanged;
			OnWindChanged(_windSystem.WindDirection, _windSystem.WindSpeedMph);

			if (_toggleWindBtn != null)
			{
				_toggleWindBtn.Pressed += OnToggleWindPressed;
				UpdateWindToggleBtnText();
			}
			if (_windSpeedSpin != null)
			{
				_windSpeedSpin.ValueChanged += (val) => _windSystem.SetWindSpeed((float)val);
			}

			// Connect Direction Grid
			var grid = GetNodeOrNull<GridContainer>("WindContainer/WindDirGrid");
			if (grid != null)
			{
				foreach (Node child in grid.GetChildren())
				{
					if (child is Button btn)
					{
						btn.Pressed += () => OnWindDirButtonPressed(btn.Text);
					}
				}
			}
		}

		var resetBtn = GetNodeOrNull<Button>("StatsPanel/ResetBtn");
		if (resetBtn != null) resetBtn.Pressed += () => _archerySystem?.ResetMatch();

		var exitBtn = GetNodeOrNull<Button>("StatsPanel/ExitGolfBtn");
		if (exitBtn != null) exitBtn.Pressed += () => _archerySystem?.ExitCombatMode();

		if (_btnNextShot != null) _btnNextShot.Pressed += OnNextShotPressed;
	}

	private void OnToggleWindPressed()
	{
		if (_windSystem == null) return;
		_windSystem.ToggleWind();
		UpdateWindToggleBtnText();
	}

	private void UpdateWindToggleBtnText()
	{
		if (_toggleWindBtn != null && _windSystem != null)
		{
			_toggleWindBtn.Text = $"Wind: {(_windSystem.IsWindEnabled ? "ON" : "OFF")}";
		}
	}

	private void OnWindDirButtonPressed(string dirName)
	{
		if (_windSystem == null) return;
		Vector3 dir = Vector3.Forward;
		switch (dirName)
		{
			case "N": dir = Vector3.Back; break; // Reversed from Forward to match user INTENT (North Wind = FROM North)
			case "S": dir = Vector3.Forward; break;
			case "E": dir = Vector3.Left; break; // East Wind = FROM East
			case "W": dir = Vector3.Right; break;
			case "NW": dir = (Vector3.Back + Vector3.Right).Normalized(); break;
			case "NE": dir = (Vector3.Back + Vector3.Left).Normalized(); break;
			case "SW": dir = (Vector3.Forward + Vector3.Right).Normalized(); break;
			case "SE": dir = (Vector3.Forward + Vector3.Left).Normalized(); break;
		}
		_windSystem.SetWindDirection(dir);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;

		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			// Check if click is on UI
			if (IsPointOnInteractionUI(mouseBtn.Position)) return;

			_archerySystem?.HandleInput();
			GetViewport().SetInputAsHandled();
		}
	}

	private bool IsPointOnInteractionUI(Vector2 pos)
	{
		var statsPanel = GetNodeOrNull<Control>("StatsPanel");
		if (statsPanel != null && statsPanel.GetGlobalRect().HasPoint(pos)) return true;

		var windContainer = GetNodeOrNull<Control>("WindContainer");
		if (windContainer != null && windContainer.GetGlobalRect().HasPoint(pos)) return true;

		if (_btnNextShot != null && _btnNextShot.Visible && _btnNextShot.GetGlobalRect().HasPoint(pos)) return true;
		return false;
	}

	private void OnNextShotPressed()
	{
		if (_btnNextShot != null) _btnNextShot.Visible = false;
		_archerySystem?.PrepareNextShot();
	}

	private void OnWindChanged(Vector3 direction, float speedMph)
	{
		if (_windSystem == null) return;

		if (_windLabel != null)
		{
			_windLabel.Text = _windSystem.IsWindEnabled ? $"{speedMph:F0} mph" : "OFF";
			_windLabel.Modulate = _windSystem.IsWindEnabled ? Colors.White : new Color(1, 1, 1, 0.5f);
		}

		if (_windArrow != null)
		{
			_windArrow.Visible = _windSystem.IsWindEnabled;
			_windArrow.Rotation = Mathf.Atan2(-direction.X, direction.Z);
		}
	}

	private void OnShotResult(float power, float accuracy)
	{
		GD.Print($"ArcheryHUD: Result Power={power}, Acc={accuracy}");
	}

	private void OnShotModeChanged(int newModeIndex)
	{
		if (_modeLabel != null)
		{
			ArcheryShotMode mode = (ArcheryShotMode)newModeIndex;
			_modeLabel.Text = $"Mode: {mode}";
		}
	}

	private void OnArrowCarried(float distance)
	{
		if (_distanceLabel != null) _distanceLabel.Text = $"Carry: {distance * ArcheryConstants.UNIT_RATIO:F1}y";
	}

	private void OnArrowSettled(float distance)
	{
		if (_distanceLabel != null) _distanceLabel.Text = $"Total: {distance * ArcheryConstants.UNIT_RATIO:F1}y";
		if (_btnNextShot != null) _btnNextShot.Visible = true;
	}

	private void OnArcheryValuesUpdated(float currentBarValue, float lockedPower, float lockedAccuracy)
	{
		if (_drawBar == null) return;

		_drawBar.Value = currentBarValue;
		float parentWidth = _drawBar.Size.X;

		if (lockedAccuracy < 0 && _accuracyMarker != null)
		{
			float ratio = currentBarValue / 100.0f;
			_accuracyMarker.Position = new Vector2(ratio * parentWidth - (_accuracyMarker.Size.X / 2.0f), _accuracyMarker.Position.Y);
			_accuracyMarker.Visible = true;
		}
		else if (_accuracyMarker != null) _accuracyMarker.Visible = false;

		if (_lockedPowerLine != null)
		{
			_lockedPowerLine.Visible = (lockedPower >= 0);
			if (lockedPower >= 0)
			{
				// Thicken the line
				_lockedPowerLine.Size = new Vector2(4, _lockedPowerLine.Size.Y);

				float ratio = lockedPower / 100.0f;
				_lockedPowerLine.Position = new Vector2(ratio * parentWidth - (_lockedPowerLine.Size.X / 2.0f), _lockedPowerLine.Position.Y);

				bool isPerfectPower = Mathf.Abs(lockedPower - ArcheryConstants.PERFECT_POWER_VALUE) <= ArcheryConstants.TOLERANCE_POWER;
				_lockedPowerLine.Modulate = isPerfectPower ? Colors.Green : Colors.White;
			}
		}

		if (_lockedAccuracyLine != null)
		{
			_lockedAccuracyLine.Visible = (lockedAccuracy >= 0);
			if (lockedAccuracy >= 0)
			{
				// Thicken the line
				_lockedAccuracyLine.Size = new Vector2(4, _lockedAccuracyLine.Size.Y);

				float ratio = lockedAccuracy / 100.0f;
				_lockedAccuracyLine.Position = new Vector2(ratio * parentWidth - (_lockedAccuracyLine.Size.X / 2.0f), _lockedAccuracyLine.Position.Y);

				bool isPerfectAcc = Mathf.Abs(lockedAccuracy - ArcheryConstants.PERFECT_ACCURACY_VALUE) <= ArcheryConstants.TOLERANCE_ACCURACY;
				_lockedAccuracyLine.Modulate = isPerfectAcc ? Colors.Green : Colors.White;
			}
		}

		if (_arrowsLabel != null && _archerySystem != null)
		{
			_arrowsLabel.Text = $"Arrows: {_archerySystem.ArrowCount}";
		}
	}
}
