using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// MOBA-specific HUD overlay. Shows:
///   - Center-top: Wave timer + wave count
///   - Tower score flanking the timer
///   - Bottom-left: Player stats (HP, Stamina, Mana/Fury, XP, Level, Gold)
/// All styling uses MobaTheme for visual consistency.
/// Hero Status panel is visible only in RPG mode.
/// </summary>
public partial class MobaHUD : CanvasLayer
{
	// Wave timer
	private Label _waveTimerLabel;
	private Label _waveCountLabel;
	private Panel _timerPanel;

	// Player stats
	private Panel _statsPanel;
	private ProgressBar _playerHpBar;
	private Label _playerHpLabel;
	private ProgressBar _staminaBar;
	private Label _staminaLabel;
	private ProgressBar _secondaryBar;  // Mana or Fury
	private Label _secondaryLabel;
	private ProgressBar _xpBar;
	private Label _goldLabel;
	private Label _levelLabel;

	// Perk Choice UI
	private Panel _perkChoicePanel;
	private HBoxContainer _perkContainer;
	private StatsService _subscribedStatsService;
	private List<AbilityPerk> _currentPerks = new List<AbilityPerk>();

	public bool IsSelectingPerk => _perkChoicePanel?.Visible ?? false;

	// Tower score
	private Label _redTowerLabel;
	private Label _blueTowerLabel;

	// References
	private MobaGameManager _gameManager;

	// State
	private string _heroClass = "Ranger";  // Default; updated externally
	private bool _isRpgMode = false;
	private float _uiUpdateTimer = 0f;
	private const float UiUpdateInterval = 0.05f; // 20Hz is enough for UI

	public override void _Ready()
	{
		Layer = 10;
		BuildUI();
		BuildPerkUI();
		CallDeferred(nameof(FindGameManager));
		ConnectModeSignal();
	}

	private void FindGameManager()
	{
		_gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
	}

	private void ConnectModeSignal()
	{
		if (ToolManager.Instance != null)
		{
			ToolManager.Instance.HotbarModeChanged += OnHotbarModeChanged;
			// Sync initial state
			_isRpgMode = ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;
			UpdateStatsVisibility();
		}
	}

	private void OnHotbarModeChanged(int newMode)
	{
		_isRpgMode = (ToolManager.HotbarMode)newMode == ToolManager.HotbarMode.RPG;
		UpdateStatsVisibility();
	}

	private void UpdateStatsVisibility()
	{
		if (_statsPanel != null) _statsPanel.Visible = _isRpgMode;
	}

	/// <summary>Set the current hero class to determine which secondary resource bar to show.</summary>
	public void SetHeroClass(string heroClass)
	{
		_heroClass = heroClass?.ToLower() ?? "ranger";
		UpdateSecondaryBarStyle();
	}

	private void UpdateSecondaryBarStyle()
	{
		if (_secondaryBar == null || _secondaryLabel == null) return;

		bool isFury = _heroClass == "warrior";
		_secondaryBar.AddThemeStyleboxOverride("fill",
			MobaTheme.CreateBarFill(isFury ? MobaTheme.FuryFill : MobaTheme.ManaFill));
		_secondaryBar.AddThemeStyleboxOverride("background",
			MobaTheme.CreateBarBg(isFury ? MobaTheme.FuryBg : MobaTheme.ManaBg));
		_secondaryLabel.Text = isFury ? "-- / -- Fury" : "-- / -- MP";
	}

