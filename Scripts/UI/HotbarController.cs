using Godot;
using System;

namespace Archery;

/// <summary>
/// Controller for the hotbar UI. Displays tool slots and handles selection.
/// Supports multiple layout modes: horizontal, vertical, and grid layouts.
/// </summary>
public partial class HotbarController : Control
{
    [Export] public int SlotCount = 8;
    [Export] public PackedScene SlotScene;
    [Export] public NodePath SlotContainerPath;

    // Layout modes
    public enum HotbarLayout { Horizontal1x8, Vertical8x1, Grid2x4, Grid4x2 }
    private HotbarLayout _currentLayout = HotbarLayout.Horizontal1x8;
    private Container _slotContainer;
    private TextureButton[] _slots;
    private int _selectedSlot = -1;

    // Draggable Hotbar variables
    private bool _isDraggingPanel = false;
    private Vector2 _dragOffset = Vector2.Zero;

    // Slot Drag and Drop variables
    private int _draggedSlotIndex = -1;
    private Control _dragPreview;

    // Placeholder colors for slots without icons
    private static readonly Color[] SlotColors = new Color[]
    {
        new Color(0.6f, 0.2f, 0.2f),  // Sword - Red
        new Color(0.2f, 0.6f, 0.2f),  // Bow - Green
        new Color(0.6f, 0.4f, 0.2f),  // Hammer - Brown
		new Color(0.5f, 0.5f, 0.5f),  // Shovel - Gray
		new Color(0.3f, 0.3f, 0.3f),  // Empty
		new Color(0.3f, 0.3f, 0.3f),
        new Color(0.3f, 0.3f, 0.3f),
        new Color(0.3f, 0.3f, 0.3f),
    };

    public override void _Ready()
    {
        // Find or create slot container
        if (!string.IsNullOrEmpty(SlotContainerPath?.ToString()))
        {
            _slotContainer = GetNodeOrNull<HBoxContainer>(SlotContainerPath);
        }

        if (_slotContainer == null)
        {
            _slotContainer = GetNodeOrNull<HBoxContainer>("SlotContainer");
        }

        if (_slotContainer == null)
        {
            var hbox = new HBoxContainer();
            hbox.Name = "SlotContainer";
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(hbox);
            _slotContainer = hbox;
        }

        CreateSlots();

        // Subscribe to ToolManager
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
        }
        else
        {
            CallDeferred(nameof(DeferredSubscribe));
        }

        // Enable mouse input for the background panel to allow dragging
        var bg = GetNodeOrNull<Control>("Background");
        if (bg != null)
        {
            bg.GuiInput += OnBackgroundGuiInput;
        }
    }

    private void OnBackgroundGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isDraggingPanel = true;
                    _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                }
                else
                {
                    _isDraggingPanel = false;
                }
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                // Cycle to next layout
                CycleLayout();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDraggingPanel)
        {
            GlobalPosition = GetGlobalMousePosition() - _dragOffset;
            GetViewport().SetInputAsHandled();
        }
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

        int cols = 8, rows = 1;
        switch (_currentLayout)
        {
            case HotbarLayout.Vertical8x1:
                cols = 1; rows = 8;
                break;
            case HotbarLayout.Grid2x4:
                cols = 4; rows = 2;
                break;
            case HotbarLayout.Grid4x2:
                cols = 2; rows = 4;
                break;
            case HotbarLayout.Horizontal1x8:
            default:
                cols = 8; rows = 1;
                break;
        }

        float width = (cols * slotSize) + ((cols - 1) * spacing) + padding;
        float height = (rows * slotSize) + ((rows - 1) * spacing) + padding;

        // Resize the Hotbar Control
        CustomMinimumSize = new Vector2(width, height);
        Size = new Vector2(width, height);

        // Recenter anchored position
        OffsetLeft = -width / 2f;
        OffsetRight = width / 2f;
        OffsetBottom = OffsetTop + height;
    }

    private void DeferredSubscribe()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
        }
    }

    public override void _ExitTree()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged -= OnToolChanged;
            ToolManager.Instance.HotbarUpdated -= RefreshSlots;
        }
    }

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

            // Create a background rect
            var colorRect = new ColorRect();
            colorRect.Name = "Background";
            colorRect.Color = new Color(0.15f, 0.15f, 0.15f, 0.8f); // Dark background
            colorRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            colorRect.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(colorRect);

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

            // Add number label
            var label = new Label();
            label.Name = "NumberLabel";
            label.Text = (i + 1).ToString();
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Bottom;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
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

            // Selection highlight
            var highlight = new ColorRect();
            highlight.Name = "Highlight";
            highlight.Color = new Color(1, 1, 1, 0.3f);
            highlight.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            highlight.Visible = false;
            highlight.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(highlight);

            int slotIndex = i; // Capture for lambda
            slot.Pressed += () => OnSlotPressed(slotIndex);

            // DRAG AND DROP SETUP
            slot.SetDragForwarding(new Godot.Callable(this, nameof(GetSlotDragData)), new Godot.Callable(this, nameof(CanDropOnSlot)), new Godot.Callable(this, nameof(DropOnSlot)));

            _slotContainer.AddChild(slot);
            _slots[i] = slot;
        }

        RefreshSlots();
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

            // Show dark background behind all slots (icons or not)
            var bg = slot.GetNodeOrNull<ColorRect>("Background");
            if (bg != null)
            {
                bg.Visible = true;
                bg.Color = new Color(0.15f, 0.15f, 0.15f, 0.8f); // Same dark gray for all slots
            }
        }

        // Ensure highlight is correct after refresh
        OnToolChanged((int)(ToolManager.Instance?.CurrentTool ?? ToolType.None));
    }

    private void OnSlotPressed(int slotIndex)
    {
        GD.Print($"[Hotbar] Slot {slotIndex + 1} pressed");
        ToolManager.Instance?.SelectSlot(slotIndex);
    }

    private void OnToolChanged(int toolType)
    {
        // Update selection highlight
        for (int i = 0; i < _slots.Length; i++)
        {
            var highlight = _slots[i].GetNodeOrNull<ColorRect>("Highlight");
            if (highlight == null) continue;

            var item = ToolManager.Instance?.GetSlotItem(i);
            // Highlight if this slot contains the active tool
            if (item != null && (int)item.Type == toolType && toolType != (int)ToolType.None)
            {
                highlight.Visible = true;
                _selectedSlot = i;
            }
            else
            {
                highlight.Visible = false;
            }
        }
    }
}
