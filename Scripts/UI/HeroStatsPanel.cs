using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Displays hero stats in a Base + Mod = Total format.
/// Mod column has hover tooltips showing per-item breakdown.
/// </summary>
public partial class HeroStatsPanel : PanelContainer
{
    // Stat row labels: [stat] = (base, mod, total)
    private readonly Dictionary<string, (Label Base, Label Mod, Label Total)> _statRows = new();
    // Derived stat labels
    private Label _adLabel, _moveLabel, _atkCdrLabel, _ablCdrLabel;
    private Label _hpLabel, _spLabel, _mpLabel;
    // Tooltip for mod hover
    private PanelContainer _modTooltip;
    private VBoxContainer _modTooltipVBox;
    // Cached refs
    private Stats _cachedStats;
    private float _refreshTimer;
    private const float RefreshInterval = 0.2f; // 5Hz

    private bool _isVisible;
    public bool IsStatsVisible => _isVisible;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", MobaTheme.CreatePanelStyle());
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(310, 0);
        Visible = false;
        _isVisible = false;

        var mainVBox = new VBoxContainer();
        mainVBox.AddThemeConstantOverride("separation", 2);
        mainVBox.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(mainVBox);

        // Title
        var title = MobaTheme.CreateHeadingLabel("HERO STATS", MobaTheme.AccentGold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        mainVBox.AddChild(title);

        // Column headers
        var headerRow = CreateRow("Stat", "Base", "Mod", "Total", MobaTheme.TextMuted);
        mainVBox.AddChild(headerRow);

        // Separator
        mainVBox.AddChild(CreateSeparator());

        // Core stat rows
        string[] coreStats = { "STR", "INT", "VIT", "WIS", "AGI", "Haste", "Conc" };
        foreach (var stat in coreStats)
        {
            var row = CreateStatRow(stat);
            mainVBox.AddChild(row);
        }

        // Separator before derived
        mainVBox.AddChild(CreateSeparator());

        // Derived stats header
        var derivedTitle = MobaTheme.CreateBodyLabel("─── Derived Stats ───", MobaTheme.TextSecondary);
        derivedTitle.HorizontalAlignment = HorizontalAlignment.Center;
        mainVBox.AddChild(derivedTitle);

        // Derived stats grid
        var derivedGrid = new GridContainer();
        derivedGrid.Columns = 2;
        derivedGrid.AddThemeConstantOverride("h_separation", 16);
        derivedGrid.AddThemeConstantOverride("v_separation", 2);
        derivedGrid.MouseFilter = MouseFilterEnum.Ignore;
        mainVBox.AddChild(derivedGrid);

        _adLabel = MobaTheme.CreateBodyLabel("AD: --", MobaTheme.TextPrimary);
        derivedGrid.AddChild(_adLabel);
        _moveLabel = MobaTheme.CreateBodyLabel("Move: --", MobaTheme.TextPrimary);
        derivedGrid.AddChild(_moveLabel);
        _atkCdrLabel = MobaTheme.CreateBodyLabel("Atk CDR: --", MobaTheme.TextPrimary);
        derivedGrid.AddChild(_atkCdrLabel);
        _ablCdrLabel = MobaTheme.CreateBodyLabel("Abl CDR: --", MobaTheme.TextPrimary);
        derivedGrid.AddChild(_ablCdrLabel);

        // Vitals row
        mainVBox.AddChild(CreateSeparator());

        var vitalsRow = new HBoxContainer();
        vitalsRow.AddThemeConstantOverride("separation", 8);
        vitalsRow.MouseFilter = MouseFilterEnum.Ignore;
        mainVBox.AddChild(vitalsRow);

        _hpLabel = MobaTheme.CreateBodyLabel("HP: --", MobaTheme.HpFill);
        vitalsRow.AddChild(_hpLabel);
        _spLabel = MobaTheme.CreateBodyLabel("SP: --", MobaTheme.StaminaFill);
        vitalsRow.AddChild(_spLabel);
        _mpLabel = MobaTheme.CreateBodyLabel("MP: --", MobaTheme.ManaFill);
        vitalsRow.AddChild(_mpLabel);

        // Build mod tooltip (floats above)
        BuildModTooltip();
    }

    private void BuildModTooltip()
    {
        _modTooltip = new PanelContainer();
        _modTooltip.AddThemeStyleboxOverride("panel", MobaTheme.CreateTooltipStyle());
        _modTooltip.ZIndex = 200;
        _modTooltip.MouseFilter = MouseFilterEnum.Ignore;
        _modTooltip.Visible = false;
        _modTooltip.CustomMinimumSize = new Vector2(200, 0);

        _modTooltipVBox = new VBoxContainer();
        _modTooltipVBox.MouseFilter = MouseFilterEnum.Ignore;
        _modTooltipVBox.AddThemeConstantOverride("separation", 2);
        _modTooltip.AddChild(_modTooltipVBox);

        CallDeferred(nameof(AttachModTooltipToRoot));
    }

    private void AttachModTooltipToRoot()
    {
        GetTree().Root?.AddChild(_modTooltip);
    }

