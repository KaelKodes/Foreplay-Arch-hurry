using Godot;
using System;

namespace Archery;

public partial class HotbarController
{
    private void CreateSlots()
    {
        // Clear existing slots
        foreach (Node child in _slotContainer.GetChildren())
        {
            child.QueueFree();
        }

        _slots = new TextureButton[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            var slot = new TextureButton();
            slot.Name = $"Slot{i + 1}";
            slot.CustomMinimumSize = new Vector2(64, 64);
            slot.StretchMode = TextureButton.StretchModeEnum.Scale;
            slot.IgnoreTextureSize = true;

            // Create a styled slot background using MobaTheme
            var slotPanel = new Panel();
            slotPanel.Name = "Background";
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
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);
            slotPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slotPanel.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(slotPanel);

            // Add icon rect (layered on top of background)
            var iconRect = new TextureRect();
            iconRect.Name = "IconRect";
            iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconRect.OffsetLeft = 4;
            iconRect.OffsetTop = 4;
            iconRect.OffsetRight = -4;
            iconRect.OffsetBottom = -12; // Leave room for number label
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(iconRect);

            // Add number label with themed styling
            var label = new Label();
            label.Name = "NumberLabel";
            label.Text = (i + 1).ToString();
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Bottom;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", MobaTheme.TextMuted);
            label.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(label);

            // Add tool name label
            var nameLabel = new Label();
            nameLabel.Name = "NameLabel";
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.VerticalAlignment = VerticalAlignment.Center;
            nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(nameLabel);

            // Selection highlight with themed glow
            var highlight = new ColorRect();
            highlight.Name = "Highlight";
            highlight.Color = new Color(0.4f, 0.6f, 1f, 0.25f);
            highlight.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            highlight.Visible = false;
            highlight.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(highlight);

            int slotIndex = i; // Capture for lambda
            slot.Pressed += () => OnSlotPressed(slotIndex);

            // Ability tooltip on hover (RPG mode only)
            slot.MouseEntered += () => OnSlotMouseEntered(slotIndex);
            slot.MouseExited += () => OnSlotMouseExited();

            // Add Upgrade Button (+)
            var upgradeBtn = new Button();
            upgradeBtn.Name = "UpgradeButton";
            upgradeBtn.Text = "+";
            upgradeBtn.CustomMinimumSize = new Vector2(24, 24);
            upgradeBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            upgradeBtn.OffsetLeft = -24;
            upgradeBtn.OffsetBottom = 24;
            upgradeBtn.Visible = false; // Hidden by default
            upgradeBtn.AddThemeFontSizeOverride("font_size", 14);
            upgradeBtn.AddThemeColorOverride("font_color", MobaTheme.AccentGold);

            // Styled button
            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            btnStyle.BorderWidthBottom = 2;
            btnStyle.BorderColor = MobaTheme.AccentGold;
            upgradeBtn.AddThemeStyleboxOverride("normal", btnStyle);

            upgradeBtn.Pressed += () => OnUpgradePressed(slotIndex);
            slot.AddChild(upgradeBtn);

            // Re-center Upgrade Button over icon
            upgradeBtn.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            upgradeBtn.OffsetLeft = -12;
            upgradeBtn.OffsetTop = -12;
            upgradeBtn.OffsetRight = 12;
            upgradeBtn.OffsetBottom = 12;
            // Add Level Label - Moved to top
            var lvlLabel = new Label();
            lvlLabel.Name = "LevelLabel";
            lvlLabel.Text = "Lvl 1";
            lvlLabel.HorizontalAlignment = HorizontalAlignment.Right;
            lvlLabel.VerticalAlignment = VerticalAlignment.Top;
            lvlLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            lvlLabel.OffsetRight = -4;
            lvlLabel.OffsetTop = 2; // Positioned at top
            lvlLabel.AddThemeFontSizeOverride("font_size", 9);
            lvlLabel.AddThemeColorOverride("font_color", MobaTheme.AccentGold);
            lvlLabel.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(lvlLabel);

            // DRAG AND DROP SETUP
            slot.SetDragForwarding(new Godot.Callable(this, nameof(GetSlotDragData)), new Godot.Callable(this, nameof(CanDropOnSlot)), new Godot.Callable(this, nameof(DropOnSlot)));

            _slotContainer.AddChild(slot);
            _slots[i] = slot;
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (ToolManager.Instance == null) return;

        for (int i = 0; i < SlotCount && i < _slots.Length; i++)
        {
            var item = ToolManager.Instance.GetSlotItem(i);
            var nameLabel = _slots[i].GetNodeOrNull<Label>("NameLabel");
            var iconRect = _slots[i].GetNodeOrNull<TextureRect>("IconRect");
            var slot = _slots[i];

            // Load icon if available (into the IconRect, which is layered above background)
            if (!string.IsNullOrEmpty(item?.IconPath) && ResourceLoader.Exists(item.IconPath))
            {
                if (iconRect != null) iconRect.Texture = GD.Load<Texture2D>(item.IconPath);
                if (nameLabel != null) nameLabel.Visible = false; // Hide text when icon is shown
            }
            else
            {
                if (iconRect != null) iconRect.Texture = null;
                if (nameLabel != null && item != null)
                {
                    nameLabel.Text = item.DisplayName;
                    nameLabel.Visible = true;
                }
            }

            // Ensure slot background is visible
            var bg = slot.GetNodeOrNull<Panel>("Background");
            if (bg != null) bg.Visible = true;
        }

        // Ensure highlight is correct after refresh
        OnToolChanged((int)(ToolManager.Instance?.CurrentTool ?? ToolType.None));
        RefreshUpgradeVisibility();
    }

