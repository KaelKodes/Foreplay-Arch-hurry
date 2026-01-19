using Godot;
using System;
using Archery;

namespace Archery;

public partial class ObjectPlacer : Node
{
    private ArcherySystem _archerySystem;
    private InteractableObject _currentObject;
    private Vector3 _originalPosition;
    private Vector3 _originalRotation; // Vector3 (Euler)
    private bool _isNewObject = false;
    private string _originalObjectName = ""; // Tracks original for networked move
    private System.Collections.Generic.Dictionary<string, Vector3> _lastScales = new System.Collections.Generic.Dictionary<string, Vector3>();

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
        _originalObjectName = obj.GetParent() != null ? obj.Name : "";

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

        // Apply persistent scale if we have one for this object type
        bool isTower = obj.ObjectName.ToLower().Contains("watch+tower");
        if (_lastScales.ContainsKey(obj.ObjectName))
        {
            Vector3 storedScale = _lastScales[obj.ObjectName];

            // Special Rule: If it's a tower and the stored scale is roughly "default" (0.6 - 1.2),
            // override it with the new preferred default (0.42).
            // This catches cases where user placed it at 1.0 or 0.65 previously.
            if (isTower && (storedScale.X > 0.6f && storedScale.X < 1.2f))
            {
                obj.Scale = new Vector3(0.42f, 0.42f, 0.42f);
                // Update the memory too so it sticks
                _lastScales[obj.ObjectName] = obj.Scale;
            }
            else
            {
                obj.Scale = storedScale;
            }
            GD.Print($"ObjectPlacer: Re-applying last used scale ({obj.Scale}) for {obj.ObjectName}");
        }
        else if (isTower)
        {
            // Default smaller size for towers as requested (first time placement)
            obj.Scale = new Vector3(0.42f, 0.42f, 0.42f);
        }
    }

    public void ConfirmPlacement()
    {
        if (_currentObject == null) return;

        // Save scale for next time we place this object type
        _lastScales[_currentObject.ObjectName] = _currentObject.Scale;

        // DEBUG: Print exact details for user feedback
        GD.Print($"[PLACEMENT] Object: {_currentObject.ObjectName} | Scale: {_currentObject.Scale} | Pos: {_currentObject.GlobalPosition}");

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

            string resourcePath = "";
            string objName = _currentObject.ObjectName;

            if (objName == "DistanceSign" || objName == "DistanceMarker")
            {
                resourcePath = "res://Scenes/Environment/DistanceMarker.tscn";
            }
            else if (objName == "CourseMap")
            {
                resourcePath = "res://Scenes/Environment/CourseMapSign.tscn";
            }
            else if (!string.IsNullOrEmpty(_currentObject.ModelPath))
            {
                // TRUST THE MODEL PATH set by MainHUDController (handles dedicated scenes like Zombie.tscn)
                resourcePath = _currentObject.ModelPath;
            }
            else if (objName == "Monster" || _currentObject is Monster)
            {
                // Fallback for generic monsters without a dedicated scene
                string species = (_currentObject as Monster)?.Species ?? "Yeti";
                resourcePath = $"res://Scenes/Entities/Monster.tscn:{species}";
            }
            else
            {
                // Fallback for objects missing ModelPath (Legacy support, but risky)
                // Warn if we are relying on this
                GD.PrintErr($"[ObjectPlacer] WARNING: Object {objName} has no ModelPath. Attempting fallback...");
                resourcePath = "res://Assets/Textures/NatureObjects/" + objName + ".gltf";
            }

            if (string.IsNullOrEmpty(resourcePath))
            {
                GD.PrintErr($"ObjectPlacer: Could not resolve resource path for {objName}!");
                return;
            }

            netManager.RpcId(1, nameof(NetworkManager.RequestSpawnObject), resourcePath, _currentObject.GlobalPosition, _currentObject.GlobalRotation, _currentObject.Scale);

            // If this was a "Move" of an existing networked object, delete the old one
            if (!string.IsNullOrEmpty(_originalObjectName))
            {
                netManager.RpcId(1, nameof(NetworkManager.RequestDeleteObject), _originalObjectName);
            }

            // Remove local ghost immediately
            _currentObject.QueueFree();
            GD.Print($"ObjectPlacer: Sent Network Spawn Request for {objName} (Move: {!string.IsNullOrEmpty(_originalObjectName)})");
        }
        else
        {
            // Local Only (Offline)
            GD.Print($"ObjectPlacer: Placed {_currentObject.ObjectName} (Local)");
        }

        _currentObject = null;
        _isNewObject = false;
        _originalObjectName = "";

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
        _originalObjectName = "";

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

        // Exclude the object and its children to prevent self-intersection
        var exclude = new Godot.Collections.Array<Rid>();
        if (_currentObject.HasMethod("get_rid")) exclude.Add(_currentObject.Call("get_rid").AsRid());

        // Note: _currentObject is a Node3D, so we also check children for colliders
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