    // ── Row Builders ────────────────────────────────────────────

    private HBoxContainer CreateRow(string col1, string col2, string col3, string col4, Color color)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        row.MouseFilter = MouseFilterEnum.Ignore;

        var l1 = MobaTheme.CreateBodyLabel(col1, color);
        l1.CustomMinimumSize = new Vector2(70, 0);
        row.AddChild(l1);

        var l2 = MobaTheme.CreateBodyLabel(col2, color);
        l2.CustomMinimumSize = new Vector2(60, 0);
        l2.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(l2);

        var l3 = MobaTheme.CreateBodyLabel(col3, color);
        l3.CustomMinimumSize = new Vector2(70, 0);
        l3.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(l3);

        var l4 = MobaTheme.CreateBodyLabel(col4, color);
        l4.CustomMinimumSize = new Vector2(70, 0);
        l4.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(l4);

        return row;
    }

    private HBoxContainer CreateStatRow(string statName)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        row.MouseFilter = MouseFilterEnum.Ignore;

        var nameLabel = MobaTheme.CreateBodyLabel(statName, MobaTheme.AccentGold);
        nameLabel.CustomMinimumSize = new Vector2(70, 0);
        row.AddChild(nameLabel);

        var baseLabel = MobaTheme.CreateBodyLabel("--", MobaTheme.TextPrimary);
        baseLabel.CustomMinimumSize = new Vector2(60, 0);
        baseLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(baseLabel);

        // Mod label is interactive (hover for tooltip)
        var modLabel = MobaTheme.CreateBodyLabel("+0", new Color(0.4f, 0.9f, 0.5f));
        modLabel.CustomMinimumSize = new Vector2(70, 0);
        modLabel.HorizontalAlignment = HorizontalAlignment.Right;
        modLabel.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(modLabel);

        var totalLabel = MobaTheme.CreateBodyLabel("= --", MobaTheme.TextPrimary);
        totalLabel.CustomMinimumSize = new Vector2(70, 0);
        totalLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(totalLabel);

        _statRows[statName] = (baseLabel, modLabel, totalLabel);

        // Wire up mod hover
        string stat = statName;
        modLabel.MouseEntered += () => ShowModTooltip(stat, modLabel);
        modLabel.MouseExited += () => HideModTooltip();

        return row;
    }

    private HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        sep.MouseFilter = MouseFilterEnum.Ignore;
        return sep;
    }

    // ── Toggle ──────────────────────────────────────────────────

    public void Toggle()
    {
        if (_isVisible) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        _isVisible = true;
        Visible = true;
        RefreshStats();
    }

    public void HidePanel()
    {
        _isVisible = false;
        Visible = false;
        HideModTooltip();
    }

    // ── Stats Refresh ───────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (!_isVisible) return;

        _refreshTimer += (float)delta;
        if (_refreshTimer < RefreshInterval) return;
        _refreshTimer = 0;

        RefreshStats();
    }

    public void RefreshStats()
    {
        if (_cachedStats == null)
        {
            var player = GetTree().GetFirstNodeInGroup("local_player") as Node;
            var archery = player?.FindChild("ArcherySystem", true, false) as ArcherySystem;
            if (archery == null && player != null)
            {
                foreach (var child in player.GetChildren())
                {
                    if (child is ArcherySystem asys) { archery = asys; break; }
                }
            }
            _cachedStats = archery?.PlayerStats;
            if (_cachedStats == null) return;
        }

        var itemMods = ComputeItemMods();

        // Update stat rows
        UpdateStatRow("STR", _cachedStats.Strength, itemMods.Strength);
        UpdateStatRow("INT", _cachedStats.Intelligence, itemMods.Intelligence);
        UpdateStatRow("VIT", _cachedStats.Vitality, itemMods.Vitality);
        UpdateStatRow("WIS", _cachedStats.Wisdom, itemMods.Wisdom);
        UpdateStatRow("AGI", _cachedStats.Agility, itemMods.Agility);
        UpdateStatRow("Haste", _cachedStats.Haste, itemMods.Haste);
        UpdateStatRow("Conc", _cachedStats.Concentration, itemMods.Concentration);

        // Derived stats
        _adLabel.Text = $"AD: {_cachedStats.AttackDamage}";
        _moveLabel.Text = $"Move: {_cachedStats.DerivedMoveSpeed:F1}";

        float atkCdr = (1f - _cachedStats.AttackCooldownMultiplier) * 100f;
        float ablCdr = (1f - _cachedStats.AbilityCooldownMultiplier) * 100f;
        _atkCdrLabel.Text = $"Atk CDR: {atkCdr:F0}%";
        _ablCdrLabel.Text = $"Abl CDR: {ablCdr:F0}%";

        // Vitals
        _hpLabel.Text = $"HP: {_cachedStats.MaxHealth}";
        _spLabel.Text = $"SP: {_cachedStats.MaxStamina}";
        _mpLabel.Text = $"MP: {_cachedStats.MaxMana}";
    }

    private void UpdateStatRow(string statName, int total, int itemMod)
    {
        if (!_statRows.ContainsKey(statName)) return;
        var (baseLabel, modLabel, totalLabel) = _statRows[statName];

        int baseStat = total - itemMod;
        baseLabel.Text = baseStat.ToString();
        modLabel.Text = itemMod > 0 ? $"+{itemMod}" : itemMod < 0 ? itemMod.ToString() : "+0";
        modLabel.AddThemeColorOverride("font_color",
            itemMod > 0 ? new Color(0.4f, 0.9f, 0.5f) :
            itemMod < 0 ? new Color(1f, 0.4f, 0.4f) :
            MobaTheme.TextMuted);
        totalLabel.Text = $"= {total}";
    }

    // ── Item Mod Computation ────────────────────────────────────

    private ItemStats ComputeItemMods()
    {
        var mods = new ItemStats();
        if (ToolManager.Instance == null) return mods;

        var slots = ToolManager.Instance.InventorySlots;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.DisplayName)) continue;

            string itemId = slot.WeaponScenePath;
            if (string.IsNullOrEmpty(itemId)) continue;

            var info = ItemData.Get(itemId);
            if (info?.Stats == null) continue;

            mods.Strength += info.Stats.Strength;
            mods.Intelligence += info.Stats.Intelligence;
            mods.Vitality += info.Stats.Vitality;
            mods.Wisdom += info.Stats.Wisdom;
            mods.Agility += info.Stats.Agility;
            mods.Haste += info.Stats.Haste;
            mods.Concentration += info.Stats.Concentration;
        }

        return mods;
    }

    /// <summary>
    /// Returns a list of (itemName, statValue) for a given stat from equipped items.
    /// </summary>
    private List<(string Name, int Value)> GetModSources(string statName)
    {
        var sources = new List<(string, int)>();
        if (ToolManager.Instance == null) return sources;

        var slots = ToolManager.Instance.InventorySlots;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.DisplayName)) continue;

            string itemId = slot.WeaponScenePath;
            if (string.IsNullOrEmpty(itemId)) continue;

            var info = ItemData.Get(itemId);
            if (info?.Stats == null) continue;

            int val = statName switch
            {
                "STR" => info.Stats.Strength,
                "INT" => info.Stats.Intelligence,
                "VIT" => info.Stats.Vitality,
                "WIS" => info.Stats.Wisdom,
                "AGI" => info.Stats.Agility,
                "Haste" => info.Stats.Haste,
                "Conc" => info.Stats.Concentration,
                _ => 0
            };

            if (val != 0)
            {
                sources.Add((info.Name, val));
            }
        }

        return sources;
    }

    // ── Mod Tooltip ─────────────────────────────────────────────

    private void ShowModTooltip(string statName, Control anchor)
    {
        foreach (Node child in _modTooltipVBox.GetChildren()) child.QueueFree();

        var sources = GetModSources(statName);
        if (sources.Count == 0)
        {
            var noItems = MobaTheme.CreateBodyLabel("No item bonuses", MobaTheme.TextMuted);
            noItems.MouseFilter = MouseFilterEnum.Ignore;
            _modTooltipVBox.AddChild(noItems);
        }
        else
        {
            var header = MobaTheme.CreateBodyLabel($"{statName} Sources:", MobaTheme.AccentGold);
            header.MouseFilter = MouseFilterEnum.Ignore;
            _modTooltipVBox.AddChild(header);

            foreach (var (name, val) in sources)
            {
                string sign = val > 0 ? "+" : "";
                var line = MobaTheme.CreateBodyLabel($"  {name}: {sign}{val}", MobaTheme.TextPrimary);
                line.MouseFilter = MouseFilterEnum.Ignore;
                _modTooltipVBox.AddChild(line);
            }
        }

        _modTooltip.Visible = true;
        _modTooltip.ForceUpdateTransform();

        // Position to the right of the mod label
        Vector2 globalPos = anchor.GlobalPosition;
        Vector2 targetPos = new Vector2(
            globalPos.X + anchor.Size.X + 8,
            globalPos.Y - 4
        );

        // Clamp to screen
        var viewportRect = GetViewportRect();
        float margin = 10f;
        if (targetPos.X + _modTooltip.Size.X > viewportRect.Size.X - margin)
        {
            targetPos.X = globalPos.X - _modTooltip.Size.X - 8;
        }
        targetPos.Y = Mathf.Clamp(targetPos.Y, margin, viewportRect.Size.Y - _modTooltip.Size.Y - margin);

        _modTooltip.GlobalPosition = targetPos;
    }

    private void HideModTooltip()
    {
        if (_modTooltip != null) _modTooltip.Visible = false;
    }

    // ── Cleanup ─────────────────────────────────────────────────

    public override void _ExitTree()
    {
        if (_modTooltip != null && _modTooltip.GetParent() != null)
        {
            _modTooltip.GetParent().CallDeferred("remove_child", _modTooltip);
            _modTooltip.CallDeferred("queue_free");
        }
    }
}
