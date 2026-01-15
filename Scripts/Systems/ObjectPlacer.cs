using Godot;
using System;
using Archery;

public partial class ObjectPlacer : Node
{
    private ArcherySystem _archerySystem;
    private InteractableObject _currentObject;
    private Vector3 _originalPosition;
    private Vector3 _originalRotation; // Vector3 (Euler)
    private bool _isNewObject = false;

    // Placement State
    private float _currentRotationY = 0.0f;
    private float _currentHeightOffset = 0.0f;

    public bool IsPlacing => _currentObject != null;

    public override void _Ready()
    {
        _archerySystem = GetParent<ArcherySystem>();
    }

    public void StartPlacing(InteractableObject obj)
    {
        if (obj == null) return;

        _currentObject = obj;
        _originalPosition = obj.GlobalPosition;
        _originalRotation = obj.GlobalRotation;

        _currentRotationY = obj.GlobalRotation.Y;
        _currentHeightOffset = 0.0f;

        // Notify ArcherySystem/Player to enter placement mode
        var player = _archerySystem.GetNodeOrNull<PlayerController>("../PlayerPlaceholder"); // Assuming standard path
        if (player == null) player = _archerySystem.GetTree().GetFirstNodeInGroup("player") as PlayerController;

        if (player != null)
        {
            player.CurrentState = PlayerState.PlacingObject;
        }
    }

    public void SpawnAndPlace(InteractableObject obj)
    {
        if (obj == null) return;
        if (obj.GetParent() == null) GetTree().CurrentScene.AddChild(obj);

        StartPlacing(obj);
        _isNewObject = true;

        // Position it roughly in front of camera to start
        var camera = GetViewport().GetCamera3D();
        if (camera != null)
        {
            obj.GlobalPosition = camera.GlobalPosition + camera.ProjectRayNormal(GetViewport().GetMousePosition()) * 5.0f;
        }
    }

    public void ConfirmPlacement()
    {
        if (_currentObject == null) return;

        // Enforce Single-Instance Rule for Tee and Pin
        bool isTee = _currentObject.ObjectName.Contains("Tee");
        bool isPin = _currentObject.ObjectName.Contains("Pin") || _currentObject.ObjectName.Contains("Flag");

        if (isTee || isPin)
        {
            string group = isTee ? "tees" : "pins";
            var existing = GetTree().GetNodesInGroup(group);
            foreach (Node node in existing)
            {
                if (node != _currentObject && IsInstanceValid(node))
                {
                    node.QueueFree();
                }
            }
            _currentObject.AddToGroup(group);
            _currentObject.AddToGroup("targets"); // Ensure distance markers can track it

            // Update ArcherySystem
            if (isTee) _archerySystem?.UpdateTeePosition(_currentObject.GlobalPosition);
            if (isPin) _archerySystem?.UpdatePinPosition(_currentObject.GlobalPosition);
        }

        // NETWORK SPAWN LOGIC
        // If we are connected, send RPC to spawn this object for everyone
        var netManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
        if (netManager != null && netManager.Multiplayer.HasMultiplayerPeer() &&
            (netManager.Multiplayer.IsServer() || netManager.Multiplayer.GetUniqueId() != 1))
        {
            // We have a ghost object (_currentObject). We need to spawn a real networked one.
            // But wait, what if we just placed a Tee/Pin locally logic above?
            // Ideally, Tee/Pin should also be networked.

            // Reconstruct Resource Path (we need to store it in InteractableObject or pass it)
            // For now, assuming ObjectName matches a file in Assets/Textures/Objects or is a known type

            string resourcePath = "";
            // Try to deduce path. This is tricky without storing it.
            // Option: Add 'ResourcePath' property to InteractableObject?
            // For now, let's reverse-lookup or use a naming convention if possible, 
            // OR assumes MainHUD passed valid path info that we stored?
            // ObjectPlacer doesn't store path.

            // QUICK FIX strategy: We don't have the original path here easily unless we stored it.
            // However, _currentObject is an instance. 
            // If we look at MainHUDController, it does: Name = objectId.
            // NetworkManager.SpawnNetworkObject logic re-constructs from path.

            // Let's rely on constructing path from ObjectName if simple.
            // BUT MainHUDController has the logic "res://Assets/Textures/Objects/" + Name + ".gltf"
            // We should duplicate that logic or store the path.

            // Let's assume standard asset path for now.
            string objName = _currentObject.ObjectName;
            if (objName == "DistanceSign" || objName == "TeePin" || objName == "Pin" || objName == "CourseMap")
            {
                // Known types
                if (objName == "DistanceSign") resourcePath = "res://Scenes/Environment/DistanceMarker.tscn";
                else if (objName == "CourseMap") resourcePath = "res://Scenes/Environment/CourseMapSign.tscn";
                else resourcePath = "res://Scenes/Environment/DistanceMarker.tscn"; // Fallback
            }
            else
            {
                // Dynamic Asset
                resourcePath = "res://Assets/Textures/Objects/" + objName + ".gltf";
            }

            netManager.RpcId(1, nameof(NetworkManager.RequestSpawnObject), resourcePath, _currentObject.GlobalPosition, _currentObject.GlobalRotation, _currentObject.Scale);

            // Remove local ghost immediately
            _currentObject.QueueFree();
            GD.Print($"ObjectPlacer: Sent Network Spawn Request for {objName}");
        }
        else
        {
            // Local Only (Offline)
            GD.Print($"ObjectPlacer: Placed {_currentObject.ObjectName} (Local)");
        }

        _currentObject = null;
        _isNewObject = false;

        ExitPlacementMode();
    }

