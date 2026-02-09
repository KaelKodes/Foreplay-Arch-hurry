using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class Monsters
{
    private void LoadZombieAnimations()
    {
        var zombieAnimFiles = new Dictionary<string, string>
        {
            { "Idle", "res://Assets/Monsters/Zombie/zombie idle.fbx" },
            { "Walk", "res://Assets/Monsters/Zombie/zombie walk.fbx" },
            { "Run", "res://Assets/Monsters/Zombie/zombie run.fbx" },
            { "Attack", "res://Assets/Monsters/Zombie/zombie attack.fbx" },
            { "Death", "res://Assets/Monsters/Zombie/zombie death.fbx" },
            { "Hit", "res://Assets/Monsters/Zombie/zombie scream.fbx" },
            { "Crawl", "res://Assets/Monsters/Zombie/zombie crawl.fbx" }
        };

        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null)
        {
            lib = new AnimationLibrary();
            _animPlayer.AddAnimationLibrary("", lib);
        }

        foreach (var kvp in zombieAnimFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            if (!ResourceLoader.Exists(fbxPath)) continue;

            try
            {
                var animScene = GD.Load<PackedScene>(fbxPath);
                if (animScene == null) continue;

                var tempInstance = animScene.Instantiate();
                var tempAnimPlayer = FindAnimationPlayerRecursive(tempInstance);

                if (tempAnimPlayer != null)
                {
                    var animList = tempAnimPlayer.GetAnimationList();
                    if (animList.Length > 0)
                    {
                        var srcAnim = tempAnimPlayer.GetAnimation(animList[0]);
                        if (srcAnim != null)
                        {
                            var animCopy = (Animation)srcAnim.Duplicate();
                            RemoveRootMotion(animCopy);
                            bool shouldLoop = animName == "Idle" || animName == "Walk" || animName == "Run" || animName == "Crawl";
                            animCopy.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

                            if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                            lib.AddAnimation(animName, animCopy);
                        }
                    }
                }
                tempInstance.QueueFree();
            }
            catch (Exception e) { GD.PrintErr($"[Monsters] Failed to load zombie animation {animName}: {e.Message}"); }
        }
    }

    private void LoadCrawlerAnimations()
    {
        if (_animPlayer == null) return;
        var animList = _animPlayer.GetAnimationList();
        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null) return;

        foreach (var animName in animList)
        {
            string lowerName = animName.ToLower();
            Animation anim = _animPlayer.GetAnimation(animName);
            if (anim == null) continue;

            if (lowerName.Contains("idle") && !lib.HasAnimation("Idle"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Idle", copy);
            }
            else if (lowerName.Contains("walk") && !lib.HasAnimation("Walk"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Walk", copy);
            }
            else if (lowerName.Contains("attack") && !lib.HasAnimation("Attack"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                lib.AddAnimation("Attack", copy);
            }
            else if (lowerName.Contains("death") && !lib.HasAnimation("Death"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                lib.AddAnimation("Death", copy);
            }
        }

        if (!lib.HasAnimation("Idle") && animList.Length > 0)
        {
            var firstAnim = _animPlayer.GetAnimation(animList[0]);
            if (firstAnim != null)
            {
                var copy = (Animation)firstAnim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Idle", copy);
            }
        }
    }

    private void LoadExternalAnimations(Dictionary<string, string> animFiles)
    {
        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null)
        {
            lib = new AnimationLibrary();
            _animPlayer.AddAnimationLibrary("", lib);
        }

        foreach (var kvp in animFiles)
        {
            string animName = kvp.Key;
            string fbxPath = kvp.Value;

            if (!ResourceLoader.Exists(fbxPath)) continue;

            try
            {
                var animScene = GD.Load<PackedScene>(fbxPath);
                if (animScene == null) continue;

                var tempInstance = animScene.Instantiate();
                var tempAnimPlayer = FindAnimationPlayerRecursive(tempInstance);

                if (tempAnimPlayer != null)
                {
                    var animList = tempAnimPlayer.GetAnimationList();
                    if (animList.Length > 0)
                    {
                        var srcAnim = tempAnimPlayer.GetAnimation(animList[0]);
                        if (srcAnim != null)
                        {
                            var animCopy = (Animation)srcAnim.Duplicate();
                            RemoveRootMotion(animCopy);
                            bool shouldLoop = animName == "Idle" || animName == "Walk" || animName == "Run" || animName == "Crawl";
                            animCopy.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

                            if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                            lib.AddAnimation(animName, animCopy);
                        }
                    }
                }
                tempInstance.QueueFree();
            }
            catch (Exception e) { GD.PrintErr($"[Monsters] Failed to load animation {animName}: {e.Message}"); }
        }
    }

    private void PlayAnimationRobust(string animName)
    {
        if (_animPlayer == null) return;
        var anims = _animPlayer.GetAnimationList();

        if (_animPlayer.HasAnimation(animName))
        {
            _animPlayer.Play(animName);
        }
        else if (anims.Length > 0)
        {
            string fallback = anims[0];
            var animRef = _animPlayer.GetAnimation(fallback);
            if (animRef != null) animRef.LoopMode = Animation.LoopModeEnum.Linear;
            _animPlayer.Play(fallback);
        }
    }

    public virtual void SetAnimation(string animName)
    {
        PlayAnimationRobust(animName);
    }

    private void AliasEmbeddedAnimations()
    {
        if (_animPlayer == null) return;
        var animList = _animPlayer.GetAnimationList();
        var lib = _animPlayer.GetAnimationLibrary("");
        if (lib == null) return;

        foreach (var animName in animList)
        {
            string lowerName = animName.ToLower();
            Animation anim = _animPlayer.GetAnimation(animName);
            if (anim == null) continue;

            if (lowerName.Contains("idle") && !lib.HasAnimation("Idle"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Idle", copy);
            }
            else if (lowerName.Contains("walk") && !lib.HasAnimation("Walk"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Walk", copy);
            }
            else if ((lowerName.Contains("run") || lowerName.Contains("fast")) && !lib.HasAnimation("Run"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                copy.LoopMode = Animation.LoopModeEnum.Linear;
                lib.AddAnimation("Run", copy);
            }
            else if (lowerName.Contains("attack") && !lib.HasAnimation("Attack"))
            {
                var copy = (Animation)anim.Duplicate();
                lib.AddAnimation("Attack", copy);
            }
            else if ((lowerName.Contains("death") || lowerName.Contains("die")) && !lib.HasAnimation("Death"))
            {
                var copy = (Animation)anim.Duplicate();
                RemoveRootMotion(copy);
                lib.AddAnimation("Death", copy);
            }
        }

        if (!lib.HasAnimation("Idle") && animList.Length > 0)
        {
            var copy = (Animation)_animPlayer.GetAnimation(animList[0]).Duplicate();
            RemoveRootMotion(copy);
            copy.LoopMode = Animation.LoopModeEnum.Linear;
            lib.AddAnimation("Idle", copy);
        }
    }

    private void RemoveRootMotion(Animation anim)
    {
        int trackCount = anim.GetTrackCount();
        for (int i = trackCount - 1; i >= 0; i--)
        {
            string trackPath = anim.TrackGetPath(i).ToString();
            if ((trackPath.Contains("Hips") || trackPath.Contains("Root")) &&
                anim.TrackGetType(i) == Animation.TrackType.Position3D)
            {
                anim.RemoveTrack(i);
            }
        }
    }
}
