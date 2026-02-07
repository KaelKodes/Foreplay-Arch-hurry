using Godot;

namespace Archery;

/// <summary>
/// MOBA-specific HUD overlay. Shows:
///   - Center-top: Wave timer + wave count
///   - Tower score flanking the timer
///   - Bottom-left: Player stats (HP bar, level, gold)
/// The existing Hotbar handles item slots (Sword/Bow/Hammer/Shovel).
/// </summary>
public partial class MobaHUD : CanvasLayer
{
	// Wave timer
	private Label _waveTimerLabel;
	private Label _waveCountLabel;
	private Panel _timerPanel;

	// Player stats
	private ProgressBar _playerHpBar;
	private Label _playerHpLabel;
	private Label _goldLabel;
	private Label _levelLabel;

	// Tower score
	private Label _redTowerLabel;
	private Label _blueTowerLabel;

	// References
	private MobaGameManager _gameManager;

	public override void _Ready()
	{
		Layer = 10; // Above other UI
		BuildUI();
		CallDeferred(nameof(FindGameManager));
	}

	private void FindGameManager()
	{
		_gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
	}

	private void BuildUI()
	{
		// ╔══════════════════════════════════════════════════╗
		// ║  CENTER TOP: Wave Timer                          ║
		// ╚══════════════════════════════════════════════════╝

		_timerPanel = new Panel();
		_timerPanel.AnchorLeft = 0.5f;
		_timerPanel.AnchorRight = 0.5f;
		_timerPanel.AnchorTop = 0f;
		_timerPanel.AnchorBottom = 0f;
		_timerPanel.OffsetLeft = -120;
		_timerPanel.OffsetRight = 120;
		_timerPanel.OffsetTop = 10;
		_timerPanel.OffsetBottom = 70;
		_timerPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

		var timerStyle = new StyleBoxFlat();
		timerStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
		timerStyle.CornerRadiusBottomLeft = 12;
		timerStyle.CornerRadiusBottomRight = 12;
		timerStyle.CornerRadiusTopLeft = 12;
		timerStyle.CornerRadiusTopRight = 12;
		timerStyle.BorderWidthBottom = 2;
		timerStyle.BorderWidthTop = 2;
		timerStyle.BorderWidthLeft = 2;
		timerStyle.BorderWidthRight = 2;
		timerStyle.BorderColor = new Color(0.4f, 0.5f, 0.7f, 0.6f);
		_timerPanel.AddThemeStyleboxOverride("panel", timerStyle);
		AddChild(_timerPanel);

		var timerVBox = new VBoxContainer();
		timerVBox.AnchorRight = 1f;
		timerVBox.AnchorBottom = 1f;
		timerVBox.Alignment = BoxContainer.AlignmentMode.Center;
		timerVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
		_timerPanel.AddChild(timerVBox);

		_waveTimerLabel = new Label();
		_waveTimerLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_waveTimerLabel.AddThemeFontSizeOverride("font_size", 24);
		_waveTimerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
		_waveTimerLabel.Text = "0:30";
		_waveTimerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		timerVBox.AddChild(_waveTimerLabel);

		_waveCountLabel = new Label();
		_waveCountLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_waveCountLabel.AddThemeFontSizeOverride("font_size", 12);
		_waveCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
		_waveCountLabel.Text = "WAVE 0";
		_waveCountLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		timerVBox.AddChild(_waveCountLabel);

		// Tower score flanking the timer
		_redTowerLabel = new Label();
		_redTowerLabel.AnchorLeft = 0.5f;
		_redTowerLabel.AnchorRight = 0.5f;
		_redTowerLabel.OffsetLeft = -200;
		_redTowerLabel.OffsetRight = -130;
		_redTowerLabel.OffsetTop = 20;
		_redTowerLabel.OffsetBottom = 50;
		_redTowerLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_redTowerLabel.AddThemeFontSizeOverride("font_size", 20);
		_redTowerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
		_redTowerLabel.Text = "🔴 2";
		_redTowerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_redTowerLabel);

