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
    public enum HotbarMode { Design, RPG }

    public static ToolManager Instance { get; private set; }

    [Export] public int HotbarSlotCount = 8;
    [Export] public int RPGAbilityCount = 4;

    // Signals
    [Signal] public delegate void ToolChangedEventHandler(int toolType);
    [Signal] public delegate void HotbarUpdatedEventHandler();
    [Signal] public delegate void HotbarModeChangedEventHandler(int newMode);
    [Signal] public delegate void AbilityTriggeredEventHandler(int abilityIndex);

    // State
    public HotbarMode CurrentMode { get; private set; } = HotbarMode.Design;
    public ToolType CurrentTool { get; private set; } = ToolType.None;

    private ToolItem[] _designSlots;
    private ToolItem[] _rpgSlots;
    private ToolItem[] _inventorySlots;

    public ToolItem[] HotbarSlots => CurrentMode == HotbarMode.Design ? _designSlots : _rpgSlots;
    public ToolItem[] InventorySlots => _inventorySlots;

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
        _designSlots = new ToolItem[HotbarSlotCount];
        _rpgSlots = new ToolItem[RPGAbilityCount];
        _inventorySlots = new ToolItem[6]; // As requested: Separate inventory panel with 6 slots

        // Default Design Mode layout
        _designSlots[0] = new ToolItem(ToolType.Sword, "Sword", "res://Assets/UI/Icons/icon_sword.png", "res://Scenes/Weapons/Sword.tscn");
        _designSlots[1] = new ToolItem(ToolType.Bow, "Bow", "res://Assets/UI/Icons/icon_bow.png", "res://Scenes/Weapons/Bow.tscn");
        _designSlots[2] = new ToolItem(ToolType.Hammer, "Hammer", "res://Assets/UI/Icons/icon_hammer.png", "");
        _designSlots[3] = new ToolItem(ToolType.Shovel, "Shovel", "res://Assets/UI/Icons/icon_shovel.png", "");

        for (int i = 4; i < HotbarSlotCount; i++) _designSlots[i] = new ToolItem();

        // Default RPG Mode slots (placeholder until class auto-populates)
        for (int i = 0; i < RPGAbilityCount; i++) _rpgSlots[i] = new ToolItem();

        // Default Inventory
        for (int i = 0; i < 6; i++) _inventorySlots[i] = new ToolItem();

        EmitSignal(SignalName.HotbarUpdated);
    }

    /// <summary>
    /// Updates the RPG ability hotbar with icons for the specific hero class.
    /// </summary>
    public string CurrentHeroClass { get; private set; } = "Ranger";

    public void UpdateRPGAbilities(string heroClass)
    {
        CurrentHeroClass = heroClass;
        string h = heroClass.ToLower();
        string folder = h switch
        {
            "warrior" => "Warrior",
            "ranger" => "Ranger",
            "necromancer" => "Necromancer",
            "cleric" => "Cleric",
            _ => "Warrior"
        };

        string[][] abilityMap = new string[][] {
            new string[] { "ShieldSlam", "Intercept", "DemoralizingShout", "AvatarOfWar" }, // Warrior
            new string[] { "RapidFire", "PiercingShot", "RainOfArrows", "Vault" }, // Ranger
            new string[] { "Lifetap", "PlagueOfDarkness", "SummonSkeleton", "LichForm" }, // Necro
            new string[] { "HighRemedy", "CelestialBuff", "Judgement", "DivineIntervention" } // Cleric
        };

        int classIdx = h switch
        {
            "warrior" => 0,
            "ranger" => 1,
            "necromancer" => 2,
            "cleric" => 3,
            _ => 0
        };

        string[] abilities = abilityMap[classIdx];

        for (int i = 0; i < RPGAbilityCount && i < abilities.Length; i++)
        {
            string iconPath = $"res://Assets/Heroes/{folder}/Spellicons/{abilities[i]}.png";
            _rpgSlots[i] = new ToolItem(ToolType.None, abilities[i], iconPath, "");
        }

        GD.Print($"[ToolManager] Updated RPG Abilities for Hero: {h}");
        EmitSignal(SignalName.HotbarUpdated);
    }

    public void ToggleHotbarMode()
    {
        CurrentMode = CurrentMode == HotbarMode.Design ? HotbarMode.RPG : HotbarMode.Design;
        GD.Print($"[ToolManager] Mode toggled to: {CurrentMode}");
        EmitSignal(SignalName.HotbarModeChanged, (int)CurrentMode);
        EmitSignal(SignalName.HotbarUpdated);
    }

    /// <summary>
    /// Swaps the items in two hotbar slots.
    /// </summary>
    public void SwapSlots(int index1, int index2)
    {
        var slots = HotbarSlots;
        if (index1 < 0 || index1 >= slots.Length || index2 < 0 || index2 >= slots.Length) return;

        ToolItem temp = slots[index1];
        slots[index1] = slots[index2];
        slots[index2] = temp;

        EmitSignal(SignalName.HotbarUpdated);
    }

    /// <summary>
    /// Select a tool by slot index.
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        var slots = HotbarSlots;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        if (slots[slotIndex] == null) return;

        if (CurrentMode == HotbarMode.Design)
        {
            SelectTool(slots[slotIndex].Type);
        }
        else
        {
            // RPG Ability trigger logic
            GD.Print($"[ToolManager] RPG Ability {slotIndex + 1} Triggered");
            EmitSignal(SignalName.AbilityTriggered, slotIndex);
        }
    }

    public void SelectTool(ToolType tool)
    {
        CurrentTool = (CurrentTool == tool) ? ToolType.None : tool;
        EmitSignal(SignalName.ToolChanged, (int)CurrentTool);
    }

    public bool IsToolActive(ToolType tool) => CurrentTool == tool;

    public ToolItem GetSlotItem(int slotIndex)
    {
        var slots = HotbarSlots;
        if (slotIndex < 0 || slotIndex >= slots.Length) return null;
        return slots[slotIndex];
    }

    public void SetSlotItem(int slotIndex, ToolItem item)
    {
        var slots = HotbarSlots;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        slots[slotIndex] = item ?? new ToolItem();
        EmitSignal(SignalName.HotbarUpdated);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            // Shift + B Toggle
            if (key.Keycode == Key.B && key.ShiftPressed)
            {
                ToggleHotbarMode();
                GetViewport().SetInputAsHandled();
                return;
            }

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
                // In RPG mode, only 1-4 are valid for abilities
                if (CurrentMode == HotbarMode.RPG && slotIndex >= RPGAbilityCount) return;

                SelectSlot(slotIndex);
            }
        }
    }
}
