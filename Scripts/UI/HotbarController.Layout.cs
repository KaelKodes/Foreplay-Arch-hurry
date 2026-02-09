using Godot;
using System;

namespace Archery;

public partial class HotbarController
{
    // Layout modes
    public enum HotbarLayout { Horizontal1x8, Vertical8x1, Grid2x4, Grid4x2 }
    private HotbarLayout _currentLayout = HotbarLayout.Horizontal1x8;

    private void OnHotbarModeChanged(ToolManager.HotbarMode mode)
    {
        SlotCount = mode == ToolManager.HotbarMode.Design ? 8 : 4;

        if (mode == ToolManager.HotbarMode.RPG)
        {
            _currentLayout = HotbarLayout.Horizontal1x8; // Force horizontal for RPG
            SetAnchorsPreset(LayoutPreset.CenterBottom);
            OffsetBottom = -20;
            OffsetTop = -100;
        }
        else
        {
            SetAnchorsPreset(LayoutPreset.CenterTop);
            OffsetTop = 80;
            OffsetBottom = 160;
        }

        GD.Print($"[HotbarController] Mode changed: {mode}. SlotCount: {SlotCount}");
        RebuildContainer();
    }

    /// <summary>
    /// Cycles to the next hotbar layout mode.
    /// </summary>
    public void CycleLayout()
    {
        _currentLayout = _currentLayout switch
        {
            HotbarLayout.Horizontal1x8 => HotbarLayout.Vertical8x1,
            HotbarLayout.Vertical8x1 => HotbarLayout.Grid2x4,
            HotbarLayout.Grid2x4 => HotbarLayout.Grid4x2,
            HotbarLayout.Grid4x2 => HotbarLayout.Horizontal1x8,
            _ => HotbarLayout.Horizontal1x8
        };
        GD.Print($"[Hotbar] Layout changed to: {_currentLayout}");
        RebuildContainer();
    }

    /// <summary>
    /// Rebuilds the slot container with the current layout.
    /// </summary>
    private void RebuildContainer()
    {
        // Remove old container
        _slotContainer?.QueueFree();

        // Create new container based on layout
        Container newContainer;
        switch (_currentLayout)
        {
            case HotbarLayout.Vertical8x1:
                var vbox = new VBoxContainer();
                vbox.Alignment = BoxContainer.AlignmentMode.Center;
                newContainer = vbox;
                break;
            case HotbarLayout.Grid2x4:
                var grid24 = new GridContainer();
                grid24.Columns = 4;
                newContainer = grid24;
                break;
            case HotbarLayout.Grid4x2:
                var grid42 = new GridContainer();
                grid42.Columns = 2;
                newContainer = grid42;
                break;
            case HotbarLayout.Horizontal1x8:
            default:
                var hbox = new HBoxContainer();
                hbox.Alignment = BoxContainer.AlignmentMode.Center;
                newContainer = hbox;
                break;
        }

        newContainer.Name = "SlotContainer";
        newContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        newContainer.OffsetLeft = 8;
        newContainer.OffsetTop = 8;
        newContainer.OffsetRight = -8;
        newContainer.OffsetBottom = -8;
        AddChild(newContainer);
        // Move to index 1 so it's AFTER the Background (index 0)
        MoveChild(newContainer, 1);
        _slotContainer = newContainer;

        CreateSlots();
        RefreshSlots();
        RefreshUpgradeVisibility();

        // Defer resize to next frame so container has calculated its size
        CallDeferred(nameof(ResizeToFitContainer));
    }

    private void ResizeToFitContainer()
    {
        if (_slotContainer == null) return;

        // Calculate required size based on layout
        float slotSize = 64f;
        float padding = 16f; // 8px on each side
        float spacing = 4f;

        int cols = SlotCount, rows = 1;
        switch (_currentLayout)
        {
            case HotbarLayout.Vertical8x1:
                cols = 1; rows = SlotCount;
                break;
            case HotbarLayout.Grid2x4:
                cols = SlotCount / 2; rows = 2;
                if (SlotCount % 2 != 0) cols++;
                break;
            case HotbarLayout.Grid4x2:
                cols = 2; rows = SlotCount / 2;
                if (SlotCount % 2 != 0) rows++;
                break;
            case HotbarLayout.Horizontal1x8:
            default:
                cols = SlotCount; rows = 1;
                break;
        }

        float width = (cols * slotSize) + ((cols - 1) * spacing) + padding;
        float height = (rows * slotSize) + ((rows - 1) * spacing) + padding;

        // Resize the Hotbar Control
        CustomMinimumSize = new Vector2(width, height);
        Size = new Vector2(width, height);

        // Recenter anchored position
        if ((LayoutPreset)AnchorsPreset == LayoutPreset.CenterRight)
        {
            OffsetLeft = -width - 20; // 20px padding from right edge
            OffsetRight = -20;
            OffsetTop = -height / 2f;
            OffsetBottom = height / 2f;
        }
        else
        {
            OffsetLeft = -width / 2f;
            OffsetRight = width / 2f;
            OffsetBottom = OffsetTop + height;
        }
    }
}