	private void BuildUI()
	{
		// ╔══════════════════════════════════════════════════════╗
		// ║  CENTER TOP: Wave Timer                              ║
		// ╚══════════════════════════════════════════════════════╝

		_timerPanel = MobaTheme.CreatePanel();
		_timerPanel.AnchorLeft = 0.5f;
		_timerPanel.AnchorRight = 0.5f;
		_timerPanel.AnchorTop = 0f;
		_timerPanel.AnchorBottom = 0f;
		_timerPanel.OffsetLeft = -120;
		_timerPanel.OffsetRight = 120;
		_timerPanel.OffsetTop = 10;
		_timerPanel.OffsetBottom = 70;
		AddChild(_timerPanel);

		var timerVBox = new VBoxContainer();
		timerVBox.AnchorRight = 1f;
		timerVBox.AnchorBottom = 1f;
		timerVBox.Alignment = BoxContainer.AlignmentMode.Center;
		timerVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
		_timerPanel.AddChild(timerVBox);

		_waveTimerLabel = MobaTheme.CreateHeroLabel("0:30", MobaTheme.AccentGold);
		_waveTimerLabel.HorizontalAlignment = HorizontalAlignment.Center;
		timerVBox.AddChild(_waveTimerLabel);

		_waveCountLabel = MobaTheme.CreateBodyLabel("WAVE 0", MobaTheme.TextSecondary);
		_waveCountLabel.HorizontalAlignment = HorizontalAlignment.Center;
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

		// ╔══════════════════════════════════════════════════════╗
		// ║  BOTTOM LEFT: Hero Stats (RPG Mode Only)             ║
		// ╚══════════════════════════════════════════════════════╝

		_statsPanel = MobaTheme.CreatePanel();
		_statsPanel.AnchorLeft = 0f;
		_statsPanel.AnchorTop = 1f;
		_statsPanel.AnchorBottom = 1f;
		_statsPanel.OffsetLeft = 10;
		_statsPanel.OffsetRight = 220;
		_statsPanel.OffsetTop = -220;
		_statsPanel.OffsetBottom = -10;
		_statsPanel.Visible = _isRpgMode;
		AddChild(_statsPanel);

		var statsVBox = new VBoxContainer();
		statsVBox.AnchorRight = 1f;
		statsVBox.AnchorBottom = 1f;
		statsVBox.OffsetLeft = 10;
		statsVBox.OffsetTop = 8;
		statsVBox.OffsetRight = -10;
		statsVBox.OffsetBottom = -8;
		statsVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
		statsVBox.AddThemeConstantOverride("separation", 3);
		_statsPanel.AddChild(statsVBox);

		// Level
		_levelLabel = MobaTheme.CreateHeadingLabel("LVL -", MobaTheme.AccentGold);
		statsVBox.AddChild(_levelLabel);

		// HP Bar
		_playerHpBar = MobaTheme.CreateHpBar(100f);
		statsVBox.AddChild(_playerHpBar);

		_playerHpLabel = MobaTheme.CreateBodyLabel("-- / -- HP", MobaTheme.TextPrimary);
		_playerHpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statsVBox.AddChild(_playerHpLabel);

		// Stamina Bar (all heroes)
		_staminaBar = MobaTheme.CreateStaminaBar(100f);
		statsVBox.AddChild(_staminaBar);

		_staminaLabel = MobaTheme.CreateBodyLabel("-- / -- SP", MobaTheme.TextSecondary);
		_staminaLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statsVBox.AddChild(_staminaLabel);

		// Secondary Resource (Mana or Fury)
		_secondaryBar = MobaTheme.CreateManaBar(100f);  // default to Mana style
		statsVBox.AddChild(_secondaryBar);

		_secondaryLabel = MobaTheme.CreateBodyLabel("-- / --", MobaTheme.TextSecondary);
		_secondaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statsVBox.AddChild(_secondaryLabel);

		// XP Bar (thin)
		_xpBar = MobaTheme.CreateXpBar(100f);
		statsVBox.AddChild(_xpBar);

		// Gold
		_goldLabel = MobaTheme.CreateHeadingLabel("💰 0", MobaTheme.AccentGold);
		statsVBox.AddChild(_goldLabel);

		// Apply class-specific secondary bar styling
		UpdateSecondaryBarStyle();
	}

