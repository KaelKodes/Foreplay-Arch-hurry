using Godot;
using System;

[Tool]
public partial class InspectHierarchy : Node
{
    private bool _runInspect;
    [Export]
    public bool RunInspect
    {
        get => _runInspect;
        set
        {
            _runInspect = value;
            if (_runInspect && IsInsideTree())
            {
                _runInspect = false;
                Inspect();
            }
        }
    }

    [Export] public string ScenePath = "res://Assets/Textures/Monsters/scene.gltf";

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) GD.Print("[Hierarchy] Ready. Toggle RunInspect.");
    }

    private void Inspect()
    {
        var scn = GD.Load<PackedScene>(ScenePath);
        if (scn == null)
        {
            GD.PrintErr("Failed to load scene.");
            return;
        }
        var instance = scn.Instantiate();
        AddChild(instance);

        PrintRecursive(instance, "");

        instance.QueueFree();
    }

    private void PrintRecursive(Node node, string indent)
    {
        GD.Print($"{indent}- {node.Name} ({node.GetType().Name})");
        foreach (Node child in node.GetChildren())
        {
            PrintRecursive(child, indent + "  ");
        }
    }
}
