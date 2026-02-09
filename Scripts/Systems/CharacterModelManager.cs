using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager : Node
{
    private PlayerController _player;
    private Node3D _meleeModel;
    private Node3D _archeryModel;
    private Node3D _currentCustomModel;

    private static readonly Dictionary<string, string> ErikaAnimationFiles = new()
    {
        { "standing idle 01", "res://Assets/Erika/standing idle 01.fbx" },
        { "standing walk forward", "res://Assets/Erika/standing walk forward.fbx" },
        { "standing walk back", "res://Assets/Erika/standing walk back.fbx" },
        { "standing walk left", "res://Assets/Erika/standing walk left.fbx" },
        { "standing walk right", "res://Assets/Erika/standing walk right.fbx" },
        { "standing run forward", "res://Assets/Erika/standing run forward.fbx" },
        { "standing run back", "res://Assets/Erika/standing run back.fbx" },
        { "standing run left", "res://Assets/Erika/standing run left.fbx" },
        { "standing run right", "res://Assets/Erika/standing run right.fbx" },
        { "standing jump", "res://Assets/Erika/sword and shield jump (2).fbx" },
        { "melee attack", "res://Assets/Erika/sword and shield slash.fbx" },
        { "melee perfect attack", "res://Assets/Erika/sword and shield attack (3).fbx" },
        { "melee triple attack", "res://Assets/Erika/sword and shield slash (2).fbx" },
        { "archery draw", "res://Assets/ErikaBow/standing draw arrow.fbx" },
        { "archery aim idle", "res://Assets/ErikaBow/standing aim overdraw.fbx" },
        { "archery recoil", "res://Assets/ErikaBow/standing aim recoil.fbx" },
        { "archery walk forward", "res://Assets/ErikaBow/standing aim walk forward.fbx" },
        { "archery walk back", "res://Assets/ErikaBow/standing aim walk back.fbx" },
        { "archery walk left", "res://Assets/ErikaBow/standing aim walk left.fbx" },
        { "archery walk right", "res://Assets/ErikaBow/standing aim walk right.fbx" }
    };
    private AnimationTree _animTree;
    private AnimationPlayer _meleeAnimPlayer;
    private AnimationPlayer _archeryAnimPlayer;
    private AnimationPlayer _customAnimPlayer;
    private string _lastPlayedAnim = "";
    private Mesh _cachedBowMesh;
    private string _currentModelId = "Ranger";

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
}
