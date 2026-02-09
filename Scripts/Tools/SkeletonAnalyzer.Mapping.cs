using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public static partial class SkeletonAnalyzer
{
    /// <summary>
    /// Attempts to auto-map standard bones to actual bone names using a weighted scoring system.
    /// </summary>
    public static void AutoMapBones(AnalysisResult result)
    {
        var usedActualBones = new HashSet<string>();
        var sideTerms = new[] { "_l_", "_r_", "_l", "_r", ".l", ".r", "left", "right" };

        foreach (var standardBone in CharacterConfig.StandardBones)
        {
            string bestMatch = null;
            float bestScore = float.MaxValue;

            if (BonePatterns.TryGetValue(standardBone, out var patterns))
            {
                foreach (var boneName in result.BoneNames)
                {
                    if (usedActualBones.Contains(boneName)) continue;

                    string lowerBone = boneName.ToLower();

                    int patternIndex = -1;
                    for (int i = 0; i < patterns.Length; i++)
                    {
                        if (IsStrictPartialMatch(boneName, patterns[i]))
                        {
                            patternIndex = i;
                            break;
                        }
                    }

                    if (patternIndex != -1)
                    {
                        float score = patternIndex * 10.0f;
                        bool isStandardSide = standardBone.Contains("Left") || standardBone.Contains("Right");
                        bool isActualSide = sideTerms.Any(t => lowerBone.Contains(t) || lowerBone.EndsWith(t.TrimEnd('_', '.')));

                        if (!isStandardSide && isActualSide) score += 1000.0f;
                        if (standardBone.Contains("Left") && (lowerBone.Contains("right") || lowerBone.Contains("_r"))) score += 1000f;
                        if (standardBone.Contains("Right") && (lowerBone.Contains("left") || lowerBone.Contains("_l"))) score += 1000f;
                        if (lowerBone.Contains("twist") || lowerBone.Contains("adjust") || lowerBone.Contains("offset")) score += 50.0f;
                        score += boneName.Length * 0.1f;

                        if (score < bestScore)
                        {
                            bestMatch = boneName;
                            bestScore = score;
                        }
                    }
                }
            }

            if (bestMatch != null && bestScore < 500.0f)
            {
                result.AutoMappedBones[standardBone] = bestMatch;
                usedActualBones.Add(bestMatch);
            }
            else
            {
                result.UnmappedStandardBones.Add(standardBone);
            }
        }

        foreach (var bone in result.BoneNames)
        {
            if (!usedActualBones.Contains(bone)) result.UnmappedActualBones.Add(bone);
        }
    }

    private static bool IsStrictPartialMatch(string boneName, string pattern)
    {
        string lowerBone = boneName.ToLower();
        string lowerPattern = pattern.ToLower();

        int idx = lowerBone.IndexOf(lowerPattern);
        if (idx < 0) return false;

        if (idx > 0 && char.IsLetterOrDigit(lowerBone[idx - 1])) return false;

        int endIdx = idx + lowerPattern.Length;
        if (endIdx >= lowerBone.Length) return true;

        char nextChar = lowerBone[endIdx];
        if (nextChar == '_' || nextChar == '.' || nextChar == '-') return true;
        if (lowerBone.Substring(endIdx).StartsWith("jnt")) return true;

        return false;
    }

    private static string GenerateSignature(List<string> boneNames)
    {
        var keyBones = new[] { "Hips", "Spine", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot" };
        int keyCount = 0;
        foreach (var key in keyBones)
        {
            if (boneNames.Any(b => b.ToLower().Contains(key.ToLower()))) keyCount++;
        }
        return $"B{boneNames.Count}_K{keyCount}";
    }
}
