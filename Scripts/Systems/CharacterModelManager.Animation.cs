using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager
{
    /// <summary>
    /// Direct animation update for custom skeletons.
    /// Logic mirrors Erika's AnimationTree but via direct Play() calls.
    /// </summary>
    public void UpdateCustomAnimations(bool isMoving, bool sprinting, bool jumping, bool swinging, bool firing, bool overcharged = false)
    {
        if (_customAnimPlayer == null) return;

        string target = "Idle";

        // Prioritize Attack/Action -> Movement -> Idle
        if (swinging)
        {
            if (overcharged) target = "MeleeAttack4";
            else
            {
                // Simple cycle: MeleeAttack1 -> MeleeAttack2 -> MeleeAttack3
                if (_lastPlayedAnim == "MeleeAttack1") target = "MeleeAttack2";
                else if (_lastPlayedAnim == "MeleeAttack2") target = "MeleeAttack3";
                else target = "MeleeAttack1";
            }
        }
        else if (firing) target = "BowAttack";
        else if (jumping) target = "Jump";
        else if (isMoving) target = sprinting ? "Run" : "Walk";

        // If target is found in AnimationPlayer or has a valid fallback, play it
        if (target != _lastPlayedAnim || !_customAnimPlayer.IsPlaying())
        {
            GD.Print($"[AnimationDebug] Custom Model ({_currentModelId}) state transition: {_lastPlayedAnim} -> {target}");
            PlayAnimation(target);
            _lastPlayedAnim = target;
        }
    }

    public void PlayAnimation(string standardName)
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) return;

        var model = registry.GetModel(_currentModelId);
        if (model == null) return;

        if (!model.IsCustomSkeleton) return; // Erika handled by AnimationTree

        if (_customAnimPlayer == null) return;

        // 1. Map name
        string targetAnim = standardName;
        if (model.AnimationMap.ContainsKey(standardName))
        {
            targetAnim = model.AnimationMap[standardName];
        }

        // 2. Try play
        string matchedName = "";

        // Exact or fuzzy match
        var anims = _customAnimPlayer.GetAnimationList();
        string lowerTarget = targetAnim.ToLower();
        string lowerStandard = standardName.ToLower();

        foreach (var a in anims)
        {
            string lowerA = a.ToLower();
            if (lowerA == lowerTarget || lowerA == lowerStandard ||
                lowerA.Contains(lowerTarget) || lowerTarget.Contains(lowerA) ||
                lowerA.Contains(lowerStandard) || lowerStandard.Contains(lowerA))
            {
                matchedName = a;
                break;
            }
        }

        if (!string.IsNullOrEmpty(matchedName))
        {
            _customAnimPlayer.Play(matchedName);
        }
        else
        {
            // --- DIAGNOSTIC: List all animations ---
            string allAnims = string.Join(", ", anims);
            GD.PrintErr($"[CharacterModelManager] Animation not found: {standardName} (mapped to {targetAnim}) for {_currentModelId}. Available: [{allAnims}]");
        }
    }

    private void LoadRetargetedStandardAnimations(AnimationPlayer animPlayer, CharacterRegistry.CharacterModel model)
    {
        // 0. Get Bone Map
        Dictionary<string, string> boneMap = null;
        if (model.AnimationMap.ContainsKey("__BONE_MAP__"))
        {
            try
            {
                boneMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(model.AnimationMap["__BONE_MAP__"]);
            }
            catch { GD.PrintErr("[CharacterModelManager] Failed to deserialize BoneMap"); }
        }

        // Ensure default library exists
        AnimationLibrary lib;
        if (animPlayer.HasAnimationLibrary("")) lib = animPlayer.GetAnimationLibrary("");
        else
        {
            lib = new AnimationLibrary();
            animPlayer.AddAnimationLibrary("", lib);
        }

        var skeleton = FindSkeletonRecursive(_currentCustomModel);
        if (skeleton == null) return;

        // 1. Iterate through required standard animations
        string[] standardAnims = new string[] {
            "Idle", "Walk", "Run", "Jump",
            "MeleeAttack1", "MeleeAttack2", "MeleeAttack3",
            "ArcheryIdle", "ArcheryDraw", "ArcheryFire",
            "Death"
        };

        // Map standard names to what SetupErikaAnimations uses
        var fileMap = new Dictionary<string, string> {
            { "Idle", "standing idle 01" },
            { "Walk", "standing walk forward" },
            { "Run", "standing run forward" },
            { "Jump", "standing jump" },
            { "MeleeAttack1", "melee attack" },
            { "MeleeAttack2", "melee perfect attack" },
            { "MeleeAttack3", "melee triple attack" },
            { "ArcheryIdle", "archery idle normal" },
            { "ArcheryDraw", "archery draw" },
            { "ArcheryFire", "archery recoil" },
            { "Death", "death" }
        };

        var skeletonBones = new HashSet<string>();
        for (int i = 0; i < skeleton.GetBoneCount(); i++) skeletonBones.Add(skeleton.GetBoneName(i));

        // 0. Load Erika's skeleton as a rest-pose reference
        Skeleton3D erikaSkeleton = null;
        const string erikaPath = "res://Assets/Erika/Erika Archer.fbx";
        if (ResourceLoader.Exists(erikaPath))
        {
            var erikaScn = GD.Load<PackedScene>(erikaPath);
            var erikaInst = erikaScn.Instantiate();
            erikaSkeleton = FindSkeletonRecursive(erikaInst);
        }

        foreach (var animName in standardAnims)
        {
            string source = "standard";
            if (model.AnimationSources.ContainsKey(animName)) source = model.AnimationSources[animName];

            if (source == "standard")
            {
                if (boneMap == null || boneMap.Count == 0) continue;
                if (!fileMap.ContainsKey(animName)) continue;
                string fileKey = fileMap[animName];

                if (!ErikaAnimationFiles.ContainsKey(fileKey)) continue;
                string fbxPath = ErikaAnimationFiles[fileKey];

                if (!ResourceLoader.Exists(fbxPath)) continue;
                var fbxScene = GD.Load<PackedScene>(fbxPath);
                if (fbxScene == null) continue;

                var instance = fbxScene.Instantiate();
                var srcPlayer = instance.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
                if (srcPlayer == null) { instance.QueueFree(); continue; }

                var srcList = srcPlayer.GetAnimationList();
                if (srcList.Length == 0) { instance.QueueFree(); continue; }

                var srcAnim = srcPlayer.GetAnimation(srcList[0]);
                var newAnim = srcAnim.Duplicate() as Animation;
                instance.QueueFree();

                int trackCount = newAnim.GetTrackCount();
                for (int i = trackCount - 1; i >= 0; i--)
                {
                    string trackPath = newAnim.TrackGetPath(i).ToString();
                    string[] parts = trackPath.Split(':');
                    string propertyPart = (parts.Length > 1) ? parts[1] : "";
                    string boneInTrack = !string.IsNullOrEmpty(propertyPart) ? propertyPart : parts[0];
                    int lastSlash = boneInTrack.LastIndexOf('/');
                    if (lastSlash != -1) boneInTrack = boneInTrack.Substring(lastSlash + 1);

                    string standardBone = boneInTrack.Replace("mixamorig_", "");

                    string targetBone = null;
                    if (boneMap.ContainsKey(standardBone)) targetBone = boneMap[standardBone];
                    else if (boneMap.ContainsKey(boneInTrack)) targetBone = boneMap[boneInTrack];

                    if (targetBone != null && skeletonBones.Contains(targetBone))
                    {
                        string newPath = $"{skeleton.Name}:{targetBone}";
                        newAnim.TrackSetPath(i, newPath);

                        if (erikaSkeleton != null)
                        {
                            string eBoneName = "mixamorig_" + standardBone;
                            int eBoneIdx = erikaSkeleton.FindBone(eBoneName);
                            if (eBoneIdx == -1) eBoneIdx = erikaSkeleton.FindBone(standardBone);

                            if (eBoneIdx != -1)
                            {
                                int tBoneIdx = skeleton.FindBone(targetBone);
                                Quaternion sRest = erikaSkeleton.GetBoneRest(eBoneIdx).Basis.GetRotationQuaternion();
                                Quaternion tRest = skeleton.GetBoneRest(tBoneIdx).Basis.GetRotationQuaternion();
                                Quaternion correction = tRest.Inverse() * sRest;

                                if (newAnim.TrackGetType(i) == Animation.TrackType.Rotation3D)
                                {
                                    for (int k = 0; k < newAnim.TrackGetKeyCount(i); k++)
                                    {
                                        Quaternion oldQ = (Quaternion)newAnim.TrackGetKeyValue(i, k);
                                        newAnim.TrackSetKeyValue(i, k, correction * oldQ);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        newAnim.RemoveTrack(i);
                    }
                }

                newAnim.LoopMode = Animation.LoopModeEnum.None;
                if (animName == "Idle" || animName == "Run" || animName == "Walk" || animName == "ArcheryIdle" || animName == "ArcheryDraw")
                {
                    newAnim.LoopMode = Animation.LoopModeEnum.Linear;
                }

                if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                lib.AddAnimation(animName, newAnim);
            }
            else if (source.StartsWith("res://"))
            {
                if (!ResourceLoader.Exists(source)) continue;
                var fbxScene = GD.Load<PackedScene>(source);
                if (fbxScene == null) continue;

                var instance = fbxScene.Instantiate();
                var srcPlayer = FindPopulatedAnimationPlayerRecursive(instance);

                if (srcPlayer == null)
                {
                    instance.QueueFree();
                    continue;
                }

                var srcList = srcPlayer.GetAnimationList();
                if (srcList.Length == 0)
                {
                    instance.QueueFree();
                    continue;
                }

                var srcAnim = srcPlayer.GetAnimation(srcList[0]);
                var newAnim = srcAnim.Duplicate() as Animation;
                instance.QueueFree();

                int trackCount = newAnim.GetTrackCount();
                for (int i = 0; i < trackCount; i++)
                {
                    string trackPath = newAnim.TrackGetPath(i).ToString();
                    string boneName = trackPath;
                    string propertyPart = "";

                    if (trackPath.Contains(":"))
                    {
                        var parts = trackPath.Split(':');
                        boneName = parts[0];
                        if (parts.Length > 1)
                        {
                            if (parts[0].ToLower().Contains("skeleton"))
                            {
                                boneName = parts[1];
                                if (parts.Length > 2) propertyPart = parts[2];
                            }
                            else
                            {
                                propertyPart = parts[1];
                            }
                        }
                    }

                    int lastSlash = boneName.LastIndexOf('/');
                    if (lastSlash != -1) boneName = boneName.Substring(lastSlash + 1);

                    string newPath = $"{skeleton.Name}:{boneName}";
                    if (!string.IsNullOrEmpty(propertyPart)) newPath += $":{propertyPart}";

                    newAnim.TrackSetPath(i, newPath);

                    string lowerBone = boneName.ToLower();
                    if (lowerBone.Contains("hips") || lowerBone.Contains("root"))
                    {
                        if (newAnim.TrackGetType(i) == Animation.TrackType.Position3D)
                        {
                            for (int k = 0; k < newAnim.TrackGetKeyCount(i); k++)
                            {
                                Vector3 pos = (Vector3)newAnim.TrackGetKeyValue(i, k);
                                newAnim.TrackSetKeyValue(i, k, new Vector3(0, pos.Y, 0));
                            }
                        }
                    }
                }

                newAnim.LoopMode = Animation.LoopModeEnum.None;
                if (animName == "Idle" || animName == "Run" || animName == "Walk" || animName == "ArcheryIdle" || animName == "ArcheryDraw")
                {
                    newAnim.LoopMode = Animation.LoopModeEnum.Linear;
                }

                if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                lib.AddAnimation(animName, newAnim);
            }
        }

        if (erikaSkeleton != null)
        {
            erikaSkeleton.GetParent().QueueFree();
        }
    }

    private void AliasEmbeddedAnimations(AnimationPlayer ap, CharacterRegistry.CharacterModel model)
    {
        if (ap == null) return;

        AnimationLibrary lib;
        if (ap.HasAnimationLibrary("")) lib = ap.GetAnimationLibrary("");
        else
        {
            lib = new AnimationLibrary();
            ap.AddAnimationLibrary("", lib);
        }

        var anims = ap.GetAnimationList();
        foreach (var name in anims)
        {
            string lower = name.ToLower();

            if ((lower.Contains("idle") || lower.Contains("mixamo.com")) && !lib.HasAnimation("Idle"))
                lib.AddAnimation("Idle", ap.GetAnimation(name));

            if ((lower.Contains("walk") || lower.Contains("moving")) && !lib.HasAnimation("Walk"))
                lib.AddAnimation("Walk", ap.GetAnimation(name));

            if (lower.Contains("run") && !lib.HasAnimation("Run"))
                lib.AddAnimation("Run", ap.GetAnimation(name));

            if (lower.Contains("jump") && !lib.HasAnimation("Jump"))
                lib.AddAnimation("Jump", ap.GetAnimation(name));

            if ((lower.Contains("attack") || lower.Contains("slash")) && !lib.HasAnimation("MeleeAttack1"))
                lib.AddAnimation("MeleeAttack1", ap.GetAnimation(name));

            if (lower.Contains("death") && !lib.HasAnimation("Death"))
                lib.AddAnimation("Death", ap.GetAnimation(name));
        }

        RemapBoneTracks(ap);
    }

    private void RemapBoneTracks(AnimationPlayer ap)
    {
        var skeleton = _currentCustomModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(_currentCustomModel);
        if (skeleton == null) return;

        string skelName = skeleton.Name.ToString();
        var skeletonBones = new HashSet<string>();
        for (int i = 0; i < skeleton.GetBoneCount(); i++) skeletonBones.Add(skeleton.GetBoneName(i));

        var animNames = ap.GetAnimationList();
        foreach (var animName in animNames)
        {
            var anim = ap.GetAnimation(animName);
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                var path = anim.TrackGetPath(i);
                string pathStr = path.ToString();
                if (!pathStr.Contains(":")) continue;

                string[] parts = pathStr.Split(':');
                string bonePart = parts[parts.Length - 1];

                bool needsFix = false;
                string newNodePart = skelName;
                string newBonePart = bonePart;

                if (!skeletonBones.Contains(bonePart))
                {
                    string bestMatch = "";
                    string strippedBone = bonePart.Replace("mixamorig_", "").Replace("%", "").ToLower();
                    if (strippedBone.Contains("_")) strippedBone = strippedBone.Split('_')[strippedBone.Split('_').Length - 1];

                    foreach (var sb in skeletonBones)
                    {
                        string lowerSb = sb.ToLower();
                        if (lowerSb == strippedBone || lowerSb.Contains(strippedBone) || strippedBone.Contains(lowerSb))
                        {
                            bestMatch = sb;
                            break;
                        }
                    }

                    if (bestMatch != "")
                    {
                        newBonePart = bestMatch;
                        needsFix = true;
                    }
                }

                if (!pathStr.StartsWith(skelName + ":"))
                {
                    needsFix = true;
                }

                if (needsFix)
                {
                    var newPath = new NodePath(newNodePart + ":" + newBonePart);
                    anim.TrackSetPath(i, newPath);
                }
            }
        }
    }

    private AnimationPlayer FindAnimationPlayerRecursive(Node node)
    {
        if (node is AnimationPlayer ap) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindAnimationPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private AnimationPlayer FindPopulatedAnimationPlayerRecursive(Node node)
    {
        AnimationPlayer best = null;
        int maxAnims = -1;

        void Search(Node target)
        {
            if (target is AnimationPlayer ap)
            {
                int count = ap.GetAnimationList().Length;
                if (count > maxAnims)
                {
                    maxAnims = count;
                    best = ap;
                }
            }
            foreach (Node child in target.GetChildren())
            {
                Search(child);
            }
        }

        Search(node);
        return best;
    }
}
