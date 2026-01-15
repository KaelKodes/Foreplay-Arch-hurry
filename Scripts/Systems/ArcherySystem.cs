using Godot;
using System;
using System.Collections.Generic;
using Archery;

namespace Archery
{
	public enum DrawStage
	{
		Idle,
		Drawing, // Power phase
		Aiming,  // Accuracy phase
		Executing,
		ShotComplete
	}

	public enum ArcheryShotMode
	{
		Standard, // 5 degrees
		Long,     // 25 degrees
		Max       // 45 degrees
	}

	public partial class ArcherySystem : Node
	{
		[Export] public PackedScene ArrowScene; // Assigned in _Ready or Editor
		public int ArrowCount { get; private set; } = 10;

		[Export] public float DrawSpeed = 1.0f;
		[Export] public NodePath ArrowPath;
		[Export] public NodePath CameraPath;
		[Export] public NodePath WindSystemPath;

		private ArrowController _arrow;
		private CameraController _camera;
		private WindSystem _windSystem;
		private StatsService _statsService;

		[Export] public bool CanDashWhileShooting { get; set; } = false;
		[Export] public bool CanJumpWhileShooting { get; set; } = false;
		[Export] public float ShootingMoveMultiplier { get; set; } = 0.3f;

		private BuildManager _buildManager;
		private ObjectPlacer _objectPlacer;

