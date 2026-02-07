using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Manages character model swapping, mesh updates, and bow attachment for PlayerController.
/// Extracted from PlayerController to reduce file size.
/// </summary>
public partial class CharacterModelManager : Node
{
    private PlayerController _player;
    private Node3D _meleeModel;
    private Node3D _archeryModel;
    private Node3D _currentCustomModel;
    private AnimationTree _animTree;
    private AnimationPlayer _meleeAnimPlayer;
    private AnimationPlayer _archeryAnimPlayer;
    private AnimationPlayer _customAnimPlayer;
    private string _lastPlayedAnim = "";
    private Mesh _cachedBowMesh;
    private string _currentModelId = "erika";

    public string CurrentModelId => _currentModelId;

    public void Initialize(PlayerController player, Node3D meleeModel, Node3D archeryModel,
                          AnimationTree animTree, AnimationPlayer meleeAnimPlayer, AnimationPlayer archeryAnimPlayer)
    {
        _player = player;
        _meleeModel = meleeModel;
        _archeryModel = archeryModel;
        _animTree = animTree;
        _meleeAnimPlayer = meleeAnimPlayer;
        _archeryAnimPlayer = archeryAnimPlayer;

        CacheBowMesh();
    }

    /// <summary>
    /// Cycles to the next available character model.
    /// </summary>
    public void CycleCharacterModel()
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null)
        {
            GD.PrintErr("[CharacterModelManager] CharacterRegistry not found!");
            return;
        }

        var nextModel = registry.GetNextModel(_currentModelId);
        if (nextModel != null && nextModel.Id != _currentModelId)
        {
            SetCharacterModel(nextModel.Id);
            GD.Print($"[CharacterModelManager] Switched to model: {nextModel.DisplayName}");
        }
    }

    /// <summary>
    /// Sets the character model by ID.
    /// </summary>
    public void SetCharacterModel(string modelId)
    {
        var registry = CharacterRegistry.Instance;
        if (registry == null) return;

        var model = registry.GetModel(modelId);
        if (model == null)
        {
            GD.PrintErr($"[CharacterModelManager] Model not found: {modelId}");
            return;
        }

        _currentModelId = modelId;

        if (model.IsCustomSkeleton)
        {
            // Full scene swap
            SetupCustomModel(model);
        }
        else
        {
            // Shared skeleton mesh swap
            CleanupCustomModel();
            SwapModelMesh(_meleeModel, model.MeleeScenePath);
            SwapModelMesh(_archeryModel, model.ArcheryScenePath);
        }

        GD.Print($"[CharacterModelManager] Model set to: {model.DisplayName} (Custom Rig: {model.IsCustomSkeleton})");

        // Refresh visibility/mode
        SetModelMode(_player.CurrentState == PlayerState.CombatArcher);
    }

    private void SetupCustomModel(CharacterRegistry.CharacterModel model)
    {
        CleanupCustomModel();

        // 1. Hide Erika
        if (_meleeModel != null) _meleeModel.Visible = false;
        if (_archeryModel != null) _archeryModel.Visible = false;
        if (_animTree != null) _animTree.Active = false;

        // 2. Instantiate Custom Model
        var path = model.MeleeScenePath; // Use Melee for default
        if (ResourceLoader.Exists(path))
        {
            var scn = GD.Load<PackedScene>(path);
            if (scn != null)
            {
                _currentCustomModel = scn.Instantiate<Node3D>();
                _player.AddChild(_currentCustomModel);
                _currentCustomModel.Transform = _meleeModel.Transform; // Match position/rotation

                // Apply custom rig offsets
                _currentCustomModel.Position += model.PositionOffset;
                _currentCustomModel.RotationDegrees += model.RotationOffset;
                _currentCustomModel.Scale *= model.ModelScale;

                // Find AnimPlayer
                _customAnimPlayer = FindPopulatedAnimationPlayerRecursive(_currentCustomModel);
                if (_customAnimPlayer != null)
                {
                    var animations = _customAnimPlayer.GetAnimationList();
                    GD.Print($"[CharacterModelManager] Custom AnimPlayer: {_customAnimPlayer.GetPath()} with {animations.Length} anims.");

                    // 1. Load Standard Animations (Retargeted) based on Config
                    LoadRetargetedStandardAnimations(_customAnimPlayer, model);

                    // 2. Map common internal names to standard ones (Alias) for any "Internal" sources
                    // This is still useful if the user picked "Internal" but didn't specify exact names
                    AliasEmbeddedAnimations(_customAnimPlayer, model);

                    // Re-fetch names after aliasing/loading
                    animations = _customAnimPlayer.GetAnimationList();
                    GD.Print($"[CharacterModelManager] Final animation count: {animations.Length}");

                    _lastPlayedAnim = "";
                }
                else
                {
                    GD.PrintErr($"[CharacterModelManager] NO ANIMATION PLAYER found in {path}");
                }

                // Apply Mesh Configuration (Hiding/Scaling)
                ApplyMeshConfig(_currentCustomModel, model);
            }
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

        if (boneMap == null || boneMap.Count == 0) return;

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
            { "MeleeAttack3", "melee power attack" },
            { "ArcheryIdle", "archery idle normal" },
            { "ArcheryDraw", "archery draw" },
            { "ArcheryFire", "archery recoil" },
            { "Death", "death" } // Erika doesn't have a death anim in the list
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
            // We'll free the instance after the loop
        }

        foreach (var animName in standardAnims)
        {
            // Check desired source
            string source = "standard";
            if (model.AnimationSources.ContainsKey(animName)) source = model.AnimationSources[animName];

            if (source == "standard")
            {
                // We need to load and retarget
                if (!fileMap.ContainsKey(animName)) continue;
                string fileKey = fileMap[animName];

                if (!SetupErikaAnimations.AnimationFiles.ContainsKey(fileKey)) continue;
                string fbxPath = SetupErikaAnimations.AnimationFiles[fileKey];

                // Load Source
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

                // Retargeting Logic
                int trackCount = newAnim.GetTrackCount();
                for (int i = trackCount - 1; i >= 0; i--)
                {
                    string trackPath = newAnim.TrackGetPath(i).ToString();
                    string[] parts = trackPath.Split(':'); // SkeletonName:BoneName
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

                        // --- REST POSE COMPENSATION ---
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

                // Overwrite if exists
                if (lib.HasAnimation(animName)) lib.RemoveAnimation(animName);
                lib.AddAnimation(animName, newAnim);
            }
        }

        // Cleanup Erika reference
        if (erikaSkeleton != null)
        {
            erikaSkeleton.GetParent().QueueFree();
        }
    }

    private void ApplyMeshConfig(Node3D modelInstance, CharacterRegistry.CharacterModel modelData)
    {
        if (modelData.Meshes == null || modelData.Meshes.Count == 0)
        {
            // Fallback for models without config: Hide known weapon strings to be safe
            HideBuiltinWeapons(modelInstance);
            return;
        }

        foreach (var kvp in modelData.Meshes)
        {
            string meshName = kvp.Key;
            var cfg = kvp.Value;

            var meshNode = modelInstance.FindChild(meshName, true, false) as Node3D;
            if (meshNode != null)
            {
                // Apply Scale
                meshNode.Scale = new Vector3(cfg.Scale[0], cfg.Scale[1], cfg.Scale[2]);

                // Apply Visibility based on Category
                // Items/Body/Hidden are static. Weapons are dynamic.
                if (cfg.Category == CharacterConfig.MeshConfig.Categories.Hidden)
                {
                    meshNode.Visible = false;
                }
                else if (cfg.Category == CharacterConfig.MeshConfig.Categories.Body ||
                         cfg.Category == CharacterConfig.MeshConfig.Categories.Item)
                {
                    meshNode.Visible = cfg.IsVisible;
                }
                else
                {
                    // Weapons: Initially hide until Mode update
                    meshNode.Visible = false;
                }
            }
        }
    }

    private void CleanupCustomModel()
    {
        if (_currentCustomModel != null)
        {
            _currentCustomModel.QueueFree();
            _currentCustomModel = null;
            _customAnimPlayer = null;
        }

        // Restore Erika base state
        if (_meleeModel != null) _meleeModel.Visible = true;
        if (_archeryModel != null) _archeryModel.Visible = false;
        if (_animTree != null) _animTree.Active = true;
        _lastPlayedAnim = "";
    }

    private void HideBuiltinWeapons(Node3D model)
    {
        HideNodesByKeywordsRecursive(model, new string[] {
            "sword", "weapon", "blade", "bow", "arrow",
            "knight_sword", "sword_low", "weapon_r", "hand_r_weapon"
        });
    }

    private void HideNodesByKeywordsRecursive(Node node, string[] keywords)
    {
        string lowerName = node.Name.ToString().ToLower();
        foreach (var k in keywords)
        {
            if (lowerName.Contains(k))
            {
                if (node is Node3D n3d)
                {
                    n3d.Visible = false;
                    GD.Print($"[CharacterModelManager] Hiding built-in part: {node.Name}");
                }
                break;
            }
        }
        foreach (Node child in node.GetChildren())
        {
            HideNodesByKeywordsRecursive(child, keywords);
        }
    }

    /// <summary>
    /// Maps internally named animations (like "mixamo.com") to standard names like "Idle", "Walk", etc.
    /// This makes custom models work better out-of-the-box.
    /// </summary>
    private void AliasEmbeddedAnimations(AnimationPlayer ap, CharacterRegistry.CharacterModel model)
    {
        if (ap == null) return;

        // Ensure default library exists
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

            // Map common patterns
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

        // Fix track paths for skeletons that don't match mixamo naming
        RemapBoneTracks(ap);
    }

    private void RemapBoneTracks(AnimationPlayer ap)
    {
        var skeleton = _currentCustomModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(_currentCustomModel);
        if (skeleton == null) return;

        string skelName = skeleton.Name.ToString();
        var skeletonBones = new System.Collections.Generic.HashSet<string>();
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

                // Typical path is "Armature/Skeleton3D:RightHand"
                // We want to change it to match OUR skeleton's node name and the bone name.
                string[] parts = pathStr.Split(':');
                string bonePart = parts[parts.Length - 1]; // Always the last part

                bool needsFix = false;
                string newNodePart = skelName;
                string newBonePart = bonePart;

                // 2. Fix Bone Part
                if (!skeletonBones.Contains(bonePart))
                {
                    string bestMatch = "";
                    // Strip mixamo prefixes and other junk
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

                // Check node path too
                if (!pathStr.StartsWith(skelName + ":"))
                {
                    needsFix = true;
                }

                if (needsFix)
                {
                    var newPath = new NodePath(newNodePart + ":" + newBonePart);
                    anim.TrackSetPath(i, newPath);
                    // GD.Print($"[CharacterModelManager] Remapped track: {pathStr} -> {newPath} in {animName}");
                }
            }
        }
    }

    private Skeleton3D FindSkeletonRecursive(Node node)
    {
        if (node is Skeleton3D skel) return skel;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSkeletonRecursive(child);
            if (found != null) return found;
        }
        return null;
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

    /// <summary>
    /// Direct animation update for custom skeletons.
    /// Logic mirrors Erika's AnimationTree but via direct Play() calls.
    /// </summary>
    public void UpdateCustomAnimations(bool isMoving, bool sprinting, bool jumping, bool swinging, bool firing)
    {
        if (_customAnimPlayer == null) return;

        string target = "Idle";

        if (jumping) target = "Jump";
        else if (swinging)
        {
            // Simple cycle: MeleeAttack1 -> MeleeAttack2 -> MeleeAttack3
            if (_lastPlayedAnim == "MeleeAttack1") target = "MeleeAttack2";
            else if (_lastPlayedAnim == "MeleeAttack2") target = "MeleeAttack3";
            else target = "MeleeAttack1";
        }
        else if (firing) target = "BowAttack";
        else if (isMoving) target = sprinting ? "Run" : "Walk";

        if (target != _lastPlayedAnim)
        {
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
            // 3. Simple internal fallback (Run -> Walk)
            if (standardName == "Run" && model.AnimationMap.ContainsKey("Walk"))
            {
                PlayAnimation("Walk");
            }
            else if (standardName == "Jump" && _customAnimPlayer.HasAnimation("Idle"))
            {
                PlayAnimation("Idle");
            }
            else
            {
                GD.PrintErr($"[CharacterModelManager] Animation not found: {standardName} (mapped to {targetAnim}) for {_currentModelId}");
            }
        }
    }

    /// <summary>
    /// Swaps the mesh on a model node by loading the new FBX and copying mesh instances.
    /// </summary>
    private void SwapModelMesh(Node3D targetModel, string fbxPath)
    {
        if (targetModel == null) return;

        if (!ResourceLoader.Exists(fbxPath))
        {
            GD.PrintErr($"[CharacterModelManager] Model FBX not found: {fbxPath}");
            return;
        }

        var fbxScene = GD.Load<PackedScene>(fbxPath);
        if (fbxScene == null)
        {
            GD.PrintErr($"[CharacterModelManager] Could not load FBX: {fbxPath}");
            return;
        }

        var newModelInstance = fbxScene.Instantiate<Node3D>();
        var targetSkeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        var newSkeleton = newModelInstance.GetNodeOrNull<Skeleton3D>("Skeleton3D");

        if (targetSkeleton == null || newSkeleton == null)
        {
            GD.PrintErr("[CharacterModelManager] Could not find skeletons for mesh swap");
            newModelInstance.QueueFree();
            return;
        }

        // Remove old mesh instances from target skeleton (but keep BoneAttachments)
        foreach (var child in targetSkeleton.GetChildren())
        {
            if (child is MeshInstance3D oldMesh)
            {
                oldMesh.QueueFree();
            }
        }

        // Copy mesh instances from new skeleton to target skeleton
        foreach (var child in newSkeleton.GetChildren())
        {
            if (child is MeshInstance3D newMesh)
            {
                newMesh.Owner = null;
                newSkeleton.RemoveChild(newMesh);
                targetSkeleton.AddChild(newMesh);
                FixMeshCulling(newMesh);
            }
        }

        newModelInstance.QueueFree();
        GD.Print($"[CharacterModelManager] Swapped mesh from: {fbxPath}");
    }

    /// <summary>
    /// Disables backface culling on all materials of a mesh to prevent "see-through" issues.
    /// </summary>
    private void FixMeshCulling(MeshInstance3D mesh)
    {
        if (mesh == null) return;

        int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
        if (surfaceCount == 0 && mesh.Mesh != null)
        {
            surfaceCount = mesh.Mesh.GetSurfaceCount();
        }

        for (int i = 0; i < surfaceCount; i++)
        {
            var mat = mesh.GetSurfaceOverrideMaterial(i) as BaseMaterial3D;
            if (mat == null && mesh.Mesh != null)
            {
                mat = mesh.Mesh.SurfaceGetMaterial(i) as BaseMaterial3D;
            }

            if (mat != null)
            {
                var uniqueMat = (BaseMaterial3D)mat.Duplicate();
                uniqueMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                mesh.SetSurfaceOverrideMaterial(i, uniqueMat);
            }
        }
    }

    /// <summary>
    /// Caches the bow mesh from ErikaBow skeleton at startup before any mesh swaps.
    /// </summary>
    private void CacheBowMesh()
    {
        if (_archeryModel == null) return;

        var erikaSkeleton = _archeryModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (erikaSkeleton == null) return;

        foreach (var child in erikaSkeleton.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                string meshName = mesh.Name.ToString();
                if (meshName.Contains("Bow") && !meshName.Contains("Arrow"))
                {
                    _cachedBowMesh = mesh.Mesh;
                    GD.Print($"[CharacterModelManager] Cached bow mesh: {meshName}");
                    return;
                }
            }
        }

        GD.PrintErr("[CharacterModelManager] Could not find bow mesh to cache!");
    }

    /// <summary>
    /// Sets up a standalone bow for non-Erika characters by loading Bow.tscn.
    /// </summary>
    public void SetupStandaloneBow(Node3D targetModel)
    {
        if (targetModel == null) return;
        if (_currentModelId == "erika") return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (skeleton == null) return;

        if (skeleton.HasNode("StandaloneBowAttachment")) return;

        var bowScenePath = "res://Scenes/Weapons/Bow.tscn";
        if (!ResourceLoader.Exists(bowScenePath))
        {
            GD.PrintErr($"[CharacterModelManager] Bow scene not found: {bowScenePath}");
            return;
        }

        var bowScene = GD.Load<PackedScene>(bowScenePath);
        if (bowScene == null)
        {
            GD.PrintErr("[CharacterModelManager] Could not load Bow scene");
            return;
        }

        var boneAttachment = new BoneAttachment3D();
        boneAttachment.Name = "StandaloneBowAttachment";
        boneAttachment.BoneName = "mixamorig_LeftHand";
        skeleton.AddChild(boneAttachment);

        var bowInstance = bowScene.Instantiate<Node3D>();
        boneAttachment.AddChild(bowInstance);
        bowInstance.Position = new Vector3(-0.75f, -1.39f, 0.06f);
        bowInstance.RotationDegrees = Vector3.Zero;

        GD.Print($"[CharacterModelManager] Attached standalone Bow.tscn to character");
    }

    /// <summary>
    /// Removes the standalone bow if switching back to Erika.
    /// </summary>
    public void RemoveStandaloneBow(Node3D targetModel)
    {
        if (targetModel == null) return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (skeleton == null) return;

        var bowAttachment = skeleton.GetNodeOrNull("StandaloneBowAttachment");
        if (bowAttachment != null)
        {
            bowAttachment.QueueFree();
            GD.Print("[CharacterModelManager] Removed standalone bow");
        }
    }

    /// <summary>
    /// Updates model visibility based on current tool/mode.
    /// </summary>
    public void SetModelMode(bool isArcheryMode)
    {
        var registry = CharacterRegistry.Instance;
        var model = registry?.GetModel(_currentModelId);
        bool isCustom = model?.IsCustomSkeleton ?? false;

        if (isCustom)
        {
            if (_meleeModel != null) _meleeModel.Visible = false;
            if (_archeryModel != null) _archeryModel.Visible = false;
            if (_currentCustomModel != null) _currentCustomModel.Visible = true;
            if (_animTree != null) _animTree.Active = false;

            if (isArcheryMode) SetupStandaloneBow(_currentCustomModel);
            else
            {
                RemoveStandaloneBow(_currentCustomModel);

                // Only show sword if we are actually in melee combat
                if (_player.CurrentState == PlayerState.CombatMelee)
                {
                    SetupStandaloneSword(_currentCustomModel);
                }
                else
                {
                    RemoveStandaloneSword(_currentCustomModel);
                }
                // Hide original sword to avoid double-visuals
                var swordNode = _player.GetNodeOrNull("Erika/Skeleton3D/RightHandAttachment/Sword");
                if (swordNode is Node3D sNode)
                {
                    // Hide Erika's sword if we are in Walk mode OR if we have a custom rig
                    // (The custom rig logic below handles showing the standalone one)
                    sNode.Visible = false;
                }
            }

            // Apply Dynamic Visibility (Custom Mesh Weapons)
            UpdateCustomWeaponVisibility(isArcheryMode);
            return;
        }

        if (_meleeModel != null) _meleeModel.Visible = !isArcheryMode;
        if (_archeryModel != null) _archeryModel.Visible = isArcheryMode;
        if (_currentCustomModel != null) _currentCustomModel.Visible = false;

        // Restore original sword visibility
        var origSword = _player.GetNodeOrNull("Erika/Skeleton3D/RightHandAttachment/Sword");
        if (origSword is Node3D origS3d)
        {
            // Only show if we would normally have it visible
            origS3d.Visible = !isArcheryMode;
        }

        if (isArcheryMode)
        {
            if (_animTree != null && _archeryAnimPlayer != null)
            {
                _animTree.Active = true;
                _animTree.RootNode = _animTree.GetPathTo(_archeryModel);
                _animTree.SetAnimationPlayer(_archeryAnimPlayer.GetPath());
            }

            if (_currentModelId != "erika")
            {
                SetupStandaloneBow(_archeryModel);
            }
        }
        else
        {
            if (_animTree != null && _meleeAnimPlayer != null)
            {
                _animTree.Active = true;
                _animTree.RootNode = _animTree.GetPathTo(_meleeModel);
                _animTree.SetAnimationPlayer(_meleeAnimPlayer.GetPath());
            }

            RemoveStandaloneBow(_archeryModel);
        }
    }

    /// <summary>
    /// Sets up a standalone sword for non-Erika characters by loading Sword.tscn.
    /// </summary>
    public void SetupStandaloneSword(Node3D targetModel)
    {
        if (targetModel == null) return;
        if (_currentModelId == "erika") return;

        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(targetModel);
        if (skeleton == null) return;

        if (skeleton.HasNode("StandaloneSwordAttachment")) return;

        var swordScenePath = "res://Scenes/Entities/Sword.tscn";
        if (!ResourceLoader.Exists(swordScenePath)) return;

        var swordScene = GD.Load<PackedScene>(swordScenePath);
        if (swordScene == null) return;

        // Try to find a good bone for the sword
        string boneName = "mixamorig_RightHand";
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
        {
            if (skeleton.GetBoneName(i).Contains("RightHand"))
            {
                boneName = skeleton.GetBoneName(i);
                break;
            }
        }

        var boneAttachment = new BoneAttachment3D();
        boneAttachment.Name = "StandaloneSwordAttachment";
        boneAttachment.BoneName = boneName;
        skeleton.AddChild(boneAttachment);

        var swordInstance = swordScene.Instantiate<Node3D>();
        boneAttachment.AddChild(swordInstance);

        // Adjust for typical Mixamo weapon positioning
        swordInstance.Position = new Vector3(0, 0, 0);
        swordInstance.RotationDegrees = new Vector3(-90, 0, 0);

        // Fix scaling for models that are scaled significantly (like Pale Knight)
        // We want the sword to be roughly the same world-size as when it's on Erika
        var skelScale = skeleton.GlobalTransform.Basis.Scale;
        if (skelScale.X != 0)
        {
            // If model is 0.01x, basis is 0.01. We want sword at 1.0 world size.
            // sword_local * basis = 1.0  => sword_local = 1.0 / basis
            swordInstance.Scale = Vector3.One / skelScale;
        }
        else
        {
            swordInstance.Scale = Vector3.One;
        }

        if (swordInstance is SwordController sc)
        {
            var melee = _player.GetNodeOrNull<MeleeSystem>("MeleeSystem");
            if (melee != null) sc.ConnectToMeleeSystem(melee);
        }

        GD.Print($"[CharacterModelManager] Attached standalone Sword.tscn to {boneName}");
    }

    public void RemoveStandaloneSword(Node3D targetModel)
    {
        if (targetModel == null) return;
        var skeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? FindSkeletonRecursive(targetModel);
        if (skeleton == null) return;

        var attachment = skeleton.GetNodeOrNull("StandaloneSwordAttachment");
        if (attachment != null) attachment.QueueFree();
    }

    private void LogHierarchyScales(Node node, string indent)
    {
        if (node is Node3D n3d)
        {
            GD.Print($"{indent}- {node.Name}: Scale={n3d.Scale}, GlobalScale={n3d.GlobalTransform.Basis.Scale}");
        }
        foreach (Node child in node.GetChildren())
        {
            LogHierarchyScales(child, indent + "  ");
        }
    }

    private void UpdateCustomWeaponVisibility(bool isArcheryMode)
    {
        var registry = CharacterRegistry.Instance;
        var model = registry?.GetModel(_currentModelId);
        if (model == null || _currentCustomModel == null) return;

        bool hasCustomSword = HasVisibleMeshInCategory(model, CharacterConfig.MeshConfig.Categories.WeaponMain);
        bool hasCustomBow = HasVisibleMeshInCategory(model, CharacterConfig.MeshConfig.Categories.WeaponBow);

        if (isArcheryMode)
        {
            // --- BOW ---
            if (hasCustomBow)
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, true);
                RemoveStandaloneBow(_currentCustomModel);
            }
            else
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, false);
                // Ensure standalone bow is present (SetupStandalone checks for dupes)
                SetupStandaloneBow(_currentCustomModel);
            }

            // --- SWORD ---
            // Always hide sword in archery mode
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, false);
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, false); // Shield
            RemoveStandaloneSword(_currentCustomModel);
        }
        else // Melee Mode
        {
            // --- BOW ---
            SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponBow, false);
            RemoveStandaloneBow(_currentCustomModel);

            // --- SWORD ---
            bool showSword = (_player.CurrentState == PlayerState.CombatMelee);

            if (hasCustomSword)
            {
                // Toggle custom sword based on combat state
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, showSword);
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, showSword); // Assuming shield logic matches sword
                RemoveStandaloneSword(_currentCustomModel);
            }
            else
            {
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponMain, false);
                SetCategoryVisibility(model, CharacterConfig.MeshConfig.Categories.WeaponOff, false);

                if (showSword) SetupStandaloneSword(_currentCustomModel);
                else RemoveStandaloneSword(_currentCustomModel);
            }
        }
    }

    private bool HasVisibleMeshInCategory(CharacterRegistry.CharacterModel model, string category)
    {
        if (model.Meshes == null) return false;
        foreach (var mesh in model.Meshes.Values)
        {
            // Only count if it's categorized appropriately AND marked visible in config
            // (If user unchecked "Visible" in wizard, they don't want to use it)
            if (mesh.Category == category && mesh.IsVisible) return true;
        }
        return false;
    }

    private void SetCategoryVisibility(CharacterRegistry.CharacterModel model, string category, bool visible)
    {
        if (model.Meshes == null) return;
        foreach (var kvp in model.Meshes)
        {
            if (kvp.Value.Category == category && kvp.Value.IsVisible) // Respect master visibility
            {
                var node = _currentCustomModel.FindChild(kvp.Key, true, false) as Node3D;
                if (node != null) node.Visible = visible;
            }
        }
    }

    private void PrintNodeRecursive(Node node, string indent)
    {
        string extra = "";
        if (node is MeshInstance3D mi) extra = $" [Mesh: {mi.Mesh?.ResourceName}]";
        if (node is Skeleton3D skel) extra = $" [Skeleton: {skel.GetBoneCount()} bones]";
        if (node is AnimationPlayer ap) extra = $" [AnimPlayer: {ap.GetAnimationList().Length}]";

        GD.Print($"{indent}- {node.Name} ({node.GetType().Name}){extra}");

        foreach (Node child in node.GetChildren())
        {
            PrintNodeRecursive(child, indent + "  ");
        }
    }
}
