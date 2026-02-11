using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Controller for the RPG inventory panel. Displays 6 inventory slots in a grid.
/// Shows item icons and tooltips on hover.
/// </summary>
public partial class InventoryPanelController : Control
{
    private Container _slotContainer;
    private TextureButton[] _slots;
    private PanelContainer _tooltip;
    private Label _tooltipName;
    private Label _tooltipTier;
    private Label _tooltipStats;
    private Label _tooltipPassive;
    private Label _tooltipCost;

    [Export] public NodePath SlotContainerPath;

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(SlotContainerPath?.ToString()))
        {
            _slotContainer = GetNodeOrNull<Container>(SlotContainerPath);
        }

        if (_slotContainer == null)
        {
            _slotContainer = GetNodeOrNull<Container>("SlotContainer");
        }

        if (_slotContainer == null)
        {
            var grid = new GridContainer();
            grid.Name = "SlotContainer";
            grid.Columns = 3;
            grid.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(grid);
            _slotContainer = grid;
        }

        CreateSlots();
        ApplyTheme();
        BuildTooltip();

        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
            ToolManager.Instance.HotbarModeChanged += (m) => OnHotbarModeChanged((ToolManager.HotbarMode)m);

            // Initial visibility
            Visible = ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;
        }
    }

    private void ApplyTheme()
    {
        // Style the background panel with MobaTheme
        var bgPanel = GetNodeOrNull<Panel>("Background");
        if (bgPanel != null)
        {
            bgPanel.AddThemeStyleboxOverride("panel", MobaTheme.CreatePanelStyle());
        }
    }

    private void OnHotbarModeChanged(ToolManager.HotbarMode mode)
    {
        Visible = (mode == ToolManager.HotbarMode.RPG);
        if (Visible) RefreshSlots();
    }

    // ── Tooltip ──────────────────────────────────────────────────

    private void BuildTooltip()
    {
        _tooltip = new PanelContainer();
        _tooltip.AddThemeStyleboxOverride("panel", MobaTheme.CreateTooltipStyle());
        _tooltip.ZIndex = 100;
        _tooltip.MouseFilter = MouseFilterEnum.Ignore;
        _tooltip.Visible = false;
        _tooltip.CustomMinimumSize = new Vector2(220, 0);

        var vbox = new VBoxContainer();
        vbox.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddThemeConstantOverride("separation", 4);
        _tooltip.AddChild(vbox);

        _tooltipName = MobaTheme.CreateHeadingLabel("", MobaTheme.AccentGold);
        _tooltipName.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_tooltipName);

        _tooltipTier = MobaTheme.CreateBodyLabel("", MobaTheme.TextSecondary);
        _tooltipTier.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_tooltipTier);

        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        sep.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(sep);

        _tooltipStats = MobaTheme.CreateBodyLabel("", MobaTheme.TextPrimary);
        _tooltipStats.AutowrapMode = TextServer.AutowrapMode.Word;
        _tooltipStats.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_tooltipStats);

        _tooltipPassive = MobaTheme.CreateBodyLabel("", new Color(0.6f, 0.85f, 1.0f));
        _tooltipPassive.AutowrapMode = TextServer.AutowrapMode.Word;
        _tooltipPassive.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_tooltipPassive);

        _tooltipCost = MobaTheme.CreateBodyLabel("", MobaTheme.AccentGold);
        _tooltipCost.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_tooltipCost);

        // Add to the scene root so it floats above everything
        CallDeferred(nameof(AttachTooltipToRoot));
    }

    private void AttachTooltipToRoot()
    {
        var root = GetTree().Root;
        root?.AddChild(_tooltip);
    }

    private void ShowTooltip(string itemId, Vector2 globalPos)
    {
        if (string.IsNullOrEmpty(itemId)) { HideTooltip(); return; }

        var item = ItemData.Get(itemId);
        if (item == null) { HideTooltip(); return; }

        _tooltipName.Text = item.Name;

        string tierText = item.Tier switch
        {
            ItemTier.Consumable => "🧪 Consumable",
            ItemTier.Common => "☆ Common",
            ItemTier.Uncommon => "☆☆ Uncommon",
            ItemTier.Rare => "☆☆☆ Rare",
            ItemTier.Legendary => "★ Legendary",
            _ => ""
        };
        _tooltipTier.Text = tierText;

        // Stat lines
        var lines = new List<string>();
        if (item.Stats.Strength > 0) lines.Add($"+{item.Stats.Strength} STR");
        if (item.Stats.Intelligence > 0) lines.Add($"+{item.Stats.Intelligence} INT");
        if (item.Stats.Vitality > 0) lines.Add($"+{item.Stats.Vitality} VIT");
        if (item.Stats.Wisdom > 0) lines.Add($"+{item.Stats.Wisdom} WIS");
        if (item.Stats.Agility > 0) lines.Add($"+{item.Stats.Agility} AGI");
        if (item.Stats.Haste > 0) lines.Add($"+{item.Stats.Haste} Haste");
        if (item.Stats.Concentration > 0) lines.Add($"+{item.Stats.Concentration} Concentration");
        _tooltipStats.Text = lines.Count > 0 ? string.Join("  •  ", lines) : "";
        _tooltipStats.Visible = lines.Count > 0;

        // Passive
        if (!string.IsNullOrEmpty(item.PassiveName))
        {
            _tooltipPassive.Text = $"✦ {item.PassiveName}: {item.PassiveDescription}";
            _tooltipPassive.Visible = true;
        }
        else
        {
            _tooltipPassive.Visible = false;
        }

        // Cost
        int totalCost = ItemData.GetTotalCost(item.Id);
        _tooltipCost.Text = $"💰 {item.GoldCost}g" +
            (totalCost != item.GoldCost ? $"  (Total: {totalCost}g)" : "");

        _tooltip.Visible = true;
        _tooltip.ForceUpdateTransform();

        // Position above slot
        Vector2 targetPos = new Vector2(
            globalPos.X - _tooltip.Size.X / 2f,
            globalPos.Y - _tooltip.Size.Y - 12f
        );

        // Clamp to screen
        var viewportRect = GetViewportRect();
        float margin = 10f;
        targetPos.X = Mathf.Clamp(targetPos.X, margin, viewportRect.Size.X - _tooltip.Size.X - margin);
        targetPos.Y = Mathf.Clamp(targetPos.Y, margin, viewportRect.Size.Y - _tooltip.Size.Y - margin);

        _tooltip.GlobalPosition = targetPos;
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.Visible = false;
    }

    // ── Slots ────────────────────────────────────────────────────

    private void CreateSlots()
    {
        foreach (Node child in _slotContainer.GetChildren())
        {
            child.QueueFree();
        }

        _slots = new TextureButton[6];

        for (int i = 0; i < 6; i++)
        {
            var slot = new TextureButton();
            slot.Name = $"InvSlot{i + 1}";
            slot.CustomMinimumSize = new Vector2(64, 64);
            slot.StretchMode = TextureButton.StretchModeEnum.Scale;
            slot.IgnoreTextureSize = true;

            // Slot background
            var bg = new Panel();
            bg.Name = "Background";
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            slotStyle.CornerRadiusTopLeft = 4;
            slotStyle.CornerRadiusTopRight = 4;
            slotStyle.CornerRadiusBottomLeft = 4;
            slotStyle.CornerRadiusBottomRight = 4;
            slotStyle.BorderWidthTop = 1;
            slotStyle.BorderWidthBottom = 1;
            slotStyle.BorderWidthLeft = 1;
            slotStyle.BorderWidthRight = 1;
            slotStyle.BorderColor = MobaTheme.PanelBorder;
            bg.AddThemeStyleboxOverride("panel", slotStyle);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(bg);

            // Icon
            var iconRect = new TextureRect();
            iconRect.Name = "IconRect";
            iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconRect.OffsetLeft = 4;
            iconRect.OffsetTop = 4;
            iconRect.OffsetRight = -4;
            iconRect.OffsetBottom = -4;
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(iconRect);

            // Label (shown when no icon)
            var label = new Label();
            label.Name = "Label";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", MobaTheme.TextMuted);
            label.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(label);

            _slotContainer.AddChild(slot);
            _slots[i] = slot;

            int index = i;
            slot.Pressed += () => GD.Print($"[Inventory] Slot {index} pressed");

            // Tooltip on hover
            slot.MouseEntered += () => OnSlotHover(index);
            slot.MouseExited += () => HideTooltip();
        }
    }

    private void OnSlotHover(int slotIndex)
    {
        if (ToolManager.Instance == null) return;
        var items = ToolManager.Instance.InventorySlots;
        if (slotIndex < 0 || slotIndex >= items.Length) return;

        var item = items[slotIndex];
        if (item == null || string.IsNullOrEmpty(item.DisplayName)) return;

        // WeaponScenePath stores the ItemData ID
        string itemId = item.WeaponScenePath;
        if (string.IsNullOrEmpty(itemId)) return;

        var slotControl = _slots[slotIndex] as Control;
        if (slotControl == null) return;

        Vector2 center = slotControl.GlobalPosition + slotControl.Size / 2f;
        ShowTooltip(itemId, new Vector2(center.X, slotControl.GlobalPosition.Y));
    }

    private void RefreshSlots()
    {
        if (ToolManager.Instance == null) return;

        var items = ToolManager.Instance.InventorySlots;
        for (int i = 0; i < 6 && i < items.Length; i++)
        {
            var item = items[i];
            var iconRect = _slots[i].GetNodeOrNull<TextureRect>("IconRect");
            var label = _slots[i].GetNodeOrNull<Label>("Label");

            bool hasItem = item != null && !string.IsNullOrEmpty(item.DisplayName);

            if (hasItem && !string.IsNullOrEmpty(item.IconPath) && ResourceLoader.Exists(item.IconPath))
            {
                if (iconRect != null) iconRect.Texture = GD.Load<Texture2D>(item.IconPath);
                if (label != null) label.Text = "";
            }
            else if (hasItem)
            {
                // Has item but no icon — show abbreviated name
                if (iconRect != null) iconRect.Texture = null;
                if (label != null) label.Text = item.DisplayName.Substring(0, Math.Min(3, item.DisplayName.Length));
            }
            else
            {
                if (iconRect != null) iconRect.Texture = null;
                if (label != null) label.Text = "";
            }
        }
    }

    public override void _ExitTree()
    {
        if (_tooltip != null && _tooltip.GetParent() != null)
        {
            _tooltip.GetParent().RemoveChild(_tooltip);
            _tooltip.QueueFree();
        }
    }
}
