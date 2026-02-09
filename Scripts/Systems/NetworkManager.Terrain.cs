using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class NetworkManager
{
    private MultiplayerSpawner _terrainSpawner;

    public void SetupTerrainSpawner()
    {
        // Ensure CSG Combiner exists
        var root = GetTree().CurrentScene;
        var csgRoot = root.GetNodeOrNull<CsgCombiner3D>("TerrainCombiner");
        if (csgRoot == null)
        {
            csgRoot = new CsgCombiner3D();
            csgRoot.Name = "TerrainCombiner";
            csgRoot.UseCollision = true;
            root.AddChild(csgRoot);
        }

        if (_terrainSpawner != null) { _terrainSpawner.QueueFree(); _terrainSpawner = null; }

        _terrainSpawner = new MultiplayerSpawner();
        _terrainSpawner.Name = "TerrainSpawner";
        AddChild(_terrainSpawner);
        _terrainSpawner.SpawnPath = csgRoot.GetPath();
        _terrainSpawner.SpawnFunction = new Callable(this, nameof(SpawnNetworkObject)); // Reuse same function

        GD.Print($"NetworkManager: TerrainSpawner setup complete. Target: {_terrainSpawner.SpawnPath}");
    }

    public void SyncTerrainToClient(long clientId)
    {
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
        if (terrain != null)
        {
            GD.Print($"NetworkManager: Syncing Heightmap to client {clientId}...");
            float[] heights = terrain.GetFlattenedHeightData();
            int[] types = terrain.GetFlattenedTypeData();
            RpcId((int)clientId, nameof(NetSyncHeightmap), heights, types);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NetSyncHeightmap(float[] heights, int[] types)
    {
        GD.Print("NetworkManager: Received Heightmap Sync from Server.");
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
        if (terrain != null)
        {
            terrain.SetFlattenedData(heights, types);
        }
        else
        {
            GD.PrintErr("NetworkManager: Could not find terrain for sync!");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RequestBakeTerrain(Godot.Collections.Array<Vector3> points, float elevation, int type)
    {
        if (!Multiplayer.IsServer()) return;

        GD.Print($"NetworkManager: RequestBakeTerrain received. Type: {type}, Elev: {elevation}, Pts: {points.Count}");

        var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
        if (heightmap == null)
        {
            var terrains = GetTree().GetNodesInGroup("terrain");
            if (terrains.Count > 0 && terrains[0] is HeightmapTerrain ht) heightmap = ht;
        }

        if (heightmap != null)
        {
            Rpc(nameof(NetDeformTerrain), points, elevation, type);
        }
        else
        {
            var data = new Godot.Collections.Dictionary
            {
                { "type", "terrain" },
                { "points", points },
                { "elev", elevation },
                { "tType", type },
                { "pos", Vector3.Zero },
                { "rot", Vector3.Zero },
                { "scale", Vector3.One }
            };

            _terrainSpawner.Spawn(data);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void NetDeformTerrain(Godot.Collections.Array<Vector3> points, float elevation, int type)
    {
        GD.Print("NetworkManager: NetDeformTerrain executing locally...");

        var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
        if (heightmap == null)
        {
            var terrains = GetTree().GetNodesInGroup("terrain");
            if (terrains.Count > 0 && terrains[0] is HeightmapTerrain ht) heightmap = ht;
        }

        if (heightmap != null)
        {
            Vector3[] pts = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++) pts[i] = points[i];

            heightmap.DeformArea(pts, elevation, type);
        }
    }
}
