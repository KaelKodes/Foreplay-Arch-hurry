using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

/// <summary>
/// Full MOBA shop UI. Shows all 50 items organized by tier, with filtering,
/// search, recipe trees, stat previews, and buy/sell logic.
/// Proximity gate: can only buy/sell within 10u of friendly nexus (or dead).
/// </summary>
public partial class ShopUI : CanvasLayer
{
    // ── Constants ────────────────────────────────────────────────
    private const float NexusProximityRange = 10f;
    private const float SellRefundRatio = 0.6f; // 60% refund

    // ── Tier Colors ─────────────────────────────────────────────
    private static readonly Color TierCommon = new(0.75f, 0.75f, 0.75f);     // Silver
    private static readonly Color TierUncommon = new(0.3f, 0.9f, 0.4f);      // Green
    private static readonly Color TierRare = new(0.3f, 0.5f, 1.0f);          // Blue
    private static readonly Color TierLegendary = new(1.0f, 0.6f, 0.1f);     // Orange
    private static readonly Color TierConsumable = new(0.85f, 0.85f, 0.55f);  // Pale Yellow
    private static readonly Color CanAfford = new(0.3f, 1.0f, 0.4f);
    private static readonly Color CannotAfford = new(1.0f, 0.3f, 0.3f);
    private static readonly Color RecipeOwned = new(0.3f, 0.85f, 0.35f, 0.5f);

    // ── UI Nodes ────────────────────────────────────────────────
    private Panel _backdrop;
    private Panel _mainPanel;
    private VBoxContainer _mainVBox;

    // Header
    private Label _titleLabel;
    private Label _goldDisplay;
    private Label _proximityLabel;
    private LineEdit _searchBox;

    // Tier filter bar
    private HBoxContainer _filterBar;
    private Button _filterAll;
    private Button _filterConsumable;
    private Button _filterCommon;
    private Button _filterUncommon;
    private Button _filterRare;
    private Button _filterLegendary;

    // Stat sort bar
    private HBoxContainer _sortBar;

    // Item grid + detail panel split
    private HSplitContainer _contentSplit;
    private ScrollContainer _itemScroll;
    private GridContainer _itemGrid;
    private PanelContainer _detailPanel;

    // Detail panel internals
    private VBoxContainer _detailVBox;
    private HBoxContainer _detailHeaderRow;
    private TextureRect _detailIcon;
    private Label _detailName;
    private Label _detailTier;
    private Label _detailDescription;
    private Label _detailStats;
    private Label _detailPassive;
    private Label _detailRecipeTitle;
    private VBoxContainer _recipeContainer;
    private Label _detailCostLabel;
    private Button _buyButton;
    private Button _sellButton;

    // Inventory display at bottom of detail panel
    private Label _inventoryTitle;
    private HBoxContainer _inventoryRow;

    // State
    private ItemTier? _currentFilter = null;
    private string _currentSort = "";
    private string _searchQuery = "";
    private string _selectedItemId = null;
    private Stats _cachedStats;
    private bool _isVisible = false;

    // ── Lifecycle ────────────────────────────────────────────────
    public override void _Ready()
    {
        Layer = 15; // Above MobaHUD (layer 10)
        ProcessMode = ProcessModeEnum.Always; // Process even when game paused
        BuildUI();
        Visible = false;
        _isVisible = false;
    }

    public override void _Process(double delta)
    {
        if (!_isVisible) return;
        RefreshGoldDisplay();
        RefreshProximityStatus();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            // Escape to close
            if (key.Keycode == Key.Escape && _isVisible)
            {
                Hide();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    // ── Public API ───────────────────────────────────────────────
    public void Toggle()
    {
        if (_isVisible) Hide();
        else Show();
    }

    public new void Show()
    {
        _isVisible = true;
        Visible = true;
        _cachedStats = FindPlayerStats();
        RefreshItemGrid();
        RefreshGoldDisplay();
        RefreshProximityStatus();
        RefreshInventoryDisplay();
        // Focus search box
        _searchBox?.GrabFocus();
        // Hide crosshair while shopping
        SetCrosshairVisible(false);
    }

    public new void Hide()
    {
        _isVisible = false;
        Visible = false;
        // Restore crosshair
        SetCrosshairVisible(true);
    }

    private void SetCrosshairVisible(bool visible)
    {
        var hud = GetTree().Root.FindChild("Crosshair", true, false) as Control;
        if (hud != null) hud.Visible = visible;
    }

    public bool IsShopVisible => _isVisible;

    // ═══════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION
    // ═══════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Full-screen dimmed backdrop ──
        _backdrop = new Panel();
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var backdropStyle = new StyleBoxFlat();
        backdropStyle.BgColor = new Color(0, 0, 0, 0.55f);
        _backdrop.AddThemeStyleboxOverride("panel", backdropStyle);
        _backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_backdrop);

        // ── Main shop panel (centered, large) ──
        _mainPanel = new Panel();
        var mainStyle = new StyleBoxFlat();
        mainStyle.BgColor = new Color(0.06f, 0.06f, 0.1f, 0.96f);
        MobaTheme.SetCorners(mainStyle, 12);
        MobaTheme.SetBorder(mainStyle, 2, new Color(0.45f, 0.5f, 0.75f, 0.7f));
        mainStyle.ContentMarginLeft = 16;
        mainStyle.ContentMarginRight = 16;
        mainStyle.ContentMarginTop = 12;
        mainStyle.ContentMarginBottom = 12;
        mainStyle.ShadowColor = new Color(0, 0, 0, 0.5f);
        mainStyle.ShadowSize = 10;
        _mainPanel.AddThemeStyleboxOverride("panel", mainStyle);
        _mainPanel.AnchorLeft = 0.09f;
        _mainPanel.AnchorRight = 0.88f;
        _mainPanel.AnchorTop = 0.06f;
        _mainPanel.AnchorBottom = 0.85f;
        _mainPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_mainPanel);

