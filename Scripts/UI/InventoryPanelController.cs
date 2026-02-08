using Godot;
using System;

namespace Archery;

/// <summary>
/// Controller for the RPG inventory panel. Displays 6 inventory slots in a grid.
/// </summary>
public partial class InventoryPanelController : Control
{
    private Container _slotContainer;
    private TextureButton[] _slots;

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

        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
            ToolManager.Instance.HotbarModeChanged += (m) => OnHotbarModeChanged((ToolManager.HotbarMode)m);

            // Initial visibility
            Visible = ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.RPG;
        }
    }

    private void OnHotbarModeChanged(ToolManager.HotbarMode mode)
    {
        Visible = (mode == ToolManager.HotbarMode.RPG);
        if (Visible) RefreshSlots();
    }

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

            // Background
            var bg = new ColorRect();
            bg.Name = "Background";
            bg.Color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
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

            // Number/Hotkey label (not used for inventory usually, but added for consistency)
            var label = new Label();
            label.Name = "Label";
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.VerticalAlignment = VerticalAlignment.Bottom;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(label);

            _slotContainer.AddChild(slot);
            _slots[i] = slot;

            int index = i;
            slot.Pressed += () => GD.Print($"[Inventory] Slot {index} pressed");
        }
    }

    private void RefreshSlots()
    {
        if (ToolManager.Instance == null) return;

        var items = ToolManager.Instance.InventorySlots;
        for (int i = 0; i < 6 && i < items.Length; i++)
        {
            var item = items[i];
            var iconRect = _slots[i].GetNodeOrNull<TextureRect>("IconRect");

            if (item != null && !string.IsNullOrEmpty(item.IconPath) && ResourceLoader.Exists(item.IconPath))
            {
                if (iconRect != null) iconRect.Texture = GD.Load<Texture2D>(item.IconPath);
            }
            else
            {
                if (iconRect != null) iconRect.Texture = null;
            }
        }
    }
}
