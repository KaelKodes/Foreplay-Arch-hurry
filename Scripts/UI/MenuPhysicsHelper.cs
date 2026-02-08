using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class MenuPhysicsHelper : Node
{
    [Export] public PackedScene BallScene;
    [Export] public Node3D BallsContainer;
    [Export] public Node3D CollidersContainer;
    [Export] public Camera3D StageCamera;

    private float _pixelsPerMeter = 1.0f;
    private Vector2 _screenSize = Vector2.Zero;
    private double _spawnTimer = 0.0;
    private double _spawnInterval = 1.5; // Seconds between balls
    private Random _random = new Random();

    private int _maxBalls = 50;
    private Queue<RigidBody3D> _activeBalls = new Queue<RigidBody3D>();

    public override void _Ready()
    {
        UpdateCameraMapping();
        GetViewport().Connect("size_changed", new Callable(this, MethodName.OnSizeChanged));

        // Delay to allow UI layout to complete
        var timer = GetTree().CreateTimer(0.5);
        timer.Connect("timeout", new Callable(this, MethodName.InitialRefresh));
    }

    private void InitialRefresh()
    {
        UpdateCameraMapping();
        RefreshColliders();
        CreateFloor();
    }

    private void CreateFloor()
    {
        if (CollidersContainer == null) return;

        float aspect = _screenSize.X / _screenSize.Y;
        float viewWidth = StageCamera.Size * aspect;

        StaticBody3D floor = new StaticBody3D();
        CollisionShape3D shape = new CollisionShape3D();
        BoxShape3D box = new BoxShape3D();
        box.Size = new Vector3(viewWidth * 2.0f, 1.0f, 10.0f);
        shape.Shape = box;

        floor.CollisionLayer = 2; // Match GolfBall mask
        floor.AddChild(shape);
        CollidersContainer.AddChild(floor);

        // Position at bottom of screen
        floor.GlobalPosition = new Vector3(0, -StageCamera.Size / 2.0f - 0.5f, 0);
    }

    private void OnSizeChanged()
    {
        UpdateCameraMapping();
        RefreshColliders(); // Re-sync colliders on window resize

        // Re-create floor
        CreateFloor();
    }

    private void UpdateCameraMapping()
    {
        _screenSize = GetViewport().GetVisibleRect().Size;
        if (StageCamera != null && StageCamera.Projection == Camera3D.ProjectionType.Orthogonal)
        {
            _pixelsPerMeter = _screenSize.Y / StageCamera.Size;
        }
    }

    public override void _Process(double delta)
    {
        _spawnTimer += delta;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0;
            SpawnBall();
        }

        // Cleanup out-of-bounds balls (just in case)
        if (BallsContainer != null)
        {
            foreach (Node child in BallsContainer.GetChildren())
            {
                if (child is RigidBody3D ball)
                {
                    if (ball.GlobalPosition.Y < -StageCamera.Size - 2.0f)
                    {
                        RemoveBall(ball);
                    }
                }
            }
        }
    }

    private void RemoveBall(RigidBody3D ball)
    {
        // Find and remove from queue
        // This is slightly inefficient but safe for small queues
        var tempQueue = new Queue<RigidBody3D>();
        while (_activeBalls.Count > 0)
        {
            var b = _activeBalls.Dequeue();
            if (b != ball) tempQueue.Enqueue(b);
        }
        _activeBalls = tempQueue;

        if (IsInstanceValid(ball)) ball.QueueFree();
    }

    public void RefreshColliders()
    {
        if (CollidersContainer == null) return;

        // Clear existing
        foreach (Node child in CollidersContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Find all interactive UI elements in the parent MainMenu
        Node parent = GetParent();
        if (parent == null) return;

        List<Control> targets = new List<Control>();
        FindControls(parent, targets);

        foreach (var ctrl in targets)
        {
            CreateColliderForUI(ctrl);
        }
    }

    private void FindControls(Node node, List<Control> list)
    {
        if (node is Button btn) list.Add(btn);
        // Skip title labels - they're not interactive and block falling arrows
        // else if (node is Label lbl && lbl.Name.ToString().Contains("Title")) list.Add(lbl);

        foreach (Node child in node.GetChildren())
        {
            FindControls(child, list);
        }
    }

    private void CreateColliderForUI(Control ctrl)
    {
        if (!ctrl.IsVisibleInTree()) return;

        Rect2 rect = ctrl.GetGlobalRect();
        Vector2 center = rect.GetCenter();
        Vector2 size = rect.Size;

        // Map to 3D
        Vector3 pos3D = ScreenToWorld(center);
        Vector3 size3D = new Vector3(size.X / _pixelsPerMeter, size.Y / _pixelsPerMeter, 2.0f); // Thick depth

        StaticBody3D body = new StaticBody3D();
        CollisionShape3D shape = new CollisionShape3D();
        BoxShape3D box = new BoxShape3D();
        box.Size = size3D;
        shape.Shape = box;

        body.CollisionLayer = 2; // Match GolfBall mask
        body.AddChild(shape);
        CollidersContainer.AddChild(body);
        body.GlobalPosition = pos3D;
    }

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        float x = (screenPos.X - _screenSize.X / 2.0f) / _pixelsPerMeter;
        float y = (_screenSize.Y / 2.0f - screenPos.Y) / _pixelsPerMeter;
        return new Vector3(x, y, 0);
    }

    private void SpawnBall()
    {
        if (BallScene == null || BallsContainer == null) return;

        RigidBody3D ball = BallScene.Instantiate<RigidBody3D>();
        // Add before setting transform to avoid initialization issues
        BallsContainer.AddChild(ball);
        _activeBalls.Enqueue(ball);

        // Cap balls
        if (_activeBalls.Count > _maxBalls)
        {
            var oldest = _activeBalls.Dequeue();
            if (IsInstanceValid(oldest)) oldest.QueueFree();
        }

        float aspect = _screenSize.X / _screenSize.Y;
        float viewWidth = StageCamera.Size * aspect;

        // Random X position at top of screen
        float startX = (float)(_random.NextDouble() * viewWidth - (viewWidth / 2.0f));

        // Spawn arrows just above the screen top
        // Move Z forward to 2.0f to ensure they participate in front of the background
        ball.GlobalPosition = new Vector3(startX, StageCamera.Size / 2.0f + 0.5f, 2.0f);

        // Ensure physics are active
        ball.Sleeping = false;
        ball.Freeze = false;

        // Debug log to confirm spawn
        // GD.Print($"[MenuPhysics] Spawned arrow at {ball.GlobalPosition}");

        // Set arrow to vertical orientation (point down, fletching up)
        // Arrow model faces forward by default, so rotate -90 degrees around X to point down
        ball.Rotation = new Vector3(Mathf.DegToRad(-90), 0, 0);

        // Nudge towards center slightly
        float nudge = -startX * 0.5f;
        ball.LinearVelocity = new Vector3((float)(_random.NextDouble() * 1.5 - 0.75) + nudge, -1.0f, 0);

        // Add a small spin for visual interest
        ball.AngularVelocity = new Vector3(0, 0, (float)(_random.NextDouble() * 2.0 - 1.0));

        // Lock to 2D plane (position only, allow rotation around Z)
        // Z is handling depth, we want them to stay at Z=2.0f
        ball.AxisLockLinearZ = true;
        // Remove angular locks so arrows can rotate and tumble as they fall
        ball.AxisLockAngularX = false;
        ball.AxisLockAngularY = false;
    }
}
