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

        // MOBA Specific Defaults - REMOVED to allow HUD.tscn to control layout
        // if (MobaGameManager.Instance != null) ...

        // Fallback: Load AbilityIcon.tscn if SlotScene not assigned
        if (SlotScene == null)
        {
            SlotScene = GD.Load<PackedScene>("res://Scenes/UI/AbilityIcon.tscn");
        }

        // Force layout state
        SetAnchorsPreset(LayoutPreset.CenterBottom);
        OffsetBottom = -20;

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

        // Always try to subscribe to player (independent of ToolManager)
        CallDeferred(nameof(SubscribeToPlayer));
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
        _abilityTooltip.MouseExited += OnSlotMouseExited;
        AddChild(_abilityTooltip);
    }

    private PlayerController _cachedPlayer;

    private void DeferredSubscribe()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
            ToolManager.Instance.HotbarUpdated += RefreshSlots;
        }

        SubscribeToPlayer();
    }

    private void SubscribeToPlayer()
    {
        if (_cachedPlayer != null) return;

        var playerNode = GetTree().GetFirstNodeInGroup("local_player");
        if (playerNode is PlayerController player)
        {
            _cachedPlayer = player;
            _cachedPlayer.AbilityUsed += OnAbilityUsed;
            GD.Print("[HotbarController] Subscribed to Player AbilityUsed event.");
        }
        else
        {
            // Retry later if player isn't ready
            GetTree().CreateTimer(0.5f).Timeout += SubscribeToPlayer;
        }
    }

    private void OnAbilityUsed(int slotIndex, float duration)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Length)
        {
            var slot = _slots[slotIndex] as AbilityIcon;
            slot?.StartCooldown(duration);
        }
    }

    public override void _ExitTree()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged -= OnToolChanged;
            ToolManager.Instance.HotbarUpdated -= RefreshSlots;
        }

        if (_cachedPlayer != null)
        {
            _cachedPlayer.AbilityUsed -= OnAbilityUsed;
            _cachedPlayer = null;
        }
    }
}
