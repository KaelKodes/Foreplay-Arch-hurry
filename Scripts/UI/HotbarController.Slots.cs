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
            // Instantiate AbilityIcon scene
            var slotInstance = SlotScene.Instantiate<Control>();
            var slot = slotInstance as AbilityIcon;

            if (slot == null)
            {
                GD.PushError("[HotbarController] SlotScene is not an AbilityIcon!");
                continue;
            }

            slot.Name = $"Slot{i + 1}";

            // Set Label
            slot.SetLabel((i + 1).ToString());

            // Add to container and tracker
            _slotContainer.AddChild(slot);
            _slots[i] = slot;

            // Wire up events
            int slotIndex = i; // Capture for lambda
            slot.Pressed += () => OnSlotPressed(slotIndex);
            slot.MouseEntered += () => OnSlotMouseEntered(slotIndex);
            slot.MouseExited += () => OnSlotMouseExited();

            // Drag and Drop (if supported by AbilityIcon - might need to bubble up)
            slot.SetDragForwarding(new Godot.Callable(this, nameof(GetSlotDragData)), new Godot.Callable(this, nameof(CanDropOnSlot)), new Godot.Callable(this, nameof(DropOnSlot)));
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (ToolManager.Instance == null) return;

        for (int i = 0; i < SlotCount && i < _slots.Length; i++)
        {
            var item = ToolManager.Instance.GetSlotItem(i);
            var slot = _slots[i] as AbilityIcon;
            if (slot == null) continue;

            // Load icon if available
            if (!string.IsNullOrEmpty(item?.IconPath) && ResourceLoader.Exists(item.IconPath))
            {
                slot.SetIcon(GD.Load<Texture2D>(item.IconPath));
                // slot.SetNameVisible(false); // If we add this method later
            }
            else
            {
                slot.SetIcon(null);
                // slot.SetName(item?.DisplayName ?? "");
            }
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
            var slot = _slots[i] as AbilityIcon;
            if (slot == null) continue;

            if (slot.UpgradeButton != null)
            {
                int lvl = _cachedStatsService.PlayerStats.AbilityLevels[i];
                // Only show if we have points, it's one of the first 4 slots, AND level < 6
                slot.UpgradeButton.Visible = hasPoints && i < 4 && lvl < 6;
            }

            if (slot.LevelLabel != null)
            {
                int lvl = _cachedStatsService.PlayerStats.AbilityLevels[i];
                slot.LevelLabel.Text = $"Lvl {lvl}";
                slot.LevelLabel.Visible = true;
            }
        }
    }

    private void HideAllUpgrades()
    {
        foreach (var btn in _slots)
        {
            var slot = btn as AbilityIcon;
            if (slot == null) continue;

            if (slot.UpgradeButton != null) slot.UpgradeButton.Visible = false;
            if (slot.LevelLabel != null) slot.LevelLabel.Visible = false;
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
