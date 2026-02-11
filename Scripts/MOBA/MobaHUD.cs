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
	private ProgressBar _shieldBar;
	private Label _playerHpLabel;
	private ProgressBar _staminaBar;
	private Label _staminaLabel;
	private ProgressBar _secondaryBar;  // Mana or Fury
	private Label _secondaryLabel;
	private ProgressBar _xpBar;
	private Label _goldLabel;
	private Label _levelLabel;

	// Shop
	private ShopUI _shopUI;
	private Button _shopButton;

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
		BuildShop();
		CallDeferred(nameof(FindGameManager));
		ConnectModeSignal();
	}

	private void BuildShop()
	{
		_shopUI = new ShopUI();
		_shopUI.Name = "ShopUI";
		AddChild(_shopUI);
	}

	public void ToggleShop()
	{
		_shopUI?.Toggle();
	}

	public bool IsShopOpen => _shopUI?.IsShopVisible ?? false;

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
		_statsPanel.OffsetTop = -190;
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

		// HP Bar (first in stats panel)

		// HP Bar
		_playerHpBar = MobaTheme.CreateHpBar(100f);
		statsVBox.AddChild(_playerHpBar);

		// Overlay Shield Bar
		_shieldBar = MobaTheme.CreateHpBar(0f);
		_shieldBar.AddThemeStyleboxOverride("fill", MobaTheme.CreateBarFill(MobaTheme.ShieldFill));
		_shieldBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		_shieldBar.MouseFilter = Control.MouseFilterEnum.Ignore;
		_playerHpBar.AddChild(_shieldBar);
		_shieldBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_shieldBar.Position = Vector2.Zero;
		_shieldBar.GrowHorizontal = Control.GrowDirection.Both;
		_shieldBar.GrowVertical = Control.GrowDirection.Both;

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

		// Level (bottom of stats, after XP bar)
		_levelLabel = MobaTheme.CreateHeadingLabel("Level -", MobaTheme.AccentGold);
		_levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statsVBox.AddChild(_levelLabel);

		// Gold - attached to the InventoryPanel (top-right)
		_goldLabel = MobaTheme.CreateHeadingLabel("💰 0", MobaTheme.AccentGold);
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;

		// Shop button — next to gold label
		_shopButton = new Button();
		_shopButton.Text = "🛒";
		_shopButton.AddThemeFontSizeOverride("font_size", 16);
		_shopButton.AddThemeColorOverride("font_color", MobaTheme.AccentGold);
		var shopBtnStyle = new StyleBoxFlat();
		shopBtnStyle.BgColor = new Color(0.1f, 0.1f, 0.16f, 0.85f);
		MobaTheme.SetCorners(shopBtnStyle, 4);
		MobaTheme.SetBorder(shopBtnStyle, 1, MobaTheme.AccentGold);
		shopBtnStyle.ContentMarginLeft = 6;
		shopBtnStyle.ContentMarginRight = 6;
		shopBtnStyle.ContentMarginTop = 2;
		shopBtnStyle.ContentMarginBottom = 2;
		_shopButton.AddThemeStyleboxOverride("normal", shopBtnStyle);
		var shopBtnHover = new StyleBoxFlat();
		shopBtnHover.BgColor = new Color(0.15f, 0.15f, 0.22f, 0.95f);
		MobaTheme.SetCorners(shopBtnHover, 4);
		MobaTheme.SetBorder(shopBtnHover, 2, MobaTheme.AccentGold);
		shopBtnHover.ContentMarginLeft = 6;
		shopBtnHover.ContentMarginRight = 6;
		shopBtnHover.ContentMarginTop = 2;
		shopBtnHover.ContentMarginBottom = 2;
		_shopButton.AddThemeStyleboxOverride("hover", shopBtnHover);
		_shopButton.AddThemeStyleboxOverride("pressed", shopBtnHover);
		_shopButton.Pressed += () => ToggleShop();

		CallDeferred(nameof(AttachGoldToInventory));

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

    public void UpdateHp(float current, float max, float shield = 0)
    {
        if (_playerHpBar != null) { _playerHpBar.MaxValue = max; _playerHpBar.Value = current; }
        if (_shieldBar != null)
        {
            _shieldBar.MaxValue = max;
            _shieldBar.Value = shield;
            _shieldBar.Visible = shield > 0;
        }

        string hpText = $"{(int)current}";
        if (shield > 0) hpText += $" (+{(int)shield})";
        hpText += $" / {(int)max} HP";

        if (_playerHpLabel != null) _playerHpLabel.Text = hpText;
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
        if (_levelLabel != null) _levelLabel.Text = $"Level {level}";
    }

    /// <summary>
	/// Attaches the gold label to the InventoryPanel's top-right corner.
	/// Called deferred so the InventoryPanel exists in the tree.
	/// </summary>
	private void AttachGoldToInventory()
	{
		var invPanel = GetTree().CurrentScene.FindChild("InventoryPanel", true, false) as Control;
		if (invPanel != null)
		{
			// Gold + Shop in a horizontal row
			var goldRow = new HBoxContainer();
			goldRow.AnchorLeft = 1f;
			goldRow.AnchorRight = 1f;
			goldRow.AnchorTop = 0f;
			goldRow.AnchorBottom = 0f;
			goldRow.OffsetLeft = -120;
			goldRow.OffsetRight = -4;
			goldRow.OffsetTop = -28;
			goldRow.OffsetBottom = -4;
			goldRow.AddThemeConstantOverride("separation", 4);
			goldRow.Alignment = BoxContainer.AlignmentMode.End;
			invPanel.AddChild(goldRow);

			goldRow.AddChild(_shopButton);
			goldRow.AddChild(_goldLabel);
		}
		else
		{
			// Fallback: just add to this layer
			AddChild(_goldLabel);
			_goldLabel.AnchorLeft = 1f;
			_goldLabel.AnchorRight = 1f;
			_goldLabel.AnchorTop = 1f;
			_goldLabel.AnchorBottom = 1f;
			_goldLabel.OffsetLeft = -100;
			_goldLabel.OffsetRight = -20;
			_goldLabel.OffsetTop = -184;
			_goldLabel.OffsetBottom = -164;
		}
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

		UpdateHp(_cachedStats.CurrentHealth, _cachedStats.MaxHealth, _cachedStats.CurrentShield);
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
