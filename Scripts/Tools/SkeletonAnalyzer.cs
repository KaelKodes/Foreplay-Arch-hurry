using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

/// <summary>
/// Analyzes imported models to extract skeleton structure and detect bone naming conventions.
/// </summary>
public static class SkeletonAnalyzer
{
    /// <summary>
    /// Known bone naming patterns for auto-detection.
    /// Order matters - more specific patterns should come first.
    /// Patterns should NOT include numbered suffixes (e.g., _0, _1) as those are helper bones.
    /// </summary>
    public static readonly Dictionary<string, string[]> BonePatterns = new()
    {
        // Mixamo naming (mixamorig_BoneName) + Common + Guardian Rig
        
        // === CORE BODY ===
        { "Hips", new[] {
            "mixamorig_Hips", "Hips", "pelvis", "pelvisA_M", "pelvisA_M_jnt",
            "CNTRL_Hips", "root" // Note: "hip" alone is too vague
        } },
        { "Spine", new[] {
            "mixamorig_Spine", "Spine", "spine", "spine_01",
            "spineA_0_M", "spineA_0_M_jnt"
        } },
        { "Spine1", new[] {
            "mixamorig_Spine1", "Spine1", "spine1", "spine_02",
            "spineA_1_M", "spineA_1_M_jnt"
        } },
        { "Spine2", new[] {
            "mixamorig_Spine2", "Spine2", "spine2", "spine_03",
            "spineA_chest_M", "spineA_chest_M_jnt", "chest"
        } },
        { "Neck", new[] {
            "mixamorig_Neck", "Neck", "neck",
            "neckA_0_M", "neckA_0_M_jnt"
        } },
        { "Head", new[] {
            "mixamorig_Head", "Head", "head",
            "headA_M", "headA_M_jnt", "Head_Base"
        } },
        
        // === LEFT ARM ===
        // Left Clavicle/Collar (connects spine to shoulder)
        { "LeftShoulder", new[] {
            "mixamorig_LeftShoulder", "clavicle_l", "collar_l",
            "armA_clavicle_L", "armA_clavicle_L_jnt",
            "Collar.L", "Clavicle.L", "LeftClavicle"
        } },
        
        // Left Upper Arm (shoulder joint - rotates the upper arm)
        { "LeftArm", new[] {
            "mixamorig_LeftArm", "LeftArm", "upperarm_l", "arm_l",
            "armA_shoulder_L", "armA_shoulder_L_jnt",  // This rig calls it shoulder but it's the upper arm bone
            "Arm.L", "UpperArm.L"
        } }, 
        
        // Left Forearm (elbow joint)
        { "LeftForeArm", new[] {
            "mixamorig_LeftForeArm", "LeftForeArm", "lowerarm_l", "forearm_l",
            "armA_elbow_L", "armA_elbow_L_jnt",
            "ForeArm.L", "LowerArm.L"
        } }, 
        
        // Left Hand (wrist joint)
        { "LeftHand", new[] {
            "mixamorig_LeftHand", "LeftHand", "wrist_l", "hand_l",
            "armA_wrist_L", "armA_wrist_L_jnt",
            "Hand.L", "Wrist.L"
        } },
        
        // Left Palm (for grabbing - middle knuckle)
        { "LeftPalm", new[] {
            "mixamorig_LeftHandMiddle1", "LeftPalm", "LeftHandMiddle",
            "handMiddleA_0_L", "handMiddleA_0_L_jnt",
            "palm_l", "palm.L", "Hand1.L", "HandMiddle_L",
            "Middle1.L", "hand_middle_l"
        } },
        
        // === RIGHT ARM ===
        { "RightShoulder", new[] {
            "mixamorig_RightShoulder", "clavicle_r", "collar_r",
            "armA_clavicle_R", "armA_clavicle_R_jnt",
            "Collar.R", "Clavicle.R", "RightClavicle"
        } },

        { "RightArm", new[] {
            "mixamorig_RightArm", "RightArm", "upperarm_r", "arm_r",
            "armA_shoulder_R", "armA_shoulder_R_jnt",
            "Arm.R", "UpperArm.R"
        } },

        { "RightForeArm", new[] {
            "mixamorig_RightForeArm", "RightForeArm", "lowerarm_r", "forearm_r",
            "armA_elbow_R", "armA_elbow_R_jnt",
            "ForeArm.R", "LowerArm.R"
        } },

        { "RightHand", new[] {
            "mixamorig_RightHand", "RightHand", "wrist_r", "hand_r",
            "armA_wrist_R", "armA_wrist_R_jnt",
            "Hand.R", "Wrist.R"
        } },

        { "RightPalm", new[] {
            "mixamorig_RightHandMiddle1", "RightPalm", "RightHandMiddle",
            "handMiddleA_0_R", "handMiddleA_0_R_jnt",
            "palm_r", "palm.R", "Hand1.R", "HandMiddle_R",
            "Middle1.R", "hand_middle_r"
        } },
        
        // === LEFT LEG ===
        // Left Thigh (hip-to-thigh joint) - DO NOT match numbered bones like _hip_0, _hip_1
        { "LeftUpLeg", new[] {
            "mixamorig_LeftUpLeg", "LeftUpLeg", "thigh_l", "upperleg_l",
            "legA_hip_L", "legA_hip_L_jnt",  // Main hip joint only, not _hip_0 or _hip_1
            "Leg1.L", "UpLeg.L", "Thigh.L"
        } },
        
        // Left Shin (knee joint)
        { "LeftLeg", new[] {
            "mixamorig_LeftLeg", "LeftLeg", "calf_l", "shin_l", "lowerleg_l",
            "legA_knee_L", "legA_knee_L_jnt",  // Main knee joint only
            "Leg3.L", "Knee.L", "Shin.L"
        } },
        
        // Left Foot (ankle joint)
        { "LeftFoot", new[] {
            "mixamorig_LeftFoot", "LeftFoot", "foot_l",
            "legA_ankle_L", "legA_ankle_L_jnt",
            "Heel.L", "Foot.L", "Ankle.L"
        } },
        
        // Left Toe
        { "LeftToeBase", new[] {
            "mixamorig_LeftToeBase", "LeftToeBase", "toe_l", "ball_l",
            "legA_toes_L", "legA_toes_L_jnt",
            "Toe.L"
        } },
        
        // === RIGHT LEG ===
        { "RightUpLeg", new[] {
            "mixamorig_RightUpLeg", "RightUpLeg", "thigh_r", "upperleg_r",
            "legA_hip_R", "legA_hip_R_jnt",
            "Leg1.R", "UpLeg.R", "Thigh.R"
        } },

        { "RightLeg", new[] {
            "mixamorig_RightLeg", "RightLeg", "calf_r", "shin_r", "lowerleg_r",
            "legA_knee_R", "legA_knee_R_jnt",
            "Leg3.R", "Knee.R", "Shin.R"
        } },

        { "RightFoot", new[] {
            "mixamorig_RightFoot", "RightFoot", "foot_r",
            "legA_ankle_R", "legA_ankle_R_jnt",
            "Heel.R", "Foot.R", "Ankle.R"
        } },

        { "RightToeBase", new[] {
            "mixamorig_RightToeBase", "RightToeBase", "toe_r", "ball_r",
            "legA_toes_R", "legA_toes_R_jnt",
            "Toe.R"
        } },
    };

