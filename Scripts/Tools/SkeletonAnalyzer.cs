using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Analyzes imported models to extract skeleton structure and detect bone naming conventions.
/// </summary>
public static partial class SkeletonAnalyzer
{
    /// <summary>
    /// Known bone naming patterns for auto-detection.
    /// </summary>
    public static readonly Dictionary<string, string[]> BonePatterns = new()
    {
        { "Hips", new[] { "mixamorig_Hips", "Hips", "pelvis", "pelvisA_M", "pelvisA_M_jnt", "CNTRL_Hips", "root" } },
        { "Spine", new[] { "mixamorig_Spine", "Spine", "spine", "spine_01", "spineA_0_M", "spineA_0_M_jnt" } },
        { "Spine1", new[] { "mixamorig_Spine1", "Spine1", "spine1", "spine_02", "spineA_1_M", "spineA_1_M_jnt" } },
        { "Spine2", new[] { "mixamorig_Spine2", "Spine2", "spine2", "spine_03", "spineA_chest_M", "spineA_chest_M_jnt", "chest" } },
        { "Neck", new[] { "mixamorig_Neck", "Neck", "neck", "neckA_0_M", "neckA_0_M_jnt" } },
        { "Head", new[] { "mixamorig_Head", "Head", "head", "headA_M", "headA_M_jnt", "Head_Base" } },

        { "LeftShoulder", new[] { "mixamorig_LeftShoulder", "clavicle_l", "collar_l", "armA_clavicle_L", "armA_clavicle_L_jnt", "Collar.L", "Clavicle.L", "LeftClavicle" } },
        { "LeftArm", new[] { "mixamorig_LeftArm", "LeftArm", "upperarm_l", "arm_l", "armA_shoulder_L", "armA_shoulder_L_jnt", "Arm.L", "UpperArm.L" } },
        { "LeftForeArm", new[] { "mixamorig_LeftForeArm", "LeftForeArm", "lowerarm_l", "forearm_l", "armA_elbow_L", "armA_elbow_L_jnt", "ForeArm.L", "LowerArm.L" } },
        { "LeftHand", new[] { "mixamorig_LeftHand", "LeftHand", "wrist_l", "hand_l", "armA_wrist_L", "armA_wrist_L_jnt", "Hand.L", "Wrist.L" } },
        { "LeftPalm", new[] { "mixamorig_LeftHandMiddle1", "LeftPalm", "LeftHandMiddle", "handMiddleA_0_L", "handMiddleA_0_L_jnt", "palm_l", "palm.L", "Hand1.L", "HandMiddle_L", "Middle1.L", "hand_middle_l" } },

        { "RightShoulder", new[] { "mixamorig_RightShoulder", "clavicle_r", "collar_r", "armA_clavicle_R", "armA_clavicle_R_jnt", "Collar.R", "Clavicle.R", "RightClavicle" } },
        { "RightArm", new[] { "mixamorig_RightArm", "RightArm", "upperarm_r", "arm_r", "armA_shoulder_R", "armA_shoulder_R_jnt", "Arm.R", "UpperArm.R" } },
        { "RightForeArm", new[] { "mixamorig_RightForeArm", "RightForeArm", "lowerarm_r", "forearm_r", "armA_elbow_R", "armA_elbow_R_jnt", "ForeArm.R", "LowerArm.R" } },
        { "RightHand", new[] { "mixamorig_RightHand", "RightHand", "wrist_r", "hand_r", "armA_wrist_R", "armA_wrist_R_jnt", "Hand.R", "Wrist.R" } },
        { "RightPalm", new[] { "mixamorig_RightHandMiddle1", "RightPalm", "RightHandMiddle", "handMiddleA_0_R", "handMiddleA_0_R_jnt", "palm_r", "palm.R", "Hand1.R", "HandMiddle_R", "Middle1.R", "hand_middle_r" } },

        { "LeftUpLeg", new[] { "mixamorig_LeftUpLeg", "LeftUpLeg", "thigh_l", "upperleg_l", "legA_hip_L", "legA_hip_L_jnt", "Leg1.L", "UpLeg.L", "Thigh.L" } },
        { "LeftLeg", new[] { "mixamorig_LeftLeg", "LeftLeg", "calf_l", "shin_l", "lowerleg_l", "legA_knee_L", "legA_knee_L_jnt", "Leg3.L", "Knee.L", "Shin.L" } },
        { "LeftFoot", new[] { "mixamorig_LeftFoot", "LeftFoot", "foot_l", "legA_ankle_L", "legA_ankle_L_jnt", "Heel.L", "Foot.L", "Ankle.L" } },
        { "LeftToeBase", new[] { "mixamorig_LeftToeBase", "LeftToeBase", "toe_l", "ball_l", "legA_toes_L", "legA_toes_L_jnt", "Toe.L" } },

        { "RightUpLeg", new[] { "mixamorig_RightUpLeg", "RightUpLeg", "thigh_r", "upperleg_r", "legA_hip_R", "legA_hip_R_jnt", "Leg1.R", "UpLeg.R", "Thigh.R" } },
        { "RightLeg", new[] { "mixamorig_RightLeg", "RightLeg", "calf_r", "shin_r", "lowerleg_r", "legA_knee_R", "legA_knee_R_jnt", "Leg3.R", "Knee.R", "Shin.R" } },
        { "RightFoot", new[] { "mixamorig_RightFoot", "RightFoot", "foot_r", "legA_ankle_R", "legA_ankle_R_jnt", "Heel.R", "Foot.R", "Ankle.R" } },
        { "RightToeBase", new[] { "mixamorig_RightToeBase", "RightToeBase", "toe_r", "ball_r", "legA_toes_R", "legA_toes_R_jnt", "Toe.R" } },
    };

    public class AnalysisResult
    {
        public bool HasSkeleton;
        public int BoneCount;
        public List<string> BoneNames = new();
        public Dictionary<string, string> AutoMappedBones = new();
        public List<string> UnmappedStandardBones = new();
        public List<string> UnmappedActualBones = new();
        public string SkeletonSignature = "";
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
}
