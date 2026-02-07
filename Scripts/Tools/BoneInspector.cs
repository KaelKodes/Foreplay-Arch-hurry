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
        for (int i = 0; i < skel.GetBoneCount(); i++)
        {
            string boneName = skel.GetBoneName(i);
            // Highlight hand bones
            if (boneName.Contains("Hand") || boneName.Contains("hand"))
            {
                GD.Print($"  *** [{i}] {boneName} ***");
            }
            else
            {
                GD.Print($"  [{i}] {boneName}");
            }
        }
    }
}