    public void CancelPlacement()
    {
        if (_currentObject == null) return;

        if (_isNewObject)
        {
            _currentObject.QueueFree();
        }
        else
        {
            // Revert
            _currentObject.GlobalPosition = _originalPosition;
            _currentObject.GlobalRotation = _originalRotation;
        }

        GD.Print($"ObjectPlacer: Cancelled placement of {_currentObject.Name}");
        _currentObject = null;
        _isNewObject = false;

        ExitPlacementMode();
    }

    private void ExitPlacementMode()
    {
        var player = _archerySystem.GetNodeOrNull<PlayerController>("../PlayerPlaceholder");
        if (player == null) player = _archerySystem.GetTree().GetFirstNodeInGroup("player") as PlayerController;

        if (player != null)
        {
            player.CurrentState = PlayerState.WalkMode; // Return to moving
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_currentObject == null) return;

        // Perform Raycast
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var mousePos = GetViewport().GetMousePosition();
        var from = camera.ProjectRayOrigin(mousePos);
        var to = from + camera.ProjectRayNormal(mousePos) * 100.0f;

        var spaceState = _currentObject.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 3; // Layer 1 (Default/Rough) + Layer 2 (Terrain/Heightmap)

        // Exclude the object's children to prevent self-intersection
        var exclude = new Godot.Collections.Array<Rid>();

        // Note: _currentObject is a Node3D, so we only check children for colliders
        var kids = _currentObject.FindChildren("*", "CollisionObject3D", true, false);
        foreach (var k in kids)
        {
            if (k is CollisionObject3D childCol) exclude.Add(childCol.GetRid());
        }
        query.Exclude = exclude;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];

            // Apply Height Offset
            Vector3 targetPos = hitPos + new Vector3(0, _currentHeightOffset, 0);

            _currentObject.GlobalPosition = _currentObject.GlobalPosition.Lerp(targetPos, 20.0f * (float)delta);

            // Apply Rotation
            Vector3 currentRot = _currentObject.GlobalRotation;
            _currentObject.GlobalRotation = new Vector3(currentRot.X, _currentRotationY, currentRot.Z);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_currentObject == null) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                ConfirmPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                if (mb.CtrlPressed)
                    _currentObject.Scale *= 1.1f;
                else if (mb.ShiftPressed)
                    _currentHeightOffset += 0.1f;
                else
                    _currentRotationY += Mathf.DegToRad(15.0f);

                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (mb.CtrlPressed)
                    _currentObject.Scale *= 0.9f;
                else if (mb.ShiftPressed)
                    _currentHeightOffset -= 0.1f;
                else
                    _currentRotationY -= Mathf.DegToRad(15.0f);

                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventKey k && k.Pressed)
        {
            if (k.Keycode == Key.Escape)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
