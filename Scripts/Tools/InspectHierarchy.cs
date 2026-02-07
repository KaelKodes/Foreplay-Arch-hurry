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

    [Export] public string ScenePath = "res://Assets/CharacterMeshes/pale_knight_animated.glb";
    [Export] public bool TriggerInspect { get => false; set { if (value) Inspect(); } }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) GD.Print("[Hierarchy] Ready. Toggle RunInspect.");
        Inspect();
    }

    private System.Collections.Generic.List<string> _log = new();

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

        _log.Clear();
        PrintRecursive(instance, "");

        System.IO.File.WriteAllLines("hierarchy_dump.txt", _log);
        GD.Print("Hierarchy dumped to hierarchy_dump.txt");

        instance.QueueFree();
    }

    private void PrintRecursive(Node node, string indent)
    {
        string extra = "";
        if (node is AnimationPlayer ap)
        {
            var animList = ap.GetAnimationList();
            extra = $" [Animations: {string.Join(", ", animList)}]";
        }
        else if (node is MeshInstance3D mi)
        {
            extra = $" [Mesh: {mi.Mesh?.ResourceName ?? "unnamed"}]";
        }
        else if (node is Skeleton3D skel)
        {
            extra = $" [Bones: {skel.GetBoneCount()}]";
            string line = $"{indent}- {node.Name} ({node.GetType().Name}){extra}";
            GD.Print(line);
            _log.Add(line);
            for (int i = 0; i < skel.GetBoneCount(); i++)
            {
                string boneLine = $"{indent}  - Bone {i}: {skel.GetBoneName(i)}";
                GD.Print(boneLine);
                _log.Add(boneLine);
            }
            return;
        }

        string nLine = $"{indent}- {node.Name} ({node.GetType().Name}){extra}";
        GD.Print(nLine);
        _log.Add(nLine);
        foreach (Node child in node.GetChildren())
        {
            PrintRecursive(child, indent + "  ");
        }
    }
}