        _mainVBox = new VBoxContainer();
        _mainVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mainVBox.OffsetLeft = 16;
        _mainVBox.OffsetTop = 12;
        _mainVBox.OffsetRight = -16;
        _mainVBox.OffsetBottom = -12;
        _mainVBox.AddThemeConstantOverride("separation", 8);
        _mainPanel.AddChild(_mainVBox);

        BuildHeader();
        BuildFilterBar();
        BuildSortBar();
        BuildContent();
        BuildDetailPanel();
    }

    // ── Header ──────────────────────────────────────────────────
    private void BuildHeader()
    {
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        _mainVBox.AddChild(headerRow);

        _titleLabel = MobaTheme.CreateHeroLabel("⚔ SHOP", MobaTheme.AccentGold);
        headerRow.AddChild(_titleLabel);

        // Spacer
        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerRow.AddChild(spacer);

        // Proximity indicator
        _proximityLabel = MobaTheme.CreateBodyLabel("", MobaTheme.TextMuted);
        _proximityLabel.CustomMinimumSize = new Vector2(160, 0);
        _proximityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        headerRow.AddChild(_proximityLabel);

        // Gold display
        _goldDisplay = MobaTheme.CreateHeadingLabel("💰 0", MobaTheme.AccentGold);
        _goldDisplay.CustomMinimumSize = new Vector2(100, 0);
        _goldDisplay.HorizontalAlignment = HorizontalAlignment.Right;
        headerRow.AddChild(_goldDisplay);

        // Search box
        _searchBox = new LineEdit();
        _searchBox.PlaceholderText = "🔍 Search items...";
        _searchBox.CustomMinimumSize = new Vector2(200, 28);
        _searchBox.AddThemeFontSizeOverride("font_size", 13);
        var searchStyle = new StyleBoxFlat();
        searchStyle.BgColor = new Color(0.1f, 0.1f, 0.16f, 0.9f);
        MobaTheme.SetCorners(searchStyle, 6);
        MobaTheme.SetBorder(searchStyle, 1, MobaTheme.PanelBorder);
        searchStyle.ContentMarginLeft = 8;
        searchStyle.ContentMarginRight = 8;
        _searchBox.AddThemeStyleboxOverride("normal", searchStyle);
        _searchBox.AddThemeColorOverride("font_color", MobaTheme.TextPrimary);
        _searchBox.AddThemeColorOverride("font_placeholder_color", MobaTheme.TextMuted);
        _searchBox.TextChanged += OnSearchChanged;
        headerRow.AddChild(_searchBox);

        // Close button
        var closeBtn = CreateStyledButton("✕", new Color(0.8f, 0.2f, 0.2f));
        closeBtn.CustomMinimumSize = new Vector2(32, 28);
        closeBtn.Pressed += () => Hide();
        headerRow.AddChild(closeBtn);
    }

    // ── Filter Bar ──────────────────────────────────────────────
    private void BuildFilterBar()
    {
        _filterBar = new HBoxContainer();
        _filterBar.AddThemeConstantOverride("separation", 6);
        _mainVBox.AddChild(_filterBar);

        _filterAll = CreateFilterButton("All", null, MobaTheme.TextPrimary);
        _filterConsumable = CreateFilterButton("🧪 Consumable", ItemTier.Consumable, TierConsumable);
        _filterCommon = CreateFilterButton("☆ Common", ItemTier.Common, TierCommon);
        _filterUncommon = CreateFilterButton("☆☆ Uncommon", ItemTier.Uncommon, TierUncommon);
        _filterRare = CreateFilterButton("☆☆☆ Rare", ItemTier.Rare, TierRare);
        _filterLegendary = CreateFilterButton("★ Legendary", ItemTier.Legendary, TierLegendary);

        UpdateFilterHighlight();
    }

    private Button CreateFilterButton(string text, ItemTier? tier, Color color)
    {
        var btn = CreateStyledButton(text, color);
        btn.Pressed += () =>
        {
            _currentFilter = (_currentFilter == tier) ? null : tier;
            UpdateFilterHighlight();
            RefreshItemGrid();
        };
        _filterBar.AddChild(btn);
        return btn;
    }

    private void UpdateFilterHighlight()
    {
        SetFilterActive(_filterAll, _currentFilter == null);
        SetFilterActive(_filterConsumable, _currentFilter == ItemTier.Consumable);
        SetFilterActive(_filterCommon, _currentFilter == ItemTier.Common);
        SetFilterActive(_filterUncommon, _currentFilter == ItemTier.Uncommon);
        SetFilterActive(_filterRare, _currentFilter == ItemTier.Rare);
        SetFilterActive(_filterLegendary, _currentFilter == ItemTier.Legendary);
    }

    private void SetFilterActive(Button btn, bool active)
    {
        btn.Modulate = active ? Colors.White : new Color(0.5f, 0.5f, 0.6f);
    }

    // ── Sort Bar ────────────────────────────────────────────────
    private void BuildSortBar()
    {
        _sortBar = new HBoxContainer();
        _sortBar.AddThemeConstantOverride("separation", 4);
        _mainVBox.AddChild(_sortBar);

        var sortLabel = MobaTheme.CreateBodyLabel("Sort by:", MobaTheme.TextMuted);
        _sortBar.AddChild(sortLabel);

        string[] sortOptions = { "Cost ↑", "Cost ↓", "STR", "INT", "VIT", "WIS", "AGI", "Haste", "Conc" };
        string[] sortKeys = { "cost_asc", "cost_desc", "str", "int", "vit", "wis", "agi", "haste", "conc" };
        for (int i = 0; i < sortOptions.Length; i++)
        {
            var key = sortKeys[i];
            var btn = CreateSmallButton(sortOptions[i]);
            btn.Pressed += () =>
            {
                _currentSort = (_currentSort == key) ? "" : key;
                RefreshItemGrid();
            };
            _sortBar.AddChild(btn);
        }
    }

    // ── Content Area (Grid + Detail split) ──────────────────────
    private void BuildContent()
    {
        _contentSplit = new HSplitContainer();
        _contentSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _contentSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        _mainVBox.AddChild(_contentSplit);

        // Left: Scrollable item grid
        _itemScroll = new ScrollContainer();
        _itemScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _itemScroll.CustomMinimumSize = new Vector2(500, 0);
        _contentSplit.AddChild(_itemScroll);

        _itemGrid = new GridContainer();
        _itemGrid.Columns = 6;
        _itemGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _itemGrid.AddThemeConstantOverride("h_separation", 8);
        _itemGrid.AddThemeConstantOverride("v_separation", 8);
        _itemScroll.AddChild(_itemGrid);
    }

    // ── Detail Panel (right side) ───────────────────────────────
    private void BuildDetailPanel()
    {
        _detailPanel = new PanelContainer();
        _detailPanel.CustomMinimumSize = new Vector2(320, 0);
        _detailPanel.AddThemeStyleboxOverride("panel", MobaTheme.CreateTooltipStyle());
        _contentSplit.AddChild(_detailPanel);

        var detailScroll = new ScrollContainer();
        detailScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _detailPanel.AddChild(detailScroll);

        _detailVBox = new VBoxContainer();
        _detailVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _detailVBox.AddThemeConstantOverride("separation", 6);
        detailScroll.AddChild(_detailVBox);

        // Detail header row (icon + name)
        _detailHeaderRow = new HBoxContainer();
        _detailHeaderRow.AddThemeConstantOverride("separation", 10);
        _detailVBox.AddChild(_detailHeaderRow);

        // Detail icon
        _detailIcon = new TextureRect();
        _detailIcon.CustomMinimumSize = new Vector2(48, 48);
        _detailIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _detailIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _detailIcon.Visible = false;
        _detailHeaderRow.AddChild(_detailIcon);

        // Item name
        _detailName = MobaTheme.CreateHeadingLabel("Select an item", MobaTheme.AccentGold);
        _detailName.AutowrapMode = TextServer.AutowrapMode.Word;
        _detailName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _detailHeaderRow.AddChild(_detailName);

        // Tier badge
        _detailTier = MobaTheme.CreateBodyLabel("", TierCommon);
        _detailVBox.AddChild(_detailTier);

        // Separator
        _detailVBox.AddChild(CreateSeparator());

        // Description
        _detailDescription = MobaTheme.CreateBodyLabel("", MobaTheme.TextSecondary);
        _detailDescription.AutowrapMode = TextServer.AutowrapMode.Word;
        _detailVBox.AddChild(_detailDescription);

        // Stats
        _detailStats = MobaTheme.CreateBodyLabel("", MobaTheme.TextPrimary);
        _detailStats.AutowrapMode = TextServer.AutowrapMode.Word;
        _detailVBox.AddChild(_detailStats);

        // Passive
        _detailPassive = MobaTheme.CreateBodyLabel("", new Color(0.6f, 0.85f, 1.0f));
        _detailPassive.AutowrapMode = TextServer.AutowrapMode.Word;
        _detailVBox.AddChild(_detailPassive);

        // Separator
        _detailVBox.AddChild(CreateSeparator());

        // Recipe
        _detailRecipeTitle = MobaTheme.CreateBodyLabel("", MobaTheme.AccentGold);
        _detailVBox.AddChild(_detailRecipeTitle);

        _recipeContainer = new VBoxContainer();
        _recipeContainer.AddThemeConstantOverride("separation", 2);
        _detailVBox.AddChild(_recipeContainer);

        // Separator
        _detailVBox.AddChild(CreateSeparator());

        // Cost
        _detailCostLabel = MobaTheme.CreateHeadingLabel("", MobaTheme.AccentGold);
        _detailVBox.AddChild(_detailCostLabel);

        // Buy/Sell buttons
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        _detailVBox.AddChild(btnRow);

        _buyButton = CreateStyledButton("BUY", CanAfford);
        _buyButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _buyButton.CustomMinimumSize = new Vector2(0, 34);
        _buyButton.Pressed += OnBuyPressed;
        btnRow.AddChild(_buyButton);

        _sellButton = CreateStyledButton("SELL", new Color(0.9f, 0.5f, 0.2f));
        _sellButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _sellButton.CustomMinimumSize = new Vector2(0, 34);
        _sellButton.Pressed += OnSellPressed;
        btnRow.AddChild(_sellButton);

        // Separator
        _detailVBox.AddChild(CreateSeparator());

        // Inventory display
        _inventoryTitle = MobaTheme.CreateBodyLabel("YOUR ITEMS", MobaTheme.AccentGold);
        _detailVBox.AddChild(_inventoryTitle);

        _inventoryRow = new HBoxContainer();
        _inventoryRow.AddThemeConstantOverride("separation", 4);
        _detailVBox.AddChild(_inventoryRow);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ITEM GRID
    // ═══════════════════════════════════════════════════════════════

    private void RefreshItemGrid()
    {
        // Clear existing
        foreach (Node child in _itemGrid.GetChildren()) child.QueueFree();

        var allItems = ItemData.GetAll();
        var filtered = new List<ItemInfo>();

        foreach (var kvp in allItems)
        {
            var item = kvp.Value;

            // Tier filter
            if (_currentFilter.HasValue && item.Tier != _currentFilter.Value) continue;

            // Search filter
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                bool match = item.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                          || item.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                          || item.PassiveName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
            }

            filtered.Add(item);
        }

        // Sort
        filtered = ApplySort(filtered);

        // Group by tier for visual clarity (if no specific filter)
        if (!_currentFilter.HasValue && string.IsNullOrEmpty(_currentSort))
        {
            filtered = filtered
                .OrderBy(i => TierOrder(i.Tier))
                .ThenBy(i => i.GoldCost)
                .ToList();
        }

        int currentGold = GetPlayerGold();

        foreach (var item in filtered)
        {
            var card = CreateItemCard(item, currentGold);
            _itemGrid.AddChild(card);
        }
    }

    private List<ItemInfo> ApplySort(List<ItemInfo> items)
    {
        return _currentSort switch
        {
            "cost_asc" => items.OrderBy(i => ItemData.GetTotalCost(i.Id)).ToList(),
            "cost_desc" => items.OrderByDescending(i => ItemData.GetTotalCost(i.Id)).ToList(),
            "str" => items.OrderByDescending(i => i.Stats.Strength).ToList(),
            "int" => items.OrderByDescending(i => i.Stats.Intelligence).ToList(),
            "vit" => items.OrderByDescending(i => i.Stats.Vitality).ToList(),
            "wis" => items.OrderByDescending(i => i.Stats.Wisdom).ToList(),
            "agi" => items.OrderByDescending(i => i.Stats.Agility).ToList(),
            "haste" => items.OrderByDescending(i => i.Stats.Haste).ToList(),
            "conc" => items.OrderByDescending(i => i.Stats.Concentration).ToList(),
            _ => items
        };
    }

    private int TierOrder(ItemTier tier) => tier switch
    {
        ItemTier.Consumable => 0,
        ItemTier.Common => 1,
        ItemTier.Uncommon => 2,
        ItemTier.Rare => 3,
        ItemTier.Legendary => 4,
        _ => 5
    };

    // ── Item Card ───────────────────────────────────────────────
    private Control CreateItemCard(ItemInfo item, int currentGold)
    {
        bool canAfford = currentGold >= item.GoldCost;
        Color tierColor = GetTierColor(item.Tier);

        var card = new Button();
        card.CustomMinimumSize = new Vector2(92, 110);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.ClipText = true;

        // Card background
        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = (_selectedItemId == item.Id)
            ? new Color(0.15f, 0.15f, 0.25f, 0.95f)
            : new Color(0.08f, 0.08f, 0.14f, 0.9f);
        MobaTheme.SetCorners(cardStyle, 6);
        MobaTheme.SetBorder(cardStyle, (_selectedItemId == item.Id) ? 2 : 1, tierColor);
        cardStyle.ContentMarginTop = 4;
        cardStyle.ContentMarginBottom = 4;
        cardStyle.ContentMarginLeft = 4;
        cardStyle.ContentMarginRight = 4;
        card.AddThemeStyleboxOverride("normal", cardStyle);

        // Hover style
        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.14f, 0.14f, 0.22f, 0.95f);
        MobaTheme.SetCorners(hoverStyle, 6);
        MobaTheme.SetBorder(hoverStyle, 2, tierColor);
        hoverStyle.ContentMarginTop = 4;
        hoverStyle.ContentMarginBottom = 4;
        hoverStyle.ContentMarginLeft = 4;
        hoverStyle.ContentMarginRight = 4;
        card.AddThemeStyleboxOverride("hover", hoverStyle);

        // Pressed style
        card.AddThemeStyleboxOverride("pressed", hoverStyle);

        // Card layout
        var cardVBox = new VBoxContainer();
        cardVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        cardVBox.Alignment = BoxContainer.AlignmentMode.Center;
        cardVBox.AddThemeConstantOverride("separation", 2);
        cardVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(cardVBox);

        // Item icon
        var iconPanel = new Panel();
        iconPanel.CustomMinimumSize = new Vector2(48, 48);
        var iconStyle = new StyleBoxFlat();
        iconStyle.BgColor = new Color(tierColor.R, tierColor.G, tierColor.B, 0.15f);
        MobaTheme.SetCorners(iconStyle, 4);
        MobaTheme.SetBorder(iconStyle, 1, new Color(tierColor.R, tierColor.G, tierColor.B, 0.3f));
        iconPanel.AddThemeStyleboxOverride("panel", iconStyle);
        iconPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        cardVBox.AddChild(iconPanel);

        if (!string.IsNullOrEmpty(item.IconPath) && ResourceLoader.Exists(item.IconPath))
        {
            var iconTex = new TextureRect();
            iconTex.Texture = GD.Load<Texture2D>(item.IconPath);
            iconTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconTex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconTex.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconTex.MouseFilter = Control.MouseFilterEnum.Ignore;
            iconPanel.AddChild(iconTex);
        }
        else
        {
            // Fallback: tier emoji if icon missing
            var tierIcon = new Label();
            tierIcon.Text = GetTierEmoji(item.Tier);
            tierIcon.HorizontalAlignment = HorizontalAlignment.Center;
            tierIcon.VerticalAlignment = VerticalAlignment.Center;
            tierIcon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            tierIcon.AddThemeFontSizeOverride("font_size", 20);
            tierIcon.MouseFilter = Control.MouseFilterEnum.Ignore;
            iconPanel.AddChild(tierIcon);
        }

        // Item name (truncated)
        var nameLabel = new Label();
        nameLabel.Text = item.Name;
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", MobaTheme.TextPrimary);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.ClipText = true;
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        cardVBox.AddChild(nameLabel);

        // Cost label
        var costLabel = new Label();
        costLabel.Text = $"💰 {item.GoldCost}";
        costLabel.AddThemeFontSizeOverride("font_size", 10);
        costLabel.AddThemeColorOverride("font_color", canAfford ? CanAfford : CannotAfford);
        costLabel.HorizontalAlignment = HorizontalAlignment.Center;
        costLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        cardVBox.AddChild(costLabel);

        // Click handler
        card.Pressed += () => SelectItem(item.Id);

        return card;
    }

    // ═══════════════════════════════════════════════════════════════
    //  DETAIL PANEL
    // ═══════════════════════════════════════════════════════════════

    private void SelectItem(string itemId)
    {
        _selectedItemId = itemId;
        RefreshDetailPanel();
        RefreshItemGrid(); // Re-render to update selection highlight
    }

    private void RefreshDetailPanel()
    {
        var item = _selectedItemId != null ? ItemData.Get(_selectedItemId) : null;
        if (item == null)
        {
            _detailName.Text = "Select an item";
            _detailTier.Text = "";
            _detailDescription.Text = "Click an item from the grid to see its details.";
            _detailStats.Text = "";
            _detailPassive.Text = "";
            _detailRecipeTitle.Text = "";
            _detailCostLabel.Text = "";
            _buyButton.Visible = false;
            _sellButton.Visible = false;
            ClearRecipe();
            return;
        }

        int currentGold = GetPlayerGold();
        Color tierColor = GetTierColor(item.Tier);

        _detailName.Text = item.Name;
        _detailName.AddThemeColorOverride("font_color", tierColor);

        // Detail icon
        if (!string.IsNullOrEmpty(item.IconPath) && ResourceLoader.Exists(item.IconPath))
        {
            _detailIcon.Texture = GD.Load<Texture2D>(item.IconPath);
            _detailIcon.Visible = true;
        }
        else
        {
            _detailIcon.Visible = false;
        }

        _detailTier.Text = GetTierDisplay(item.Tier);
        _detailTier.AddThemeColorOverride("font_color", tierColor);

        _detailDescription.Text = item.Description;

        // Stats display
        var statsLines = new List<string>();
        if (item.Stats.Strength > 0) statsLines.Add($"+{item.Stats.Strength} STR");
        if (item.Stats.Intelligence > 0) statsLines.Add($"+{item.Stats.Intelligence} INT");
        if (item.Stats.Vitality > 0) statsLines.Add($"+{item.Stats.Vitality} VIT");
        if (item.Stats.Wisdom > 0) statsLines.Add($"+{item.Stats.Wisdom} WIS");
        if (item.Stats.Agility > 0) statsLines.Add($"+{item.Stats.Agility} AGI");
        if (item.Stats.Haste > 0) statsLines.Add($"+{item.Stats.Haste} Haste");
        if (item.Stats.Concentration > 0) statsLines.Add($"+{item.Stats.Concentration} Concentration");
        _detailStats.Text = statsLines.Count > 0 ? string.Join("  •  ", statsLines) : "";

        // Passive
        if (!string.IsNullOrEmpty(item.PassiveName))
        {
            _detailPassive.Text = $"✦ {item.PassiveName}: {item.PassiveDescription}";
            _detailPassive.Visible = true;
        }
        else
        {
            _detailPassive.Text = "";
            _detailPassive.Visible = false;
        }

        // Recipe
        ClearRecipe();
        if (item.Recipe.Length > 0)
        {
            _detailRecipeTitle.Text = "📜 Recipe";
            foreach (var compId in item.Recipe)
            {
                var comp = ItemData.Get(compId);
                if (comp == null) continue;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);

                // Component owned indicator (placeholder — would check inventory)
                var arrow = MobaTheme.CreateBodyLabel("  ├─", MobaTheme.TextMuted);
                row.AddChild(arrow);

                var compName = MobaTheme.CreateBodyLabel(comp.Name, GetTierColor(comp.Tier));
                row.AddChild(compName);

                var compCost = MobaTheme.CreateBodyLabel($"({comp.GoldCost}g)", MobaTheme.TextMuted);
                row.AddChild(compCost);

                _recipeContainer.AddChild(row);
            }
            if (item.RecipeCost > 0)
            {
                var recipeRow = new HBoxContainer();
                recipeRow.AddThemeConstantOverride("separation", 6);
                var recipeArrow = MobaTheme.CreateBodyLabel("  └─", MobaTheme.TextMuted);
                recipeRow.AddChild(recipeArrow);
                var recipeFee = MobaTheme.CreateBodyLabel($"Recipe: {item.RecipeCost}g", MobaTheme.AccentGold);
                recipeRow.AddChild(recipeFee);
                _recipeContainer.AddChild(recipeRow);
            }
        }
        else
        {
            _detailRecipeTitle.Text = "";
        }

        // Total cost
        int totalCost = ItemData.GetTotalCost(item.Id);
        bool canAfford = currentGold >= item.GoldCost;
        _detailCostLabel.Text = $"💰 {item.GoldCost}g" +
            (totalCost != item.GoldCost ? $"  (Total: {totalCost}g)" : "");
        _detailCostLabel.AddThemeColorOverride("font_color", canAfford ? CanAfford : CannotAfford);

        // Buy/Sell visibility
        _buyButton.Visible = true;
        _buyButton.Text = canAfford ? $"BUY — {item.GoldCost}g" : $"NEED {item.GoldCost - currentGold}g MORE";
        _buyButton.Disabled = !canAfford || !IsNearNexus();

        _sellButton.Visible = true;
        int refund = (int)(item.GoldCost * SellRefundRatio);
        _sellButton.Text = $"SELL — {refund}g";
        _sellButton.Disabled = !IsNearNexus();
    }

    private void ClearRecipe()
    {
        foreach (Node child in _recipeContainer.GetChildren()) child.QueueFree();
    }

    // ═══════════════════════════════════════════════════════════════
    //  INVENTORY DISPLAY
    // ═══════════════════════════════════════════════════════════════

    private void RefreshInventoryDisplay()
    {
        foreach (Node child in _inventoryRow.GetChildren()) child.QueueFree();

        if (ToolManager.Instance == null) return;
        var slots = ToolManager.Instance.InventorySlots;
        for (int i = 0; i < 6 && i < slots.Length; i++)
        {
            var slotPanel = new Panel();
            slotPanel.CustomMinimumSize = new Vector2(40, 40);
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            MobaTheme.SetCorners(slotStyle, 4);
            MobaTheme.SetBorder(slotStyle, 1, MobaTheme.PanelBorder);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);
            slotPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

            var slotLabel = new Label();
            slotLabel.Text = (slots[i] != null && !string.IsNullOrEmpty(slots[i].DisplayName))
                ? slots[i].DisplayName.Substring(0, Math.Min(3, slots[i].DisplayName.Length))
                : $"{i + 1}";
            slotLabel.AddThemeFontSizeOverride("font_size", 10);
            slotLabel.AddThemeColorOverride("font_color", MobaTheme.TextMuted);
            slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            slotLabel.VerticalAlignment = VerticalAlignment.Center;
            slotLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slotLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            slotPanel.AddChild(slotLabel);

            _inventoryRow.AddChild(slotPanel);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BUY / SELL LOGIC
    // ═══════════════════════════════════════════════════════════════

    private void OnBuyPressed()
    {
        if (_selectedItemId == null) return;

        var item = ItemData.Get(_selectedItemId);
        if (item == null) return;

        if (!IsNearNexus())
        {
            GD.Print("[Shop] Cannot buy — not near nexus!");
            return;
        }

        var statsService = FindStatsService();
        if (statsService == null) return;

        int gold = GetPlayerGold();
        if (gold < item.GoldCost)
        {
            GD.Print("[Shop] Not enough gold!");
            return;
        }

        // Deduct gold
        statsService.AddGold(-item.GoldCost);

        // Add item to first empty inventory slot
        if (ToolManager.Instance != null)
        {
            var slots = ToolManager.Instance.InventorySlots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || string.IsNullOrEmpty(slots[i].DisplayName))
                {
                    slots[i] = new ToolItem(ToolType.None, item.Name, "", "");
                    break;
                }
            }
        }

        // Apply stat bonuses
        if (_cachedStats != null)
        {
            _cachedStats.Strength += item.Stats.Strength;
            _cachedStats.Intelligence += item.Stats.Intelligence;
            _cachedStats.Vitality += item.Stats.Vitality;
            _cachedStats.Wisdom += item.Stats.Wisdom;
            _cachedStats.Agility += item.Stats.Agility;
            _cachedStats.Haste += item.Stats.Haste;
            _cachedStats.Concentration += item.Stats.Concentration;
        }

        GD.Print($"[Shop] Bought {item.Name} for {item.GoldCost}g!");

        RefreshDetailPanel();
        RefreshItemGrid();
        RefreshInventoryDisplay();
    }

    private void OnSellPressed()
    {
        if (_selectedItemId == null) return;

        var item = ItemData.Get(_selectedItemId);
        if (item == null) return;

        if (!IsNearNexus())
        {
            GD.Print("[Shop] Cannot sell — not near nexus!");
            return;
        }

        // Find item in inventory and remove it
        if (ToolManager.Instance != null)
        {
            var slots = ToolManager.Instance.InventorySlots;
            bool found = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].DisplayName == item.Name)
                {
                    slots[i] = new ToolItem();
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                GD.Print("[Shop] Item not found in inventory!");
                return;
            }
        }

        var statsService = FindStatsService();

        // Refund gold
        int refund = (int)(item.GoldCost * SellRefundRatio);
        statsService?.AddGold(refund);

        // Remove stat bonuses
        if (_cachedStats != null)
        {
            _cachedStats.Strength -= item.Stats.Strength;
            _cachedStats.Intelligence -= item.Stats.Intelligence;
            _cachedStats.Vitality -= item.Stats.Vitality;
            _cachedStats.Wisdom -= item.Stats.Wisdom;
            _cachedStats.Agility -= item.Stats.Agility;
            _cachedStats.Haste -= item.Stats.Haste;
            _cachedStats.Concentration -= item.Stats.Concentration;
        }

        GD.Print($"[Shop] Sold {item.Name} for {refund}g!");

        RefreshDetailPanel();
        RefreshItemGrid();
        RefreshInventoryDisplay();
    }

    // ═══════════════════════════════════════════════════════════════
    //  PROXIMITY / STATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private bool IsNearNexus()
    {
        var player = GetTree().GetFirstNodeInGroup("local_player") as Node3D;
        if (player == null) return false;

        // Dead players can always shop
        if (_cachedStats != null && _cachedStats.CurrentHealth <= 0) return true;

        // Find friendly nexus
        var nexuses = GetTree().GetNodesInGroup("nexus");
        foreach (var n in nexuses)
        {
            if (n is not MobaNexus nexus) continue;
            // Check if same team (for now allow any nexus — team check can be added)
            float dist = player.GlobalPosition.DistanceTo(nexus.GlobalPosition);
            if (dist <= NexusProximityRange) return true;
        }

        return false;
    }

    private void RefreshProximityStatus()
    {
        bool near = IsNearNexus();
        _proximityLabel.Text = near ? "✅ Shop Available" : "⚠ Move to Nexus";
        _proximityLabel.AddThemeColorOverride("font_color", near ? CanAfford : CannotAfford);

        // Update buy/sell button states
        if (_buyButton != null && _selectedItemId != null)
        {
            var item = ItemData.Get(_selectedItemId);
            if (item != null)
            {
                bool canAfford = GetPlayerGold() >= item.GoldCost;
                _buyButton.Disabled = !canAfford || !near;
                _sellButton.Disabled = !near;
            }
        }
    }

    private void RefreshGoldDisplay()
    {
        int gold = GetPlayerGold();
        _goldDisplay.Text = $"💰 {gold}";
    }

    private int GetPlayerGold()
    {
        _cachedStats ??= FindPlayerStats();
        return _cachedStats?.Gold ?? 0;
    }

    private Stats FindPlayerStats()
    {
        var player = GetTree().GetFirstNodeInGroup("local_player");
        if (player == null) return null;

        var archerySystem = player.FindChild("ArcherySystem", true, false) as ArcherySystem;
        if (archerySystem == null)
        {
            foreach (var child in player.GetChildren())
            {
                if (child is ArcherySystem asys) { archerySystem = asys; break; }
            }
        }
        return archerySystem?.PlayerStats;
    }

    private StatsService FindStatsService()
    {
        var player = GetTree().GetFirstNodeInGroup("local_player");
        if (player == null) return null;

        var archerySystem = player.FindChild("ArcherySystem", true, false) as ArcherySystem;
        if (archerySystem == null)
        {
            foreach (var child in player.GetChildren())
            {
                if (child is ArcherySystem asys) { archerySystem = asys; break; }
            }
        }
        return archerySystem?.PlayerStatsService;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SEARCH
    // ═══════════════════════════════════════════════════════════════

    private void OnSearchChanged(string newText)
    {
        _searchQuery = newText?.Trim() ?? "";
        RefreshItemGrid();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STYLING HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static Color GetTierColor(ItemTier tier) => tier switch
    {
        ItemTier.Consumable => TierConsumable,
        ItemTier.Common => TierCommon,
        ItemTier.Uncommon => TierUncommon,
        ItemTier.Rare => TierRare,
        ItemTier.Legendary => TierLegendary,
        _ => MobaTheme.TextPrimary
    };

    private static string GetTierDisplay(ItemTier tier) => tier switch
    {
        ItemTier.Consumable => "🧪 Consumable",
        ItemTier.Common => "☆ Common",
        ItemTier.Uncommon => "☆☆ Uncommon",
        ItemTier.Rare => "☆☆☆ Rare",
        ItemTier.Legendary => "★ Legendary",
        _ => ""
    };

    private static string GetTierEmoji(ItemTier tier) => tier switch
    {
        ItemTier.Consumable => "🧪",
        ItemTier.Common => "☆",
        ItemTier.Uncommon => "⚔",
        ItemTier.Rare => "💎",
        ItemTier.Legendary => "👑",
        _ => "?"
    };

    private Button CreateStyledButton(string text, Color accentColor)
    {
        var btn = new Button();
        btn.Text = text;
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeColorOverride("font_color", accentColor);

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(accentColor.R * 0.15f, accentColor.G * 0.15f, accentColor.B * 0.15f, 0.8f);
        MobaTheme.SetCorners(normalStyle, 4);
        MobaTheme.SetBorder(normalStyle, 1, new Color(accentColor.R, accentColor.G, accentColor.B, 0.5f));
        normalStyle.ContentMarginLeft = 8;
        normalStyle.ContentMarginRight = 8;
        normalStyle.ContentMarginTop = 4;
        normalStyle.ContentMarginBottom = 4;
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(accentColor.R * 0.25f, accentColor.G * 0.25f, accentColor.B * 0.25f, 0.9f);
        MobaTheme.SetCorners(hoverStyle, 4);
        MobaTheme.SetBorder(hoverStyle, 2, accentColor);
        hoverStyle.ContentMarginLeft = 8;
        hoverStyle.ContentMarginRight = 8;
        hoverStyle.ContentMarginTop = 4;
        hoverStyle.ContentMarginBottom = 4;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);

        return btn;
    }

    private Button CreateSmallButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.AddThemeFontSizeOverride("font_size", 11);
        btn.AddThemeColorOverride("font_color", MobaTheme.TextSecondary);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.1f, 0.16f, 0.7f);
        MobaTheme.SetCorners(style, 3);
        MobaTheme.SetBorder(style, 1, new Color(0.3f, 0.3f, 0.4f, 0.5f));
        style.ContentMarginLeft = 6;
        style.ContentMarginRight = 6;
        style.ContentMarginTop = 2;
        style.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.15f, 0.15f, 0.22f, 0.9f);
        MobaTheme.SetCorners(hoverStyle, 3);
        MobaTheme.SetBorder(hoverStyle, 1, MobaTheme.PanelBorder);
        hoverStyle.ContentMarginLeft = 6;
        hoverStyle.ContentMarginRight = 6;
        hoverStyle.ContentMarginTop = 2;
        hoverStyle.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        return btn;
    }

    private static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 6);
        return sep;
    }
}
