using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class ArcherySystem
{
    /// <summary>
    /// Custom Spawn Function for MultiplayerSpawner. Handles instantiating the arrow
    /// and applying the initial color received in the spawn data.
    /// </summary>
    private Node SpawnArrowLocally(Godot.Collections.Dictionary data)
    {
        if (ArrowScene == null) ArrowScene = GD.Load<PackedScene>("res://Scenes/Entities/Arrow.tscn");
        var arrow = ArrowScene.Instantiate<ArrowController>();

        // Apply name from spawn data to prevent parent->has_node(name) collisions
        if (data != null && data.ContainsKey("name"))
        {
            arrow.Name = (string)data["name"];
        }

        // Apply color from spawn data
        if (data != null && data.ContainsKey("color_r"))
        {
            float r = (float)data["color_r"];
            float g = (float)data["color_g"];
            float b = (float)data["color_b"];
            arrow.SetColor(new Color(r, g, b));
            GD.Print($"ArcherySystem: SpawnArrowLocally applied color ({r},{g},{b}) to {arrow.Name}");
        }

        // Apply team from spawn data
        if (data != null && data.ContainsKey("team"))
        {
            arrow.Team = (MobaTeam)(int)data["team"];
        }

        return arrow;
    }

    private void OnArrowSpawned(Node node)
    {
        if (node is ArrowController arrow)
        {
            string currentPlayerName = _currentPlayer?.Name ?? "NULL";
            GD.Print($"ArcherySystem: Arrow Spawned/Replicated: {arrow.Name}, MyCurrentPlayer: {currentPlayerName}");

            // Parse Name to see if it belongs to us: Arrow_{PlayerID}_{Ticket}
            string[] parts = arrow.Name.ToString().Split('_');
            if (parts.Length >= 2 && long.TryParse(parts[1], out long ownerId))
            {
                // Match by Player Index or Name for robust ownership check
                bool isMine = false;
                if (_currentPlayer != null)
                {
                    if (ownerId.ToString() == _currentPlayer.Name) isMine = true;
                }

                if (isMine)
                {
                    GD.Print($"ArcherySystem: It's MY arrow! Taking control. (Owner: {ownerId}, MyPlayer: {_currentPlayer.Name})");
                    _arrow = arrow;
                }
                else
                {
                    // Even if it's not "mine" (local), if it belongs to the player this ArcherySystem represents, 
                    // we should track it so we can drive its visibility/pose for remote players.
                    if (ownerId.ToString() == (_currentPlayer?.Name ?? ""))
                    {
                        GD.Print($"ArcherySystem: Tracking arrow for remote player {ownerId}.");
                        _arrow = arrow;
                    }
                    else
                    {
                        GD.Print($"ArcherySystem: It's Player {ownerId}'s arrow. (Not for me: {currentPlayerName})");
                    }
                }

                // SetupArrow will handle connecting signals and initial setup
                PlayerController pc = GetTree().CurrentScene.FindChild(ownerId.ToString(), true, false) as PlayerController;
                SetupArrow(arrow, pc);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RequestSpawnArrow(long playerId, int ticket)
    {
        // SERVER ONLY
        if (!Multiplayer.IsServer()) return;
        SpawnNetworkedArrow(playerId, ticket);
    }

    private void SpawnNetworkedArrow(long playerId, int ticket)
    {
        string uniqueName = $"Arrow_{playerId}_{ticket}";

        PlayerController owner = null;
        if (NetworkManager.Instance != null && NetworkManager.Instance.GetPlayer(playerId) is PlayerController pc)
        {
            owner = pc;
        }

        // Use the ProjectileSpawner to spawn the arrow (Godot 4 native way)
        var spawner = GetTree().CurrentScene.GetNodeOrNull<MultiplayerSpawner>("ProjectileSpawner");
        ArrowController arrow = null;

        if (spawner == null)
        {
            GD.PrintErr("ArcherySystem: ProjectileSpawner NOT found! Falling back to AddChild.");
            arrow = ArrowScene.Instantiate<ArrowController>();
            arrow.Name = uniqueName;
            var projectiles = GetTree().CurrentScene.GetNodeOrNull("Projectiles");
            if (projectiles != null) projectiles.AddChild(arrow, true);
            else GetTree().CurrentScene.AddChild(arrow);
        }
        else
        {
            // Passing the color in the spawn data ensures it's available immediately on the client
            Color playerColor = owner != null ? GetPlayerColor(owner.PlayerIndex) : Colors.White;
            var spawnData = new Godot.Collections.Dictionary {
                { "color_r", playerColor.R },
                { "color_g", playerColor.G },
                { "color_b", playerColor.B },
                { "player_id", playerId },
                { "name", uniqueName },
                { "team", (int)(owner?.Team ?? MobaTeam.None) }
            };

            // spawner.Spawn() will handle instantiation on all peers via SpawnArrowLocally
            arrow = spawner.Spawn(spawnData) as ArrowController;
        }

        if (arrow != null)
        {
            GD.Print($"ArcherySystem: Spawned Networked Arrow '{arrow.Name}' for Player {playerId}");

            // Assign ownership if it belongs to the Host (who is also _currentPlayer on the server)
            if (_currentPlayer != null && playerId.ToString() == _currentPlayer.Name)
            {
                GD.Print("ArcherySystem (Server): It's Host's arrow! Taking control.");
                _arrow = arrow;
            }

            SetupArrow(arrow, owner);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RequestLaunchArrow(string arrowName, Vector3 startPosition, Vector3 startRotation, Vector3 velocity, Vector3 windVector, bool isPiercing)
    {
        // Received on Server from Client
        // Find the specific arrow instance
        var projectiles = GetTree().CurrentScene.GetNodeOrNull("Projectiles");
        var arrow = projectiles?.GetNodeOrNull<ArrowController>(arrowName);

        if (arrow != null)
        {
            // Broadcast execution to all (Syncs physics/visuals)
            arrow.Rpc(nameof(ArrowController.Launch), startPosition, startRotation, velocity, windVector, isPiercing);
            GD.Print($"[ArcherySystem] Server launching Client arrow: {arrowName} (Piercing: {isPiercing})");
        }
        else
        {
            GD.PrintErr($"[ArcherySystem] RequestLaunchArrow failed: Could not find arrow '{arrowName}'");
        }
    }

    private void SetupArrow(ArrowController arrow, PlayerController owner = null)
    {
        arrow.Connect(ArrowController.SignalName.ArrowSettled, new Callable(this, MethodName.OnArrowSettled));
        arrow.Connect(ArrowController.SignalName.ArrowCollected, new Callable(this, MethodName.CollectArrow));
        EmitSignal(SignalName.ArrowInitialized, arrow);

        // Default owner is _currentPlayer if not specified
        PlayerController actualOwner = owner ?? _currentPlayer;

        // Exclude player
        if (actualOwner != null) arrow.SetCollisionException(actualOwner);

        arrow.Visible = true;

        if (arrow.LinearVelocity.LengthSquared() < 0.1f)
        {
            arrow.Freeze = true;
        }

        // Apply Player Color
        if (actualOwner != null) arrow.SetColor(GetPlayerColor(actualOwner.PlayerIndex));

        // Initial Pose only if it's OUR arrow and we haven't fired it (handled in UpdateArrowPose)
        if (actualOwner == _currentPlayer)
        {
            UpdateArrowPose();
        }
    }
}
