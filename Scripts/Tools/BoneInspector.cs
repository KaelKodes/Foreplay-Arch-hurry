using Godot;
using System;

[Tool]
public partial class BoneInspector : Node
{
    [Export] public NodePath SkeletonPath;
    [Export] public bool RunInspect = false;

    public override void _Process(double delta)
    {
        if (RunInspect)
        {
            RunInspect = false;
            Inspect();
        }
    }

    private void Inspect()
    {
        var skel = GetNodeOrNull<Skeleton3D>(SkeletonPath);
        if (skel == null)
        {
            GD.Print("[BoneInspector] Skeleton not found at " + SkeletonPath);
            return;
        }

        GD.Print($"[BoneInspector] Inspecting {skel.Name} ({skel.GetBoneCount()} bones):");
        for (int i = 0; i < Math.Min(skel.GetBoneCount(), 10); i++)
        {
            GD.Print($"  [{i}] {skel.GetBoneName(i)}");
        }
    }
}