	private void BuildPerkUI()
	{
		_perkChoicePanel = MobaTheme.CreatePanel();
		_perkChoicePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_perkChoicePanel.OffsetLeft = -300;
		_perkChoicePanel.OffsetRight = 300;
		_perkChoicePanel.OffsetTop = -150;
		_perkChoicePanel.OffsetBottom = 150;
		_perkChoicePanel.Visible = false;
		_perkChoicePanel.MouseFilter = Control.MouseFilterEnum.Stop;

		// Background to ensure buttons are clickable
		var bgNode = new ColorRect();
		bgNode.Color = new Color(0, 0, 0, 0.4f);
		bgNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgNode.MouseFilter = Control.MouseFilterEnum.Stop;
		_perkChoicePanel.AddChild(bgNode);

		AddChild(_perkChoicePanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		_perkChoicePanel.AddChild(vbox);

		var title = MobaTheme.CreateHeadingLabel("CHOOSE A PERK", MobaTheme.AccentGold);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(title);

		_perkContainer = new HBoxContainer();
		_perkContainer.Alignment = BoxContainer.AlignmentMode.Center;
		_perkContainer.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(_perkContainer);
	}

	public void OnAbilityUpgraded(int slot, int level, bool perkTriggered)
	{
		if (perkTriggered)
		{
			ShowPerkOptions(slot);
		}
	}

	private void ShowPerkOptions(int slot)
	{
		// Clear old
		foreach (Node child in _perkContainer.GetChildren()) child.QueueFree();
		_currentPerks.Clear();

		_currentPerks = PerkRegistry.GetRandomPerks(_heroClass, "", 3);
		for (int i = 0; i < _currentPerks.Count; i++)
		{
			var perk = _currentPerks[i];
			var btn = new Button();
			btn.Name = perk.Id;
			btn.CustomMinimumSize = new Vector2(180, 220);
			btn.MouseFilter = Control.MouseFilterEnum.Stop;

			var cardVBox = new VBoxContainer();
			cardVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			cardVBox.Alignment = BoxContainer.AlignmentMode.Center;
			cardVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
			btn.AddChild(cardVBox);

			var pIndex = MobaTheme.CreateHeroLabel($"{i + 1}", MobaTheme.TextMuted);
			pIndex.HorizontalAlignment = HorizontalAlignment.Center;
			pIndex.AddThemeFontSizeOverride("font_size", 40);
			cardVBox.AddChild(pIndex);

			var pName = MobaTheme.CreateHeadingLabel(perk.Name, MobaTheme.TextPrimary);
			pName.HorizontalAlignment = HorizontalAlignment.Center;
			pName.MouseFilter = Control.MouseFilterEnum.Ignore;
			cardVBox.AddChild(pName);

			var pDesc = MobaTheme.CreateBodyLabel(perk.Description, MobaTheme.TextSecondary);
			pDesc.HorizontalAlignment = HorizontalAlignment.Center;
			pDesc.AutowrapMode = TextServer.AutowrapMode.Word;
			pDesc.MouseFilter = Control.MouseFilterEnum.Ignore;
			cardVBox.AddChild(pDesc);

			btn.Pressed += () => SelectPerk(perk);
			_perkContainer.AddChild(btn);
		}

		_perkChoicePanel.Visible = true;
	}
	private void SelectPerk(AbilityPerk perk)
	{
		GD.Print($"[PerkUI] Selected: {perk.Name}");
		_perkChoicePanel.Visible = false;

		if (_subscribedStatsService != null)
		{
			_subscribedStatsService.SelectPerk(perk.Id);
		}
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

		// Lazy-connect mode signal if ToolManager wasn't ready at _Ready time
        if (ToolManager.Instance != null && !_modeSignalConnected)
        {
            ConnectModeSignal();
            _modeSignalConnected = true;
        }

        // Throttled UI polling (20Hz)
        _uiUpdateTimer -= (float)delta;
        if (_uiUpdateTimer <= 0)
        {
            _uiUpdateTimer = UiUpdateInterval;
            PollPlayerStats();
        }
    }

    private bool _modeSignalConnected = false;

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

    // ── Public update methods ──

    public void UpdateHp(float current, float max)
    {
        if (_playerHpBar != null) { _playerHpBar.MaxValue = max; _playerHpBar.Value = current; }
        if (_playerHpLabel != null) _playerHpLabel.Text = $"{(int)current} / {(int)max} HP";
    }

    public void UpdateStamina(float current, float max)
    {
        if (_staminaBar != null) { _staminaBar.MaxValue = max; _staminaBar.Value = current; }
        if (_staminaLabel != null) _staminaLabel.Text = $"{(int)current} / {(int)max} SP";
    }

    public void UpdateSecondaryResource(float current, float max)
    {
        bool isFury = _heroClass == "warrior";
        string suffix = isFury ? "Fury" : "MP";
        if (_secondaryBar != null) { _secondaryBar.MaxValue = max; _secondaryBar.Value = current; }
        if (_secondaryLabel != null) _secondaryLabel.Text = $"{(int)current} / {(int)max} {suffix}";
    }

    public void UpdateXp(float current, float max)
    {
        if (_xpBar != null) { _xpBar.MaxValue = max; _xpBar.Value = current; }
    }

    public void UpdateGold(int gold)
    {
        if (_goldLabel != null) _goldLabel.Text = $"💰 {gold}";
    }

    public void UpdateLevel(int level)
    {
        if (_levelLabel != null) _levelLabel.Text = $"LVL {level}";
    }

    // ── Live Stats Polling ──

    private Stats _cachedStats;
    private ArcherySystem _cachedArcherySystem;
    private bool _statsSearched = false;

    private void PollPlayerStats()
    {
        if (!_isRpgMode) return;

        // Reliable lookup via local_player group
        if (_cachedStats == null)
        {
            var player = GetTree().GetFirstNodeInGroup("local_player") as Node;
            var archerySystem = player?.FindChild("ArcherySystem", true, false) as ArcherySystem;

            if (archerySystem == null && player != null)
            {
                // Fallback: search by type if name check fails
                foreach (var child in player.GetChildren())
                {
                    if (child is ArcherySystem asys)
                    {
                        archerySystem = asys;
                        break;
                    }
                }
            }

            if (archerySystem != null)
            {
                _cachedStats = archerySystem.PlayerStats;
                _subscribedStatsService = archerySystem.PlayerStatsService;
                if (_subscribedStatsService != null)
                {
                    // Avoid double sub
                    _subscribedStatsService.AbilityUpgraded -= OnAbilityUpgraded;
                    _subscribedStatsService.AbilityUpgraded += OnAbilityUpgraded;
                }
                GD.Print("[MobaHUD] Linked to local player stats.");

                if (ToolManager.Instance != null)
                {
                    var heroClass = ToolManager.Instance.CurrentHeroClass;
                    if (!string.IsNullOrEmpty(heroClass))
                        SetHeroClass(heroClass);
                }
            }
        }

        if (_cachedStats == null) return;

        UpdateHp(_cachedStats.CurrentHealth, _cachedStats.MaxHealth);
        UpdateStamina(_cachedStats.CurrentStamina, _cachedStats.MaxStamina);

        bool isFury = _heroClass == "warrior";
        if (isFury)
            UpdateSecondaryResource(_cachedStats.CurrentFury, _cachedStats.MaxFury);
        else
            UpdateSecondaryResource(_cachedStats.CurrentMana, _cachedStats.MaxMana);

        UpdateLevel(_cachedStats.Level);
        UpdateGold(_cachedStats.Gold);

        int xpForNext = GetRequiredXpForCurrentLevel(_cachedStats.Level);
        UpdateXp(_cachedStats.Experience, xpForNext);
    }

    private int GetRequiredXpForCurrentLevel(int level)
    {
        switch (level)
        {
            case 1: return 480;
            case 2: return 960;
            case 3: return 1600;
            case 4: return 2400;
            case 5: return 3600;
            default: return 1000 * level;
        }
    }
}
