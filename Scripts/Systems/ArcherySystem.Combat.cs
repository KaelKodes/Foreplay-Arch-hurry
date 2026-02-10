using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class ArcherySystem
{
    public void RegisterPlayer(PlayerController player)
    {
        GD.Print($"ArcherySystem: RegisterPlayer called for {player.Name}, IsLocal={player.IsLocal}, Authority={player.GetMultiplayerAuthority()}, MyUniqueId={Multiplayer.GetUniqueId()}");

        _currentPlayer = player;
        if (_buildManager != null) _buildManager.Player = player;

        // Shared Setup: Find ErikaBow and setup BoneAttachment for EVERYONE (needed for visual sync)
        var erikaBow = player.GetNodeOrNull<Node3D>("ErikaBow");
        if (erikaBow != null)
        {
            var skeleton = erikaBow.GetNodeOrNull<Skeleton3D>("Skeleton3D");
            if (skeleton != null)
            {
                // Check if already exists (prevent duplicate on re-register)
                _handAttachment = skeleton.GetNodeOrNull<BoneAttachment3D>("RightHandArrowAttachment");

                if (_handAttachment == null)
                {
                    _handAttachment = new BoneAttachment3D();
                    _handAttachment.Name = "RightHandArrowAttachment";
                    _handAttachment.BoneName = "mixamorig_RightHand";
                    skeleton.AddChild(_handAttachment);
                    GD.Print($"ArcherySystem: Created RightHandArrowAttachment on {player.Name}'s ErikaBow skeleton.");
                }
            }
        }

        // Link camera and input only if local
        if (player.IsLocal)
        {
            // Try direct child name first (standard), then property path
            var cam = player.GetNodeOrNull<CameraController>("Camera3D");
            if (cam == null && player.CameraPath != null && !player.CameraPath.IsEmpty)
            {
                cam = player.GetNodeOrNull<CameraController>(player.CameraPath);
            }

            if (cam != null)
            {
                _camera = cam;
                GD.Print($"ArcherySystem: Registered Local Player Camera: {_camera.Name}");
            }
            else
            {
                GD.PrintErr("ArcherySystem: Registered Local Player but could NOT find Camera!");
            }
        }
    }

    public void ExitCombatMode()
    {
        _stage = DrawStage.Idle;

        // Hide held arrow for everyone
        if (_arrow != null && !_arrow.HasBeenShot)
        {
            _arrow.Visible = false;
        }

        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
        if (_camera != null)
        {
            _camera.SetTarget(_currentPlayer, true); // Snap to player
        }
        EmitSignal(SignalName.ModeChanged, false);
        SetPrompt(false, "");
    }

    public void EnterCombatMode()
    {
        _stage = DrawStage.Idle;
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.CombatArcher;

        EmitSignal(SignalName.ModeChanged, true);

        // Ensure current arrow is visible (if we were stowed)
        if (_arrow != null && !_arrow.HasBeenShot)
        {
            _arrow.Visible = true;
        }

        // Ensure an arrow is ready as soon as we enter combat
        if (_arrow == null || _arrow.HasBeenShot)
        {
            PrepareNextShot();
        }
    }

    public void EnterBuildMode()
    {
        _stage = DrawStage.Idle;
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.BuildMode;
        EmitSignal(SignalName.ModeChanged, false);
        SetPrompt(false);
    }

    public void ExitBuildMode()
    {
        if (_currentPlayer != null) _currentPlayer.CurrentState = PlayerState.WalkMode;
        SetPrompt(false);
    }

    public void CycleShotMode()
    {
        _currentMode = (ArcheryShotMode)(((int)_currentMode + 1) % 3);
        GD.Print($"[ArcherySystem] Shot Mode changed to: {_currentMode}");
        EmitSignal(SignalName.ShotModeChanged, (int)_currentMode);
    }


    public void SetTarget(Node3D target)
    {
        // This is now purely for visual/shot sync if needed, but PlayerController is the source of truth.
        EmitSignal(SignalName.TargetChanged, target);

        if (_camera != null && _camera is CameraController camCtrl)
        {
            camCtrl.SetLockedTarget(target);
        }
    }

    public void ClearTarget()
    {
        SetTarget(null);
    }
}
