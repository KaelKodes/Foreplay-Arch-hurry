using Godot;
using System;

namespace Archery;

public enum ToolType
{
    None = 0,
    Hammer = 1,   // Object Placement
    Shovel = 2,   // Survey/Terrain Tools
    Bow = 3,      // Archery Combat
    Sword = 4     // Melee Combat (future)
}

/// <summary>
/// Represents an item that can be equipped in a hotbar slot.
/// </summary>
public class ToolItem
{
    public ToolType Type { get; set; } = ToolType.None;
    public string DisplayName { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string WeaponScenePath { get; set; } = ""; // Path to visual weapon scene

    public ToolItem() { }

    public ToolItem(ToolType type, string name, string iconPath = "", string weaponPath = "")
    {
        Type = type;
        DisplayName = name;
        IconPath = iconPath;
        WeaponScenePath = weaponPath;
    }
}

/// <summary>
/// Singleton manager for player tool/weapon selection.
/// Add as Autoload in Project Settings.
/// </summary>
public partial class ToolManager : Node
{
    public static ToolManager Instance { get; private set; }

    [Export] public int HotbarSlotCount = 8;

    // Signals
    [Signal] public delegate void ToolChangedEventHandler(int toolType);
    [Signal] public delegate void HotbarUpdatedEventHandler();

    // State
    public ToolType CurrentTool { get; private set; } = ToolType.None;
    public ToolItem[] HotbarSlots { get; private set; }

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            QueueFree();
            return;
        }

        InitializeDefaultHotbar();
    }

    private void InitializeDefaultHotbar()
    {
        HotbarSlots = new ToolItem[HotbarSlotCount];

        // Default tool layout: 1: Sword, 2: Bow, 3: Hammer, 4: Shovel
        HotbarSlots[0] = new ToolItem(ToolType.Sword, "Sword", "res://Assets/UI/Icons/icon_sword.png", "res://Scenes/Weapons/Sword.tscn");
        HotbarSlots[1] = new ToolItem(ToolType.Bow, "Bow", "res://Assets/UI/Icons/icon_bow.png", "res://Scenes/Weapons/Bow.tscn");
        HotbarSlots[2] = new ToolItem(ToolType.Hammer, "Hammer", "res://Assets/UI/Icons/icon_hammer.png", "");
        HotbarSlots[3] = new ToolItem(ToolType.Shovel, "Shovel", "res://Assets/UI/Icons/icon_shovel.png", "");

        // Empty slots for future items
        for (int i = 4; i < HotbarSlotCount; i++)
        {
            HotbarSlots[i] = new ToolItem(ToolType.None, "", "", "");
        }

        EmitSignal(SignalName.HotbarUpdated);
    }

    /// <summary>
    /// Swaps the items in two hotbar slots.
    /// </summary>
    public void SwapSlots(int index1, int index2)
    {
        if (index1 < 0 || index1 >= HotbarSlotCount || index2 < 0 || index2 >= HotbarSlotCount) return;

        ToolItem temp = HotbarSlots[index1];
        HotbarSlots[index1] = HotbarSlots[index2];
        HotbarSlots[index2] = temp;

        EmitSignal(SignalName.HotbarUpdated);
        GD.Print($"[ToolManager] Swapped slot {index1} with {index2}");
    }

    /// <summary>
    /// Select a tool by slot index (0-7 for hotbar slots 1-8).
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= HotbarSlotCount) return;
        if (HotbarSlots[slotIndex] == null) return;

        SelectTool(HotbarSlots[slotIndex].Type);
    }

    /// <summary>
    /// Select a tool by type. If the same tool is selected, deselect it.
    /// </summary>
    public void SelectTool(ToolType tool)
    {
        if (CurrentTool == tool)
        {
            // Toggle off
            CurrentTool = ToolType.None;
        }
        else
        {
            CurrentTool = tool;
        }

        GD.Print($"[ToolManager] Tool changed to: {CurrentTool}");
        EmitSignal(SignalName.ToolChanged, (int)CurrentTool);
    }

    /// <summary>
    /// Check if a specific tool is currently active.
    /// </summary>
    public bool IsToolActive(ToolType tool) => CurrentTool == tool;

    /// <summary>
    /// Get the ToolItem for a specific slot.
    /// </summary>
    public ToolItem GetSlotItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= HotbarSlotCount) return null;
        return HotbarSlots[slotIndex];
    }

    /// <summary>
    /// Swap an item into a hotbar slot.
    /// </summary>
    public void SetSlotItem(int slotIndex, ToolItem item)
    {
        if (slotIndex < 0 || slotIndex >= HotbarSlotCount) return;
        HotbarSlots[slotIndex] = item ?? new ToolItem();
        EmitSignal(SignalName.HotbarUpdated);
    }

    public override void _Input(InputEvent @event)
    {
        // Handle number key shortcuts (1-8)
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            int slotIndex = -1;
            switch (key.Keycode)
            {
                case Key.Key1: slotIndex = 0; break;
                case Key.Key2: slotIndex = 1; break;
                case Key.Key3: slotIndex = 2; break;
                case Key.Key4: slotIndex = 3; break;
                case Key.Key5: slotIndex = 4; break;
                case Key.Key6: slotIndex = 5; break;
                case Key.Key7: slotIndex = 6; break;
                case Key.Key8: slotIndex = 7; break;
            }

            if (slotIndex >= 0)
            {
                SelectSlot(slotIndex);
            }
        }
    }
}
