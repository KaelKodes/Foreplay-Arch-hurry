using Godot;
using System;

namespace Archery;

public partial class HotbarController
{
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || ToolManager.Instance?.CurrentMode != ToolManager.HotbarMode.RPG) return;

        // Priority to Perk Selection hotkeys
        var mobaHud = GetTree().Root.FindChild("MobaHUD", true, false) as MobaHUD;
        if (mobaHud != null && mobaHud.IsSelectingPerk) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.AltPressed)
        {
            int slot = -1;
            switch (keyEvent.Keycode)
            {
                case Key.Key1: slot = 0; break;
                case Key.Key2: slot = 1; break;
                case Key.Key3: slot = 2; break;
                case Key.Key4: slot = 3; break;
            }

            if (slot != -1)
            {
                OnUpgradePressed(slot);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void OnUpgradePressed(int slotIndex)
    {
        GD.Print($"[Hotbar] Attempting Upgrade for Slot {slotIndex + 1}");
        if (_cachedStatsService != null)
        {
            _cachedStatsService.UpgradeAbility(slotIndex);
            RefreshUpgradeVisibility();
        }
        else
        {
            GD.PrintErr("[Hotbar] Cannot upgrade: StatsService not found!");
        }
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
            var slot = _slots[i] as AbilityIcon;
            if (slot == null || slot.HighlightOverlay == null) continue;

            var item = ToolManager.Instance?.GetSlotItem(i);
            // Highlight if this slot contains the active tool
            if (item != null && (int)item.Type == toolType && toolType != (int)ToolType.None)
            {
                slot.HighlightOverlay.Visible = true;
                _selectedSlot = i;
            }
            else
            {
                slot.HighlightOverlay.Visible = false;
            }
        }
    }

    // ── Tooltip Hover Logic ─────────────────────────────────

    private void OnSlotMouseEntered(int slotIndex)
    {
        if (_abilityTooltip == null) return;
        if (ToolManager.Instance == null) return;
        if (ToolManager.Instance.CurrentMode != ToolManager.HotbarMode.RPG) return;

        var item = ToolManager.Instance.GetSlotItem(slotIndex);
        if (item == null || string.IsNullOrEmpty(item.DisplayName)) return;

        var abilityInfo = AbilityData.Get(item.DisplayName);
        if (abilityInfo == null) return;

        // Position tooltip above the hovered slot
        if (slotIndex < _slots.Length && _slots[slotIndex] != null)
        {
            var slotRect = _slots[slotIndex].GetGlobalRect();
            var tooltipPos = new Vector2(
                slotRect.Position.X + slotRect.Size.X / 2f,
                slotRect.Position.Y
            );

            Stats stats = _cachedStatsService?.PlayerStats;
            _abilityTooltip.ShowAbility(abilityInfo, tooltipPos, stats);
        }
    }

    private void OnSlotMouseExited()
    {
        if (_abilityTooltip != null && _abilityTooltip.Visible)
        {
            // If mouse moved from slot to tooltip, don't hide
            if (_abilityTooltip.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
            {
                return;
            }
        }
        _abilityTooltip?.HideTooltip();
    }
}
