using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class ArcherySystem
{
    public void SetTeePosition(Vector3 pos) => TeePosition = pos;
    public void UpdateTeePosition(Vector3 pos) => TeePosition = pos;
    public void UpdatePinPosition(Vector3 pos) { /* Placeholder for future target logic */ }
    public BallLie GetCurrentLie() => new BallLie { PowerEfficiency = 1.0f, LaunchAngleBonus = 0.0f, SpinModifier = 1.0f };
    public float GetEstimatedPower() => 100.0f; // Placeholder
    public float AoAOffset => 0.0f; // Placeholder

    private Color GetPlayerColorByOwnerId(long ownerId)
    {
        PlayerController pc = GetTree().CurrentScene.FindChild(ownerId.ToString(), true, false) as PlayerController;
        if (pc != null) return GetPlayerColor(pc.PlayerIndex);
        return Colors.White;
    }

    public void CollectArrow()
    {
        ArrowCount++;
        UpdateArrowLabel();
    }

    private void UpdateArrowLabel()
    {
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }

    private void UpdateArrowPose()
    {
        if (_arrow == null || _currentPlayer == null || _arrow.HasBeenShot) return;

        Transform3D t;

        if (_handAttachment != null)
        {
            t = _handAttachment.GlobalTransform;
        }
        else
        {
            Vector3 spawnPos = _currentPlayer.GlobalPosition + (_currentPlayer.GlobalBasis * (ChestOffset + new Vector3(0, 0, 0.5f)));
            t = _currentPlayer.GlobalTransform;
            t.Origin = spawnPos;
            t.Basis = t.Basis.Rotated(Vector3.Up, Mathf.Pi);
        }

        Vector3 finalPos = _calibratedPos + DebugArrowOffsetPosition;
        t.Origin += t.Basis * finalPos;

        Vector3 finalRot = _calibratedRot + DebugArrowOffsetRotation;

        if (finalRot != Vector3.Zero)
        {
            t.Basis = t.Basis.Rotated(t.Basis.X, Mathf.DegToRad(finalRot.X));
            t.Basis = t.Basis.Rotated(t.Basis.Y, Mathf.DegToRad(finalRot.Y));
            t.Basis = t.Basis.Rotated(t.Basis.Z, Mathf.DegToRad(finalRot.Z));
        }

        _arrow.GlobalTransform = t;
    }

    public void SetPrompt(bool visible, string message = "")
    {
        EmitSignal(SignalName.PromptChanged, visible, message);
    }

    private void OnArrowSettled(float distance)
    {
        SetPrompt(true, $"Shot settled: {distance * ArcheryConstants.UNIT_RATIO:F1}y");
    }

    private Color GetPlayerColor(int index)
    {
        return TargetingHelper.GetPlayerColor(index);
    }
}
