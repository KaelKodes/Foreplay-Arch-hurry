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

    private AnimationTree _animTree;
    private AnimationPlayer _meleeAnimPlayer;
    private AnimationPlayer _archeryAnimPlayer;
    private AnimationPlayer _customAnimPlayer;
    private string _lastPlayedAnim = "";
    private string _lastAttackAnim = "";
    private Mesh _cachedBowMesh;
    private string _currentModelId = "Ranger";

    public string CurrentModelId => _currentModelId;
    public Node3D ActiveModelRoot => _currentCustomModel ?? _meleeModel;

    public void Initialize(PlayerController player, Node3D meleeModel = null, Node3D archeryModel = null,
                          AnimationTree animTree = null, AnimationPlayer meleeAnimPlayer = null, AnimationPlayer archeryAnimPlayer = null)
    {
        _player = player;
        _meleeModel = meleeModel;
        _archeryModel = archeryModel;
        _animTree = animTree;
        _meleeAnimPlayer = meleeAnimPlayer;
        _archeryAnimPlayer = archeryAnimPlayer;

        if (_archeryModel != null)
            CacheBowMesh();
    }
}
