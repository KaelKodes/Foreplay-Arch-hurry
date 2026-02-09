using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void NetSetPlayerIndex(int index)
    {
        GD.Print($"[PlayerController] NetSetPlayerIndex received: {index} (Old: {PlayerIndex})");
        SetPlayerIndex(index);
    }

    public void SetPlayerIndex(int index)
    {
        PlayerIndex = index;
        UpdatePlayerColor();
    }

    private void UpdatePlayerColor()
    {
        if (_avatarMesh != null)
        {
            Color playerColor = TargetingHelper.GetPlayerColor(PlayerIndex);
            // Apply color to mesh
        }
    }

    private void UpdateSyncProperties(double delta)
    {
        if (_camera != null)
        {
            HeadXRotation = _camera.Rotation.X;
        }
    }

    // Example of syncing model across network
    public void SetCharacterModel(string modelId)
    {
        if (SynchronizedModel != modelId)
        {
            SynchronizedModel = modelId;
            Rpc(nameof(NetSetCharacterModel), modelId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void NetSetCharacterModel(string modelId)
    {
        SynchronizedModel = modelId;
    }
}