    /// <summary>
    /// Result of analyzing a model's skeleton.
    /// </summary>
    public class AnalysisResult
    {
        public bool HasSkeleton;
        public int BoneCount;
        public List<string> BoneNames = new();
        public Dictionary<string, string> AutoMappedBones = new();  // Standard -> Actual
        public List<string> UnmappedStandardBones = new();
        public List<string> UnmappedActualBones = new();
        public string SkeletonSignature = "";  // For compatibility matching
        public List<string> DetectedAnimations = new();
        public List<MeshAnalysis> DetectedMeshes = new();
    }

    public enum MeshCategory { Body, Armor, Prop, WeaponMain, WeaponOff, WeaponBow, Unknown }

    public class MeshAnalysis
    {
        public string NodeName;
        public string ParentBone;
        public bool IsSkinned;
        public int VertexCount;
        public MeshCategory Category;
    }

    /// <summary>
    /// Analyzes a loaded model scene to extract skeleton and animation info.
    /// </summary>
    public static AnalysisResult AnalyzeModel(Node3D modelRoot)
    {
        var result = new AnalysisResult();

        // Find skeleton
        var skeleton = FindSkeletonRecursive(modelRoot);
        if (skeleton == null)
        {
            result.HasSkeleton = false;
            GD.Print("[SkeletonAnalyzer] No skeleton found in model.");
            return result;
        }

        result.HasSkeleton = true;
        result.BoneCount = skeleton.GetBoneCount();

        // Extract bone names
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
        {
            result.BoneNames.Add(skeleton.GetBoneName(i));
        }

        // Generate signature (sorted bone count + key bone presence)
        result.SkeletonSignature = GenerateSignature(result.BoneNames);

        // Auto-map bones
        AutoMapBones(result);

        // Find animations
        var animPlayer = FindAnimationPlayerRecursive(modelRoot);
        if (animPlayer != null)
        {
            result.DetectedAnimations = animPlayer.GetAnimationList().ToList();
        }

        // Analyze Meshes
        var allMeshes = new List<MeshInstance3D>();
        FindMeshesRecursive(modelRoot, allMeshes);

        // Identify Body (highest vertex count usually, or specific naming)
        MeshInstance3D mainBody = null;
        int maxVerts = -1;

        foreach (var mesh in allMeshes)
        {
            // Simple heuristic for main body
            int vCount = mesh.Mesh?.GetFaces().Length * 3 ?? 0; // Approximate
            bool isBodyName = mesh.Name.ToString().Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                              mesh.Name.ToString().Contains("Skin", StringComparison.OrdinalIgnoreCase);

            if (mainBody == null || (isBodyName && !mainBody.Name.ToString().Contains("Body", StringComparison.OrdinalIgnoreCase)) || vCount > maxVerts)
            {
                // Prefer explicit name, otherwise size
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

            // Determine Parent Bone if possible
            if (mesh.GetParent() is BoneAttachment3D boneAttachment)
            {
                analysis.ParentBone = boneAttachment.BoneName;
                analysis.Category = MeshCategory.Prop; // Attached to bone = usually prop
            }
            else
            {
                // Categorize
                if (mesh == mainBody)
                {
                    analysis.Category = MeshCategory.Body;
                }
                else if (analysis.IsSkinned)
                {
                    // Skinned but not main body = Armor/Clothing usually
                    analysis.Category = MeshCategory.Armor;
                }
                else
                {
                    // Unskinned, not attached to bone? Might be static prop or error
                    // Assume Prop for now
                    analysis.Category = MeshCategory.Prop;
                }
            }

            // Name overrides
            string lowerName = analysis.NodeName.ToLower();
            if (lowerName.Contains("sword") || lowerName.Contains("blade") || lowerName.Contains("katana") || lowerName.Contains("dagger"))
            {
                analysis.Category = MeshCategory.WeaponMain;
            }
            else if (lowerName.Contains("bow"))
            {
                analysis.Category = MeshCategory.WeaponBow;
            }
            else if (lowerName.Contains("shield"))
            {
                analysis.Category = MeshCategory.WeaponOff;
            }
            else if (lowerName.Contains("arrow") || lowerName.Contains("item")) // Arrows are props unless we want to shoot them
            {
                analysis.Category = MeshCategory.Prop;
            }

            result.DetectedMeshes.Add(analysis);
        }

        GD.Print($"[SkeletonAnalyzer] Found {result.BoneCount} bones, {result.AutoMappedBones.Count} auto-mapped, {result.DetectedAnimations.Count} animations.");

        return result;
    }

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
            float bestScore = float.MaxValue; // Lower is better

            if (BonePatterns.TryGetValue(standardBone, out var patterns))
            {
                foreach (var boneName in result.BoneNames)
                {
                    if (usedActualBones.Contains(boneName)) continue;

                    string lowerBone = boneName.ToLower();

                    // 1. Check against patterns
                    int patternIndex = -1;
                    for (int i = 0; i < patterns.Length; i++)
                    {
                        // Strict partial check: must be a word boundary match
                        if (IsStrictPartialMatch(boneName, patterns[i]))
                        {
                            patternIndex = i;
                            break;
                        }
                    }

                    if (patternIndex != -1)
                    {
                        // --- SCORING LOGIC ---
                        float score = patternIndex * 10.0f; // Base score by priority in list

                        // Penality: Side Mismatch
                        // If Standard Bone is Center (Hips, Spine, etc), heavily penalize if Actual Bone has side indicators
                        bool isStandardSide = standardBone.Contains("Left") || standardBone.Contains("Right");
                        bool isActualSide = sideTerms.Any(t => lowerBone.Contains(t) || lowerBone.EndsWith(t.TrimEnd('_', '.')));

                        if (!isStandardSide && isActualSide)
                        {
                            score += 1000.0f; // Soft disqualify (prevent Hips -> Leg_L)
                        }

                        // Penalty: Wrong Side
                        if (standardBone.Contains("Left") && (lowerBone.Contains("right") || lowerBone.Contains("_r"))) score += 1000f;
                        if (standardBone.Contains("Right") && (lowerBone.Contains("left") || lowerBone.Contains("_l"))) score += 1000f;

                        // Penalty: Helper Bones (e.g. Spine_01 vs Spine_01_End, or Twist)
                        // Prefer "clean" names
                        if (lowerBone.Contains("twist") || lowerBone.Contains("adjust") || lowerBone.Contains("offset")) score += 50.0f;

                        // Penalty: Numbered suffixes when unneeded
                        // e.g. Prefer "Spine" over "Spine_01" if both match pattern "Spine"
                        // But if pattern is "Spine_01", then "Spine_01" is good.
                        // We rely on pattern order for this mainly.

                        // Tie-breaker: Name Length (Prefer shorter)
                        // e.g. "Hips" (4) matches "Hips", vs "Hips_Ctrl" (9) -> Prefer Hips
                        score += boneName.Length * 0.1f;

                        if (score < bestScore)
                        {
                            bestMatch = boneName;
                            bestScore = score;
                        }
                    }
                }
            }

            if (bestMatch != null && bestScore < 500.0f) // Threshold to reject side-mismatches
            {
                result.AutoMappedBones[standardBone] = bestMatch;
                usedActualBones.Add(bestMatch);
            }
            else
            {
                result.UnmappedStandardBones.Add(standardBone);
            }
        }