		public BuildManager BuildManager => _buildManager;
		public ObjectPlacer ObjectPlacer => _objectPlacer;
		public Stats PlayerStats => _statsService?.PlayerStats ?? new Stats { Power = 10, Control = 10, Touch = 10 };
		public Vector3 ChestOffset => new Vector3(0, 1.3f, 0);
		public Vector3 BallPosition => (_stage == DrawStage.Idle && _currentPlayer != null) ? _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f))) : ((_arrow != null) ? _arrow.GlobalPosition : Vector3.Zero);
		public Vector3 TeePosition { get; private set; } = Vector3.Zero;

		public void SetTeePosition(Vector3 pos) => TeePosition = pos;
		public void UpdateTeePosition(Vector3 pos) => TeePosition = pos;
		public void UpdatePinPosition(Vector3 pos) { /* Placeholder for future target logic */ }
		public BallLie GetCurrentLie() => new BallLie { PowerEfficiency = 1.0f, LaunchAngleBonus = 0.0f, SpinModifier = 1.0f };
		public float GetEstimatedPower() => 100.0f; // Placeholder
		public float AoAOffset => 0.0f; // Placeholder

		private DrawStage _stage = DrawStage.Idle;
		private float _timer = 0.0f;
		private bool _isReturnPhase = false;
		private float _lockedPower = -1.0f;
		private float _lockedAccuracy = -1.0f;
		private Vector2 _spinIntent = Vector2.Zero;
		private float _powerOverride = -1.0f;
		public DrawStage CurrentStage => _stage;
		private float _lastAdvanceTime = 0.0f;
		private long _lastInputFrame = -1;
		private ArcheryShotMode _currentMode = ArcheryShotMode.Standard;
		public ArcheryShotMode CurrentMode => _currentMode;
		private PlayerController _currentPlayer;
		private Node3D _currentTarget;
		public Node3D CurrentTarget => _currentTarget;

		[Signal] public delegate void DrawStageChangedEventHandler(int newStage);
		[Signal] public delegate void ArcheryValuesUpdatedEventHandler(float currentBarValue, float lockedPower, float lockedAccuracy);
		[Signal] public delegate void ShotResultEventHandler(float power, float accuracy);
		[Signal] public delegate void ModeChangedEventHandler(bool isCombat);
		[Signal] public delegate void PromptChangedEventHandler(bool visible, string message);
		[Signal] public delegate void ShotModeChangedEventHandler(int newMode);
		[Signal] public delegate void TargetChangedEventHandler(Node3D target);

		public override void _Ready()
		{
			var placeholder = GetNodeOrNull<PlayerController>("../PlayerPlaceholder");
			if (placeholder != null) RegisterPlayer(placeholder);

			// Load Arrow Scene resource if not assigned
			if (ArrowScene == null) ArrowScene = GD.Load<PackedScene>("res://Scenes/Entities/Arrow.tscn");

			// Remove the scene-resident arrow if it exists (we will spawn our own)
			if (ArrowPath != null)
			{
				var sceneArrow = GetNodeOrNull(ArrowPath);
				if (sceneArrow != null) sceneArrow.QueueFree();
			}

			if (CameraPath != null) _camera = GetNode<CameraController>(CameraPath);
			GD.Print($"ArcherySystem: Ready. Camera Found: {(_camera != null)}");
			if (WindSystemPath != null) _windSystem = GetNode<WindSystem>(WindSystemPath);

			_statsService = new StatsService();
			_statsService.Name = "StatsService";
			AddChild(_statsService);
			_statsService.LoadStats();

			_buildManager = new BuildManager();
			_buildManager.Name = "BuildManager";
			AddChild(_buildManager);

			_objectPlacer = new ObjectPlacer();
			_objectPlacer.Name = "ObjectPlacer";
			AddChild(_objectPlacer);

			CallDeferred(MethodName.ExitCombatMode);
			CallDeferred(MethodName.UpdateArrowLabel);
		}

		public void CollectArrow()
		{
			ArrowCount++;
			UpdateArrowLabel();
			// Play sound?
		}

		private void UpdateArrowLabel()
		{
			// Try to update the label via signal or direct access if HUD connects to us
			EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1); // Force HUD refresh? 
																	// Ideally HUD listens to a specific signal, but we'll use the existing value update or add a new one?
																	// Actually, let's just piggyback on prompt or add a specialized method if HUD has reference.
																	// For now, simpler: modify ArcheryHUDController to read ArrowCount.
		}

		// ...

		public void PrepareNextShot()
		{
			if (ArrowCount <= 0)
			{
				SetPrompt(true, "Out of Arrows!");
				return;
			}

			if (_arrow != null)
			{
				if (!_arrow.HasBeenShot)
				{
					_arrow.QueueFree();
				}
				_arrow = null;
			}

			// Spawn new arrow (decrement count when shot, not prep)
			if (ArrowScene != null)
			{
				_arrow = ArrowScene.Instantiate<ArrowController>();
				GetTree().CurrentScene.AddChild(_arrow);

				_arrow.Connect(ArrowController.SignalName.ArrowSettled, new Callable(this, MethodName.OnArrowSettled));
				_arrow.Connect(ArrowController.SignalName.ArrowCollected, new Callable(this, MethodName.CollectArrow));

				// Exclude player
				if (_currentPlayer != null) _arrow.SetCollisionException(_currentPlayer);

				_arrow.Visible = true;

				// Position it
				if (_currentPlayer != null)
				{
					Vector3 spawnPos = _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f)));
					_arrow.GlobalPosition = spawnPos;

					Vector3 rot = _currentPlayer.GlobalRotationDegrees;
					rot.Y += 180.0f;
					_arrow.GlobalRotationDegrees = rot;

					// Apply Player Color
					_arrow.SetColor(GetPlayerColor(_currentPlayer.PlayerIndex));
				}
			}

			_stage = DrawStage.Idle;

			_timer = 0.0f;

			_isReturnPhase = false;

			_lockedPower = -1.0f;

			_lockedAccuracy = -1.0f;


			if (_camera != null) { _camera.SetTarget(_currentPlayer, true); _camera.SetFollowing(false); _camera.SetFreeLook(false); }
			EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);

			UpdateArrowLabel();
		}

		private void UpdateArrowPose()
		{
			if (_arrow == null || _currentPlayer == null || _arrow.HasBeenShot) return;

			// Position it
			Vector3 spawnPos = _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f)));
			_arrow.GlobalPosition = spawnPos;

			Vector3 rot = _currentPlayer.GlobalRotationDegrees;
			rot.Y += 180.0f;
			_arrow.GlobalRotationDegrees = rot;
		}

		public void CancelDraw()
		{
			_stage = DrawStage.Idle;
			_timer = 0.0f;
			_isReturnPhase = false;
			_lockedPower = -1.0f;
			_lockedAccuracy = -1.0f;
			EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
		}

		public void ResetMatch()
		{
			// Refund current/last shot if it was just completed
			if (_stage == DrawStage.ShotComplete || _stage == DrawStage.Executing)
			{
				ArrowCount++;
			}

			_stage = DrawStage.Idle;
			_timer = 0.0f;
			_isReturnPhase = false;
			_lockedPower = -1.0f;
			_lockedAccuracy = -1.0f;

			// Clean up previous arrow and prepare a fresh one
			PrepareNextShot();

			EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
		}
		public void RegisterPlayer(PlayerController player)
		{
			_currentPlayer = player;
		}

		public void SetPrompt(bool visible, string message = "")
		{
			EmitSignal(SignalName.PromptChanged, visible, message);
		}

		public void ExitCombatMode()
		{
			_stage = DrawStage.Idle;
			if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
			if (_camera != null)
			{
				_camera.SetTarget(_currentPlayer, true); // Snap to player


			}
			EmitSignal(SignalName.ModeChanged, false);
			SetPrompt(false, "");
		}

		public void EnterCombatMode()
		{
			_stage = DrawStage.Idle;
			if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.CombatMode;

			EmitSignal(SignalName.ModeChanged, true);

			// Ensure an arrow is ready as soon as we enter combat
			if (_arrow == null || _arrow.HasBeenShot)
			{
				PrepareNextShot();
			}
		}

		public void EnterBuildMode()
		{
			_stage = DrawStage.Idle;
			if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.BuildMode;
			EmitSignal(SignalName.ModeChanged, false);
			SetPrompt(false);
		}

		public void ExitBuildMode()
		{
			if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
			SetPrompt(false);
		}

		public void CycleShotMode()
		{
			_currentMode = (ArcheryShotMode)(((int)_currentMode + 1) % 3);
			GD.Print($"[ArcherySystem] Shot Mode changed to: {_currentMode}");
			EmitSignal(SignalName.ShotModeChanged, (int)_currentMode);
		}

		public void CycleTarget()
		{
			if (_currentPlayer == null) return;

			// Simple proximity-based target cycle
			var targets = new System.Collections.Generic.List<Node3D>();
			var allInteractables = GetTree().GetNodesInGroup("interactables"); // Assuming they are in a group, otherwise we search all nodes in root

			// Fallback: If no group, we could find all InteractableObjects in scene
			// For now, let's assume we can search the scene for targetables
			FindTargetablesRecursive(GetTree().Root, targets);

			if (targets.Count == 0)
			{
				ClearTarget();
				return;
			}

			// Sort by proximity to player
			targets.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(_currentPlayer.GlobalPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(_currentPlayer.GlobalPosition)));

			int currentIndex = (_currentTarget != null) ? targets.IndexOf(_currentTarget) : -1;
			int nextIndex = (currentIndex + 1) % targets.Count;

			_currentTarget = targets[nextIndex];
			GD.Print($"[ArcherySystem] Target Locked: {_currentTarget.Name}");
			EmitSignal(SignalName.TargetChanged, _currentTarget);

			if (_camera != null && _camera is CameraController camCtrl)
			{
				camCtrl.SetLockedTarget(_currentTarget);
			}
		}

		private void FindTargetablesRecursive(Node node, System.Collections.Generic.List<Node3D> results)
		{
			if (node is InteractableObject io && io.IsTargetable)
			{
				results.Add(io);
			}

			foreach (Node child in node.GetChildren())
			{
				FindTargetablesRecursive(child, results);
			}
		}

		public void ClearTarget()
		{
			if (_currentTarget == null) return;
			GD.Print("[ArcherySystem] Target Cleared");
			_currentTarget = null;
			EmitSignal(SignalName.TargetChanged, null);

			if (_camera != null && _camera is CameraController camCtrl)
			{
				camCtrl.SetLockedTarget(null);
			}
		}

		private void OnArrowSettled(float distance)
		{
			SetPrompt(true, $"Shot settled: {distance * ArcheryConstants.UNIT_RATIO:F1}y");
		}

		public void HandleInput()
		{
			// Prevent multiple advances in the same frame
			long currentFrame = Engine.GetFramesDrawn();
			if (currentFrame == _lastInputFrame) return;
			_lastInputFrame = currentFrame;

			// Cooldown to prevent accidental double-clicks (e.g. 200ms)
			float currentTime = (float)(Time.GetTicksMsec() / 1000.0);
			if (currentTime - _lastAdvanceTime < 0.2f) return;
			_lastAdvanceTime = currentTime;

			if (_stage == DrawStage.Idle || _stage == DrawStage.ShotComplete)
			{
				if (_stage == DrawStage.ShotComplete) PrepareNextShot();
				GD.Print($"[ArcherySystem] {currentFrame} Phase 1: Start Drawing");
				_stage = DrawStage.Drawing;
				_timer = 0.0f;
				_isReturnPhase = false;
				_lockedPower = -1.0f;
				_lockedAccuracy = -1.0f;
				EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			}
			else if (_stage == DrawStage.Drawing)
			{
				// Lock Power
				_lockedPower = Mathf.PingPong(_timer * DrawSpeed * 100.0f, 100.0f);
				GD.Print($"[ArcherySystem] {currentFrame} Phase 2: Power Locked at {_lockedPower:F1}");
				_stage = DrawStage.Aiming;
				// Continue timer, do NOT reset. Bar will naturally hit 100 and come back for Aiming.
				EmitSignal(SignalName.ArcheryValuesUpdated, _lockedPower, _lockedPower, -1);
				EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			}
			else if (_stage == DrawStage.Aiming)
			{
				// Lock Accuracy
				_lockedAccuracy = Mathf.PingPong(_timer * DrawSpeed * 100.0f, 100.0f);
				GD.Print($"[ArcherySystem] {currentFrame} Phase 3: Accuracy Locked at {_lockedAccuracy:F1}");
				_stage = DrawStage.Executing;
				EmitSignal(SignalName.ArcheryValuesUpdated, _lockedAccuracy, _lockedPower, _lockedAccuracy);
				EmitSignal(SignalName.DrawStageChanged, (int)_stage);
				ExecuteShot();
			}
		}

		// Process loop for bar animation
		public override void _Process(double delta)
		{
			if (_stage == DrawStage.Idle || _stage == DrawStage.Drawing || _stage == DrawStage.Aiming)
			{
				UpdateArrowPose();
			}

			if (_stage == DrawStage.Drawing)
			{
				_timer += (float)delta;
				float val = Mathf.PingPong(_timer * DrawSpeed * 100.0f, 100.0f);
				EmitSignal(SignalName.ArcheryValuesUpdated, val, -1, -1);
			}
			else if (_stage == DrawStage.Aiming)
			{
				_timer += (float)delta;
				float val = Mathf.PingPong(_timer * DrawSpeed * 100.0f, 100.0f);
				EmitSignal(SignalName.ArcheryValuesUpdated, val, _lockedPower, -1);
			}
		}

		private void ExecuteShot()
		{
			// 1. Power Calculation (Incorporate Stats + Locked Power)
			float powerFactor = _lockedPower / 100.0f;
			float powerStatMult = PlayerStats.Power / 10.0f;

			// Apply Forgiveness (Snap to perfect)
			if (Mathf.Abs(_lockedPower - ArcheryConstants.PERFECT_POWER_VALUE) < ArcheryConstants.TOLERANCE_POWER)
			{
				_lockedPower = ArcheryConstants.PERFECT_POWER_VALUE;
				powerFactor = _lockedPower / 100.0f;
			}

			float velocityMag = ArcheryConstants.BASE_VELOCITY * powerFactor * powerStatMult;

			// 2. Accuracy Calculation
			// Perfect Target is 25.0 on the return trip.
			float accuracyError = _lockedAccuracy - ArcheryConstants.PERFECT_ACCURACY_VALUE;

			// Apply Forgiveness
			if (Mathf.Abs(accuracyError) < ArcheryConstants.TOLERANCE_ACCURACY)
			{
				accuracyError = 0.0f;
				_lockedAccuracy = ArcheryConstants.PERFECT_ACCURACY_VALUE;
			}

			// Power Multiplier for Error: Going over Perfect Power (94) amplifies accuracy errors
			if (_lockedPower > ArcheryConstants.PERFECT_POWER_VALUE)
			{
				float overPower = _lockedPower - ArcheryConstants.PERFECT_POWER_VALUE;
				accuracyError *= (1.0f + overPower * 0.15f); // 15% more error per point over perfect
			}

			if (_arrow != null)
			{
				Vector3 launchDir;
				if (_currentTarget != null)
				{
					// Snap aiming to target
					Vector3 targetPos = _currentTarget.GlobalPosition;
					if (_currentTarget is InteractableObject io)
					{
						// Aim for the center of the mesh if possible
						targetPos = io.GlobalPosition + new Vector3(0, 1.0f, 0); // Offset upwards slightly for signs
					}
					launchDir = (targetPos - _arrow.GlobalPosition).Normalized();
				}
				else
				{
					Vector3 camFwd = -_camera.GlobalBasis.Z;
					launchDir = (_camera != null) ? new Vector3(camFwd.X, 0, camFwd.Z).Normalized() : Vector3.Forward;
				}

				// Apply Loft based on Shot Mode
				float loftDeg = 12.0f;
				switch (_currentMode)
				{
					case ArcheryShotMode.Standard: loftDeg = 12.0f; break;
					case ArcheryShotMode.Long: loftDeg = 25.0f; break;
					case ArcheryShotMode.Max: loftDeg = 45.0f; break;
				}

				float loftRad = Mathf.DegToRad(loftDeg);
				launchDir.Y = Mathf.Sin(loftRad);
				launchDir = launchDir.Normalized();

				// Apply Accuracy Deviation (Left/Right)
				// User: "over 25 causes a right veering arrow, and after the line (lower than 25) to cause a left veering arrow"
				// accuracyError > 0 means lockedAccuracy > 25.0. 
				// Right veer in Godot (with -Z Forward) is a NEGATIVE rotation around Up axis.
				float rotationDeg = -accuracyError * 0.75f; // +/- 0.75 deg per unit error
				launchDir = launchDir.Rotated(Vector3.Up, Mathf.DegToRad(rotationDeg));

				// Apply Wind to Arrow before launch
				if (_windSystem != null && _windSystem.IsWindEnabled)
				{
					Vector3 wind = _windSystem.WindDirection * _windSystem.WindSpeedMph;
					_arrow.SetWind(wind);
				}
				else if (_arrow != null)
				{
					_arrow.SetWind(Vector3.Zero);
				}

				_arrow.Launch(launchDir * velocityMag, Vector3.Zero);

				if (_camera != null)
				{


				}
			}

			EmitSignal(SignalName.ShotResult, _lockedPower, _lockedAccuracy);
			ArrowCount--;
			UpdateArrowLabel();
			_stage = DrawStage.ShotComplete;
			EmitSignal(SignalName.DrawStageChanged, (int)_stage);
			GD.Print($"[ArcherySystem] Shot Executed. Power: {_lockedPower:F1}, Accuracy: {_lockedAccuracy:F1}, Error: {accuracyError:F2}");
		}

		private Color GetPlayerColor(int index)
		{
			switch (index % 4)
			{
				case 0: return Colors.Blue;
				case 1: return Colors.Red;
				case 2: return Colors.Green;
				case 3: return Colors.Yellow;
				default: return Colors.White;
			}
		}
	}
}