    private void RefreshUpgradeVisibility()
    {
        if (ToolManager.Instance == null || ToolManager.Instance.CurrentMode != ToolManager.HotbarMode.RPG)
        {
            HideAllUpgrades();
            return;
        }

        // Reliable lookup via local_player group
        if (_cachedStatsService == null)
        {
            var player = GetTree().GetFirstNodeInGroup("local_player") as Node;
            var arch = player?.FindChild("ArcherySystem", true, false) as ArcherySystem;
            _cachedStatsService = arch?.GetNodeOrNull<StatsService>("StatsService");

            if (_cachedStatsService != null)
            {
                // Unsub first to be safe
                _cachedStatsService.AbilityUpgraded -= (s, l, p) => RefreshUpgradeVisibility();
                _cachedStatsService.AbilityUpgraded += (s, l, p) => RefreshUpgradeVisibility();
                _cachedStatsService.LevelUp -= (l) => RefreshUpgradeVisibility();
                _cachedStatsService.LevelUp += (l) => RefreshUpgradeVisibility();
            }
        }

        if (_cachedStatsService == null) return;

        bool hasPoints = _cachedStatsService.PlayerStats.AbilityPoints > 0;

        for (int i = 0; i < _slots.Length; i++)
        {
            var upgradeBtn = _slots[i].GetNodeOrNull<Button>("UpgradeButton");
            var lvlLabel = _slots[i].GetNodeOrNull<Label>("LevelLabel");

            if (upgradeBtn != null)
            {
                int lvl = _cachedStatsService.PlayerStats.AbilityLevels[i];
                // Only show if we have points, it's one of the first 4 slots, AND level < 6
                upgradeBtn.Visible = hasPoints && i < 4 && lvl < 6;
            }

            if (lvlLabel != null)
            {
                int lvl = _cachedStatsService.PlayerStats.AbilityLevels[i];
                lvlLabel.Text = $"Lvl {lvl}";
                lvlLabel.Visible = true;
            }
        }
    }

    private void HideAllUpgrades()
    {
        foreach (var slot in _slots)
        {
            if (slot == null) continue;
            var upgradeBtn = slot.GetNodeOrNull<Button>("UpgradeButton");
            var lvlLabel = slot.GetNodeOrNull<Label>("LevelLabel");
            if (upgradeBtn != null) upgradeBtn.Visible = false;
            if (lvlLabel != null) lvlLabel.Visible = false;
        }
    }

    // --- Drag and Drop Logic ---

    private Variant GetSlotDragData(Vector2 atPosition)
    {
        // Find which slot was clicked
        int index = -1;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].GetGlobalRect().HasPoint(GetGlobalMousePosition()))
            {
                index = i;
                break;
            }
        }

        if (index == -1) return default;

        var item = ToolManager.Instance?.GetSlotItem(index);
        if (item == null || item.Type == ToolType.None) return default;

        _draggedSlotIndex = index;

        // Create preview
        var preview = new Control();
        var icon = new TextureRect();
        if (!string.IsNullOrEmpty(item.IconPath) && ResourceLoader.Exists(item.IconPath))
        {
            icon.Texture = GD.Load<Texture2D>(item.IconPath);
        }
        else
        {
            // Placeholder color preview
            var rect = new ColorRect();
            rect.Color = SlotColors[index];
            rect.Size = new Vector2(64, 64);
            preview.AddChild(rect);
        }
        icon.Size = new Vector2(64, 64);
        preview.AddChild(icon);
        SetDragPreview(preview);

        return index;
    }

    private bool CanDropOnSlot(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }

    private void DropOnSlot(Vector2 atPosition, Variant data)
    {
        int fromIndex = (int)data;

        // Find drop index
        int toIndex = -1;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].GetGlobalRect().HasPoint(GetGlobalMousePosition()))
            {
                toIndex = i;
                break;
            }
        }

        if (toIndex != -1 && fromIndex != toIndex)
        {
            ToolManager.Instance?.SwapSlots(fromIndex, toIndex);
        }
    }
}