        // Find unmapped actual bones
        foreach (var bone in result.BoneNames)
        {
            if (!usedActualBones.Contains(bone))
            {
                result.UnmappedActualBones.Add(bone);
            }
        }
    }

    /// <summary>
    /// Checks if pattern matches bone at a word boundary.
    /// </summary>
    private static bool IsStrictPartialMatch(string boneName, string pattern)
    {
        string lowerBone = boneName.ToLower();
        string lowerPattern = pattern.ToLower();

        int idx = lowerBone.IndexOf(lowerPattern);
        if (idx < 0) return false;

        // Check start boundary (if not start of string, prev char must be delimiter)
        if (idx > 0)
        {
            char prev = lowerBone[idx - 1];
            if (char.IsLetterOrDigit(prev)) return false; // e.g. "ForeArm" should not match "Arm" pattern? 
                                                          // Actually pattern order handles "ForeArm" vs "Arm". 
                                                          // But "UpLeg" should not match "Leg".
        }

        int endIdx = idx + lowerPattern.Length;
        if (endIdx >= lowerBone.Length) return true;

        // Check end boundary
        char nextChar = lowerBone[endIdx];
        // Valid delimiters: _ . - or followed by "jnt" explicitly
        if (nextChar == '_' || nextChar == '.' || nextChar == '-') return true;

        // Special case: "jnt" suffix without delimiter (e.g. "Hipjnt" - rare but possible)
        if (lowerBone.Substring(endIdx).StartsWith("jnt")) return true;

        // If matched "Hip" in "Hips", 's' is next char -> Valid singular match?
        // No, we want exact word tokens usually.

        return false;
    }

    /// <summary>
    /// Generates a signature for skeleton compatibility matching.
    /// </summary>
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
        if (node is MeshInstance3D mesh)
        {
            meshes.Add(mesh);
        }

        foreach (var child in node.GetChildren())
        {
            FindMeshesRecursive(child, meshes);
        }
    }
}
