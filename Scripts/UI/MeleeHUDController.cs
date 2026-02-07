using Godot;
using Archery;

/// <summary>
/// HUD controller for melee combat. Mirrors ArcheryHUDController patterns.
/// </summary>
public partial class MeleeHUDController : Control
{
    private MeleeSystem _meleeSystem;

    private ProgressBar _swingBar;
    private ColorRect _accuracyMarker;
    private ColorRect _lockedPowerLine;
    private ColorRect _perfectLine;
    private Label _cooldownLabel;
    private Label _damageLabel;
    private PlayerController _player;

    public override void _Ready()
    {
        _swingBar = GetNodeOrNull<ProgressBar>("SwingContainer/PowerBar");
        _accuracyMarker = GetNodeOrNull<ColorRect>("SwingContainer/AccuracyMarker");
        _lockedPowerLine = GetNodeOrNull<ColorRect>("SwingContainer/LockedPowerLine");
        _perfectLine = GetNodeOrNull<ColorRect>("SwingContainer/PerfectLine");
        _cooldownLabel = GetNodeOrNull<Label>("CooldownLabel");
        _damageLabel = GetNodeOrNull<Label>("DamageLabel");

        // Initial state
        if (_lockedPowerLine != null) _lockedPowerLine.Visible = false;
        if (_accuracyMarker != null) _accuracyMarker.Visible = false;
        if (_cooldownLabel != null) _cooldownLabel.Visible = false;
        if (_damageLabel != null) _damageLabel.Visible = false;
    }

    public override void _ExitTree()
    {
        DisconnectFromSystem();
    }

    private void DisconnectFromSystem()
    {
        if (_meleeSystem != null)
        {
            _meleeSystem.SwingValuesUpdated -= OnSwingValuesUpdated;
            _meleeSystem.SwingComplete -= OnSwingComplete;
            _meleeSystem.CooldownUpdated -= OnCooldownUpdated;
            _meleeSystem.ModeChanged -= OnModeChanged;
            _meleeSystem = null;
        }
    }

    public void RegisterPlayer(PlayerController player)
    {
        _player = player;

        // Find system on THIS player
        DisconnectFromSystem();
        _meleeSystem = _player.GetNodeOrNull<MeleeSystem>("MeleeSystem");

        if (_meleeSystem != null)
        {
            _meleeSystem.SwingValuesUpdated += OnSwingValuesUpdated;
            _meleeSystem.SwingComplete += OnSwingComplete;
            _meleeSystem.CooldownUpdated += OnCooldownUpdated;
            _meleeSystem.ModeChanged += OnModeChanged;
        }

        UpdateHUDVisibility();
    }

    private void UpdateHUDVisibility()
    {
        if (_meleeSystem != null && _player != null)
        {
            // Only show if specifically in Melee state to prevent HUD overlap
            Visible = (_player.CurrentState == PlayerState.CombatMelee);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left)
            {
                _meleeSystem?.HandleInput();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right)
            {
                _meleeSystem?.CancelSwing();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void OnSwingValuesUpdated(float barValue, float power, int stateInt)
    {
        var state = (MeleeSystem.SwingState)stateInt;

        if (_swingBar != null)
        {
            _swingBar.Value = barValue;
        }

        // Hide accuracy marker (removed from 2-click system)
        if (_accuracyMarker != null)
        {
            _accuracyMarker.Visible = false;
        }

        // Show locked power line after power is locked
        if (_lockedPowerLine != null && _meleeSystem != null)
        {
            bool isLocked = (state == MeleeSystem.SwingState.Finishing || state == MeleeSystem.SwingState.Executing);
            _lockedPowerLine.Visible = isLocked;

            if (isLocked && _swingBar != null)
            {
                float barWidth = _swingBar.Size.X;
                float markerX = (power / 100f) * barWidth;
                _lockedPowerLine.Position = new Vector2(markerX - 2, _lockedPowerLine.Position.Y);
            }
        }
    }

    private void OnSwingComplete(float power, float accuracy, float damage)
    {
        if (_damageLabel != null)
        {
            _damageLabel.Text = $"DMG: {damage:F1}";
            _damageLabel.Visible = true;
        }
    }

    private void OnCooldownUpdated(float remaining, float total)
    {
        if (_cooldownLabel != null)
        {
            _cooldownLabel.Visible = remaining > 0;
            _cooldownLabel.Text = $"CD: {remaining:F1}s";
        }

        // Update bar to show cooldown progress
        if (_swingBar != null && total > 0 && remaining > 0)
        {
            _swingBar.Value = (1f - (remaining / total)) * 100f;
        }
    }

    private void OnModeChanged(bool inMeleeMode)
    {
        UpdateHUDVisibility();

        if (!inMeleeMode)
        {
            // Reset UI when exiting melee
            if (_lockedPowerLine != null) _lockedPowerLine.Visible = false;
            if (_accuracyMarker != null) _accuracyMarker.Visible = false;
            if (_cooldownLabel != null) _cooldownLabel.Visible = false;
            if (_damageLabel != null) _damageLabel.Visible = false;
        }
    }
}
