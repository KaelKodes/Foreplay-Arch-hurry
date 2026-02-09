using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public static partial class SkeletonAnalyzer
{
    /// <summary>
    /// Analyzes a loaded model scene to extract skeleton and animation info.
    /// </summary>
    public static AnalysisResult AnalyzeModel(Node3D modelRoot)
    {
        var result = new AnalysisResult();
        var skeleton = FindSkeletonRecursive(modelRoot);

        if (skeleton == null)
        {
            result.HasSkeleton = false;
            GD.Print("[SkeletonAnalyzer] No skeleton found in model.");
            return result;
        }

        result.HasSkeleton = true;
        result.BoneCount = skeleton.GetBoneCount();

        for (int i = 0; i < skeleton.GetBoneCount(); i++)
            result.BoneNames.Add(skeleton.GetBoneName(i));

        result.SkeletonSignature = GenerateSignature(result.BoneNames);
        AutoMapBones(result);

        var animPlayer = FindAnimationPlayerRecursive(modelRoot);
        if (animPlayer != null) result.DetectedAnimations = animPlayer.GetAnimationList().ToList();

        var allMeshes = new List<MeshInstance3D>();
        FindMeshesRecursive(modelRoot, allMeshes);

        MeshInstance3D mainBody = null;
        int maxVerts = -1;

        foreach (var mesh in allMeshes)
        {
            int vCount = mesh.Mesh?.GetFaces().Length * 3 ?? 0;
            bool isBodyName = mesh.Name.ToString().Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                              mesh.Name.ToString().Contains("Skin", StringComparison.OrdinalIgnoreCase);

            if (mainBody == null || (isBodyName && !mainBody.Name.ToString().Contains("Body", StringComparison.OrdinalIgnoreCase)) || vCount > maxVerts)
            {
                if (mainBody == null || !mainBody.Name.ToString().Contains("Body", StringComparison.OrdinalIgnoreCase))
                {
                    maxVerts = vCount;
                    mainBody = mesh;
                }
            }
        }

        foreach (var mesh in allMeshes)
        {
            var analysis = new MeshAnalysis
            {
                NodeName = mesh.Name,
                IsSkinned = mesh.Skeleton != null && !mesh.Skeleton.IsEmpty,
                VertexCount = mesh.Mesh?.GetFaces().Length * 3 ?? 0
            };

            if (mesh.GetParent() is BoneAttachment3D boneAttachment)
            {
                analysis.ParentBone = boneAttachment.BoneName;
                analysis.Category = MeshCategory.Prop;
            }
            else
            {
                if (mesh == mainBody) analysis.Category = MeshCategory.Body;
                else if (analysis.IsSkinned) analysis.Category = MeshCategory.Armor;
                else analysis.Category = MeshCategory.Prop;
            }

            string lowerName = analysis.NodeName.ToLower();
            if (lowerName.Contains("sword") || lowerName.Contains("blade") || lowerName.Contains("katana") || lowerName.Contains("dagger"))
                analysis.Category = MeshCategory.WeaponMain;
            else if (lowerName.Contains("bow"))
                analysis.Category = MeshCategory.WeaponBow;
            else if (lowerName.Contains("shield"))
                analysis.Category = MeshCategory.WeaponOff;
            else if (lowerName.Contains("arrow") || lowerName.Contains("item"))
                analysis.Category = MeshCategory.Prop;

            result.DetectedMeshes.Add(analysis);
        }

        GD.Print($"[SkeletonAnalyzer] Found {result.BoneCount} bones, {result.AutoMappedBones.Count} auto-mapped, {result.DetectedAnimations.Count} animations.");
        return result;
    }

    private static Skeleton3D FindSkeletonRecursive(Node node)
    {
        if (node is Skeleton3D skel) return skel;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSkeletonRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private static AnimationPlayer FindAnimationPlayerRecursive(Node node)
    {
        if (node is AnimationPlayer ap && ap.GetAnimationList().Length > 0) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private static void FindMeshesRecursive(Node node, List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (var child in node.GetChildren()) FindMeshesRecursive(child, meshes);
    }
}
