using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    private Container _slotContainer;
    private TextureButton[] _slots;
    private int _selectedSlot = -1;

    // Draggable Hotbar variables
    private bool _isDraggingPanel = false;
    private Vector2 _dragOffset = Vector2.Zero;

    // Slot Drag and Drop variables
    private int _draggedSlotIndex = -1;
    private Control _dragPreview;

    // Ability Tooltip
    private AbilityTooltip _abilityTooltip;

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

    private StatsService _cachedStatsService;

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

        // MOBA Specific Defaults
        if (MobaGameManager.Instance != null)
        {
            _currentLayout = HotbarLayout.Vertical8x1;
            SetAnchorsPreset(LayoutPreset.CenterRight);
            OffsetTop = -300; // Position vertically centered-ish
            GD.Print("[Hotbar] MOBA Mode detected: Defaulting to Vertical Right-Side layout.");
        }

        CreateSlots();
        RebuildContainer(); // Force initial layout rebuild

        // Subscribe to ToolManager
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
            ToolManager.Instance.HotbarModeChanged += (m) => OnHotbarModeChanged((ToolManager.HotbarMode)m);

            // Sync initial state
            SlotCount = ToolManager.Instance.CurrentMode == ToolManager.HotbarMode.Design ? 8 : 4;
            RebuildContainer();
        }
        else
        {
            CallDeferred(nameof(DeferredSubscribe));
        }
        // Style the hotbar background with MobaTheme
        var bg = GetNodeOrNull<Control>("Background");
        if (bg is ColorRect bgRect)
        {
            bgRect.Color = MobaTheme.PanelBg;
            bgRect.GuiInput += OnBackgroundGuiInput;
        }
        else if (bg != null)
        {
            bg.GuiInput += OnBackgroundGuiInput;
        }

        // Create ability tooltip (shared across all slots)
        _abilityTooltip = new AbilityTooltip();
        _abilityTooltip.Name = "AbilityTooltip";
        AddChild(_abilityTooltip);
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
}
