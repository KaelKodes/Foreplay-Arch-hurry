using Godot;

namespace Archery;

/// <summary>
/// Manages weapon visibility on the player based on current tool selection.
/// Attach to the player scene. Requires ToolManager singleton.
/// </summary>
public partial class WeaponHolder : Node3D
{
    [Export] public NodePath BowNodePath;
    [Export] public NodePath SwordNodePath;

    private Node3D _bowNode;
    private Node3D _swordNode;
    private ToolType _currentWeapon = ToolType.None;

    // Multiplayer sync
    [Export] public int OwnerPlayerId = -1;

    public override void _Ready()
    {
        // Load weapon nodes if paths are set
        if (!string.IsNullOrEmpty(BowNodePath?.ToString()))
        {
            _bowNode = GetNodeOrNull<Node3D>(BowNodePath);
        }
        if (!string.IsNullOrEmpty(SwordNodePath?.ToString()))
        {
            _swordNode = GetNodeOrNull<Node3D>(SwordNodePath);
        }

        // Subscribe to tool changes
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
        }
        else
        {
            // ToolManager might not be ready yet, wait for it
            CallDeferred(nameof(DeferredSubscribe));
        }

        // Hide all weapons initially
        UpdateWeaponVisibility(ToolType.None);
    }

    private void DeferredSubscribe()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged += OnToolChanged;
        }
    }

    public override void _ExitTree()
    {
        if (ToolManager.Instance != null)
        {
            ToolManager.Instance.ToolChanged -= OnToolChanged;
        }
    }

    private void OnToolChanged(int toolType)
    {
        // Only respond to tool changes if this WeaponHolder belongs to the local player
        // This prevents the multiplayer crosstalk bug where all players change weapons together
        var parentPlayer = GetParentOrNull<PlayerController>();
        if (parentPlayer != null && !parentPlayer.IsLocal)
        {
            return; // Remote player - ignore global ToolManager signal, use SynchronizedTool instead
        }

        UpdateWeaponVisibility((ToolType)toolType);
    }

    /// <summary>
    /// Update which weapon model is visible based on the active tool.
    /// </summary>
    public void UpdateWeaponVisibility(ToolType tool)
    {
        _currentWeapon = tool;

        // Hide all first
        if (_bowNode != null) _bowNode.Visible = false;
        if (_swordNode != null) _swordNode.Visible = false;

        // Show the active weapon
        switch (tool)
        {
            case ToolType.Bow:
                if (_bowNode != null) _bowNode.Visible = true;
                break;
            case ToolType.Sword:
                if (_swordNode != null) _swordNode.Visible = true;
                break;
        }
    }

    /// <summary>
    /// Spawn and attach a weapon from a scene path.
    /// Used to dynamically load weapon models.
    /// </summary>
    public void LoadWeaponModel(ToolType type, string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return;
        if (!ResourceLoader.Exists(scenePath)) return;

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null) return;

        var instance = scene.Instantiate<Node3D>();

        switch (type)
        {
            case ToolType.Bow:
                if (_bowNode != null) _bowNode.QueueFree();
                _bowNode = instance;
                _bowNode.Name = "BowModel";
                AddChild(_bowNode);
                break;
            case ToolType.Sword:
                if (_swordNode != null) _swordNode.QueueFree();
                _swordNode = instance;
                _swordNode.Name = "SwordModel";
                AddChild(_swordNode);
                break;
        }

        // Update visibility
        UpdateWeaponVisibility(_currentWeapon);
    }
}
