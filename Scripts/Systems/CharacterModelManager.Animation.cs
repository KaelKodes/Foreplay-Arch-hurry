using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager
{
    /// <summary>
    /// Direct animation update for custom skeletons.
    /// Logic mirrors Erika's AnimationTree but via direct Play() calls.
    /// lockedPower: 0=no charge, 50=basic, 100=perfect, 200=overcharged
    /// </summary>
    public void UpdateCustomAnimations(bool isMoving, bool sprinting, bool jumping, bool swinging, bool firing, float lockedPower = 0f, bool isVaulting = false)
    {
        if (_customAnimPlayer == null) return;

        string target = "Idle";

        // Define which animations should be allowed to finish before switching back to movement/idle
        string lowAnim = _lastPlayedAnim.ToLower();
        bool isActionAnim = lowAnim.StartsWith("meleeattack") ||
                           lowAnim == "bowattack" ||
                           lowAnim == "powerslash" ||
                           lowAnim == "slashcombo" ||
                           lowAnim.Contains("slot") ||
                           lowAnim.Contains("attack") ||
                           lowAnim.Contains("casting") ||
                           lowAnim.Contains("spellcast") ||
                           lowAnim == "kick" ||
                           lowAnim == "powerup" ||
                           lowAnim == "impact" ||
                           lowAnim == "death";

        // Prioritize Attack/Action -> Movement -> Idle
        if (swinging)
        {
            // Pick animation based on charge tier
            if (lockedPower >= 199f) target = "SlashCombo";       // Overcharged (200%)
            else if (lockedPower >= 99f) target = "PowerSlash";   // Perfect (100%)
            else target = "MeleeAttack1";                         // Basic (50% or quick click)

            // If already playing THIS specific action/attack, don't restart it (even if finished)
            // This prevents "flicker/restart" if the anim is shorter than the swing state.
            if (target == _lastPlayedAnim) return;
        }
        else if (firing)
        {
            target = "BowAttack";
            if (target == _lastPlayedAnim) return;
        }
        else
        {
            // We are NOT currently triggering an action.
            // If we are currently playing an action animation, LET IT FINISH
            // UNLESS we're vaulting/jumping (physics override takes priority)
            if (isActionAnim && _customAnimPlayer.IsPlaying() && !isVaulting && !jumping)
            {
                return;
            }

            if (isVaulting || jumping) target = "Jump";
            else if (isMoving) target = sprinting ? "Run" : "Walk";
        }

        // Play if changed or not currently playing
        if (target != _lastPlayedAnim || !_customAnimPlayer.IsPlaying())
        {
            PlayAnimation(target);
            // PlayAnimation now updates _lastPlayedAnim internally
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

        // 2. Try play — exact match first, then fuzzy fallback
        string matchedName = "";
        var anims = _customAnimPlayer.GetAnimationList();
        string lowerTarget = targetAnim.ToLower();
        string lowerStandard = standardName.ToLower();

        // Pass 1: Exact match (case-insensitive)
        foreach (var a in anims)
        {
            string lowerA = a.ToLower();
            if (lowerA == lowerTarget || lowerA == lowerStandard)
            {
                matchedName = a;
                break;
            }
        }

        // Pass 2: Fuzzy contains fallback
        if (string.IsNullOrEmpty(matchedName))
        {
            foreach (var a in anims)
            {
                string lowerA = a.ToLower();
                if (lowerA.Contains(lowerTarget) || lowerTarget.Contains(lowerA) ||
                    lowerA.Contains(lowerStandard) || lowerStandard.Contains(lowerA))
                {
                    matchedName = a;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(matchedName))
        {
            if (matchedName != _lastPlayedAnim)
                GD.Print($"[Anim] {_currentModelId}: '{standardName}' → '{matchedName}'");

            _customAnimPlayer.Play(matchedName);
            _lastPlayedAnim = standardName; // Use the "Standard" name for tracking logic
        }
        else
        {
            // --- DIAGNOSTIC: List all animations ---
            string allAnims = string.Join(", ", anims);
            GD.PrintErr($"[CharacterModelManager] Animation not found: {standardName} (mapped to {targetAnim}) for {_currentModelId}. Available: [{allAnims}]");
        }
    }

    /// <summary>
    /// Loads animations from individual FBX files defined in the model's AnimationSources.
    /// Each FBX is loaded, the first animation inside is extracted, bone tracks are
    /// remapped to match the hero's own skeleton, and root motion is stripped.
    /// </summary>
    private void LoadRetargetedStandardAnimations(AnimationPlayer animPlayer, CharacterRegistry.CharacterModel model)
    {
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

        string skelName = skeleton.Name.ToString();

        // Load each animation from its FBX source
        foreach (var kvp in model.AnimationSources)
        {
            string animName = kvp.Key;
            string source = kvp.Value;

            if (!source.StartsWith("res://")) continue;
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

            // Remap bone tracks to point at the hero's skeleton
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

                string newPath = $"{skelName}:{boneName}";
                if (!string.IsNullOrEmpty(propertyPart)) newPath += $":{propertyPart}";

                newAnim.TrackSetPath(i, newPath);

                // Strip root motion (keep only Y position for hips/root)
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

            // Set loop mode
            newAnim.LoopMode = Animation.LoopModeEnum.None;
            if (animName == "Idle" || animName == "Run" || animName == "Walk" || animName == "ArcheryIdle" || animName == "ArcheryDraw")
            {
                newAnim.LoopMode = Animation.LoopModeEnum.Linear;
            }

            if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
            lib.AddAnimation(animName, newAnim);
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

    /// <summary>
    /// Ensures all animation tracks point at the correct skeleton node.
    /// Only fixes the node-path prefix (e.g. "Skeleton3D:" -> actual skeleton name).
    /// Does NOT do fuzzy bone name matching — bones should already be correct
    /// since each hero uses their own animations.
    /// </summary>
    private void RemapBoneTracks(AnimationPlayer ap)
    {
        var skeleton = _currentCustomModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(_currentCustomModel);
        if (skeleton == null) return;

        string skelName = skeleton.Name.ToString();

        var animNames = ap.GetAnimationList();
        foreach (var animName in animNames)
        {
            var anim = ap.GetAnimation(animName);
            for (int i = anim.GetTrackCount() - 1; i >= 0; i--)
            {
                string pathStr = anim.TrackGetPath(i).ToString();
                if (!pathStr.Contains(":")) continue;

                string[] parts = pathStr.Split(':');

                // Only fix if the node part doesn't already match the skeleton name
                if (parts[0] != skelName)
                {
                    // Rebuild with correct skeleton name
                    parts[0] = skelName;
                    var newPath = new NodePath(string.Join(":", parts));
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