		_blueTowerLabel = new Label();
		_blueTowerLabel.AnchorLeft = 0.5f;
		_blueTowerLabel.AnchorRight = 0.5f;
		_blueTowerLabel.OffsetLeft = 130;
		_blueTowerLabel.OffsetRight = 200;
		_blueTowerLabel.OffsetTop = 20;
		_blueTowerLabel.OffsetBottom = 50;
		_blueTowerLabel.HorizontalAlignment = HorizontalAlignment.Left;
		_blueTowerLabel.AddThemeFontSizeOverride("font_size", 20);
		_blueTowerLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 1f));
		_blueTowerLabel.Text = "2 🔵";
		_blueTowerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_blueTowerLabel);

		// ╔══════════════════════════════════════════════════╗
		// ║  BOTTOM LEFT: Player Stats                       ║
		// ╚══════════════════════════════════════════════════╝

		var statsPanel = new Panel();
		statsPanel.AnchorLeft = 0f;
		statsPanel.AnchorTop = 1f;
		statsPanel.AnchorBottom = 1f;
		statsPanel.OffsetLeft = 10;
		statsPanel.OffsetRight = 200;
		statsPanel.OffsetTop = -130;
		statsPanel.OffsetBottom = -10;
		statsPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

		var statsStyle = new StyleBoxFlat();
		statsStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.8f);
		statsStyle.CornerRadiusBottomLeft = 8;
		statsStyle.CornerRadiusBottomRight = 8;
		statsStyle.CornerRadiusTopLeft = 8;
		statsStyle.CornerRadiusTopRight = 8;
		statsPanel.AddThemeStyleboxOverride("panel", statsStyle);
		AddChild(statsPanel);

		var statsVBox = new VBoxContainer();
		statsVBox.AnchorRight = 1f;
		statsVBox.AnchorBottom = 1f;
		statsVBox.OffsetLeft = 10;
		statsVBox.OffsetTop = 8;
		statsVBox.OffsetRight = -10;
		statsVBox.OffsetBottom = -8;
		statsVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
		statsPanel.AddChild(statsVBox);

		_levelLabel = new Label();
		_levelLabel.Text = "LVL 1";
		_levelLabel.AddThemeFontSizeOverride("font_size", 16);
		_levelLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		_levelLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		statsVBox.AddChild(_levelLabel);

		_playerHpBar = new ProgressBar();
		_playerHpBar.MinValue = 0;
		_playerHpBar.MaxValue = 100;
		_playerHpBar.Value = 100;
		_playerHpBar.ShowPercentage = false;
		_playerHpBar.CustomMinimumSize = new Vector2(0, 20);

		var hpFill = new StyleBoxFlat();
		hpFill.BgColor = new Color(0.2f, 0.85f, 0.3f);
		hpFill.CornerRadiusBottomLeft = 4;
		hpFill.CornerRadiusBottomRight = 4;
		hpFill.CornerRadiusTopLeft = 4;
		hpFill.CornerRadiusTopRight = 4;
		_playerHpBar.AddThemeStyleboxOverride("fill", hpFill);

		var hpBg = new StyleBoxFlat();
		hpBg.BgColor = new Color(0.2f, 0.1f, 0.1f, 0.8f);
		hpBg.CornerRadiusBottomLeft = 4;
		hpBg.CornerRadiusBottomRight = 4;
		hpBg.CornerRadiusTopLeft = 4;
		hpBg.CornerRadiusTopRight = 4;
		_playerHpBar.AddThemeStyleboxOverride("background", hpBg);
		statsVBox.AddChild(_playerHpBar);

		_playerHpLabel = new Label();
		_playerHpLabel.Text = "100 / 100";
		_playerHpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_playerHpLabel.AddThemeFontSizeOverride("font_size", 12);
		_playerHpLabel.AddThemeColorOverride("font_color", Colors.White);
		_playerHpLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		statsVBox.AddChild(_playerHpLabel);

		_goldLabel = new Label();
		_goldLabel.Text = "💰 0";
		_goldLabel.AddThemeFontSizeOverride("font_size", 16);
		_goldLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
		_goldLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		statsVBox.AddChild(_goldLabel);
	}

	public override void _Process(double delta)
	{
		if (_gameManager == null)
		{
			_gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
			if (_gameManager == null) return;
		}

		UpdateWaveTimer();
		UpdateTowerScore();
	}

	private void UpdateWaveTimer()
	{
		int wave = _gameManager.GetWaveCount();
		_waveCountLabel.Text = $"WAVE {wave}";

		float timeLeft = _gameManager.WaveTimeRemaining;
		int minutes = (int)(timeLeft / 60f);
		int seconds = (int)(timeLeft % 60f);
		_waveTimerLabel.Text = $"{minutes}:{seconds:D2}";
	}

	private void UpdateTowerScore()
	{
		int redTowers = _gameManager.GetTowerCount(MobaTeam.Red);
		int blueTowers = _gameManager.GetTowerCount(MobaTeam.Blue);
		_redTowerLabel.Text = $"🔴 {redTowers}";
		_blueTowerLabel.Text = $"{blueTowers} 🔵";
	}
}
