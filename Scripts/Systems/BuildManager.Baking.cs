using Godot;
using System.Linq;

namespace Archery;

public partial class BuildManager
{
    public void BakeTerrain(int terrainType)
    {
        if (terrainType < 0) terrainType = 0;

        var pointsToUse = GetSmoothedPoints();
        if (pointsToUse.Count < 3) return;

        // NETWORK SYNC
        var netManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
        if (netManager != null && netManager.Multiplayer.HasMultiplayerPeer())
        {
            var gdPoints = new Godot.Collections.Array<Vector3>();
            foreach (var p in pointsToUse) gdPoints.Add(p);

            netManager.RpcId(1, nameof(NetworkManager.RequestBakeTerrain), gdPoints, CurrentElevation, terrainType);

            CurrentElevation = 0.0f;
            ClearSurvey();
            return;
        }

        var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain") ??
                        GetTree().GetNodesInGroup("terrain").OfType<HeightmapTerrain>().FirstOrDefault();

        if (heightmap != null)
        {
            heightmap.DeformArea(pointsToUse.ToArray(), CurrentElevation, terrainType);
            CurrentElevation = 0.0f;
            ClearSurvey();
            return;
        }

        SetupCSGRoot();

        var bakedNode = new SurveyedTerrain();
        bakedNode.Name = $"Terrain_{terrainType}_{Time.GetTicksMsec()}";
        bakedNode.Points = pointsToUse.ToArray();
        bakedNode.TerrainType = terrainType;

        Vector3 centroid = Vector3.Zero;
        foreach (var p in pointsToUse) centroid += p;
        centroid /= pointsToUse.Count;

        Vector2[] localPoly = new Vector2[pointsToUse.Count];
        for (int i = 0; i < pointsToUse.Count; i++)
            localPoly[i] = new Vector2(pointsToUse[i].X - centroid.X, pointsToUse[i].Z - centroid.Z);

        bakedNode.Polygon = localPoly;
        bakedNode.Mode = CsgPolygon3D.ModeEnum.Depth;
        bakedNode.RotationDegrees = new Vector3(90, 0, 0);
        bakedNode.UseCollision = false;

        var mat = new StandardMaterial3D();
        switch (terrainType)
        {
            case 0: mat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f); break;
            case 1: mat.AlbedoColor = new Color(0.15f, 0.3f, 0.15f); break;
            case 2: mat.AlbedoColor = new Color(0.08f, 0.2f, 0.08f); break;
            case 3: mat.AlbedoColor = new Color(0.1f, 0.6f, 0.1f); break;
            case 4: mat.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); break;
            case 5: mat.AlbedoColor = new Color(0.1f, 0.3f, 0.8f); break;
        }
        bakedNode.MaterialOverride = mat;

        if (CurrentElevation >= 0)
        {
            bakedNode.Operation = CsgShape3D.OperationEnum.Union;
            bakedNode.Depth = 0.1f + CurrentElevation;
            _csgRoot.AddChild(bakedNode);
            bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f, centroid.Z);
        }
        else
        {
            float depth = Mathf.Abs(CurrentElevation);
            bakedNode.Operation = CsgShape3D.OperationEnum.Subtraction;
            bakedNode.Depth = depth;
            _csgRoot.AddChild(bakedNode);
            bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f - depth, centroid.Z);

            if (terrainType == 4 || terrainType == 5)
            {
                var filler = new SurveyedTerrain();
                filler.Name = $"{bakedNode.Name}_Filler";
                filler.Points = pointsToUse.ToArray();
                filler.TerrainType = terrainType;
                filler.Polygon = localPoly;
                filler.Operation = CsgShape3D.OperationEnum.Union;
                filler.Mode = CsgPolygon3D.ModeEnum.Depth;
                filler.RotationDegrees = new Vector3(90, 0, 0);
                filler.UseCollision = false;

                float fillHeight = depth * (_fillPercentage / 100.0f);
                filler.Depth = fillHeight;
                filler.MaterialOverride = mat;
                _csgRoot.AddChild(filler);
                filler.GlobalPosition = new Vector3(centroid.X, 0.1f - depth, centroid.Z);
            }
        }

        if (_csgRoot != null)
        {
            _csgRoot.UseCollision = false;
            _csgRoot.UseCollision = true;
        }

        CurrentElevation = 0.0f;
        ClearSurvey();
    }

    private void EditTerrain(SurveyedTerrain terrain)
    {
        ClearSurvey();
        foreach (var p in terrain.Points) AddPoint(p);
        terrain.QueueFree();
    }

    private void CopyTerrain(SurveyedTerrain terrain)
    {
        ClearSurvey();
        Vector3 centroid = Vector3.Zero;
        foreach (var p in terrain.Points) centroid += p;
        centroid /= terrain.Points.Length;

        Vector3 offset = Player.GlobalPosition - centroid;
        foreach (var p in terrain.Points) AddPoint(p + offset);
    }

    private void SetupCSGRoot()
    {
        var world = GetTree().CurrentScene ?? GetTree().Root.GetChild(0);
        _csgRoot = world.GetNodeOrNull<CsgCombiner3D>("TerrainCombiner");

        if (_csgRoot == null)
        {
            _csgRoot = new CsgCombiner3D { Name = "TerrainCombiner", UseCollision = true };
            world.AddChild(_csgRoot);

            var bedrock = new StaticBody3D { Name = "Bedrock" };
            var meshNode = new MeshInstance3D();
            var bMesh = new BoxMesh { Size = new Vector3(2000, 5, 2000) };
            meshNode.Mesh = bMesh;
            meshNode.Position = new Vector3(0, -5.0f, 0);
            meshNode.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.1f, 0.05f) };
            bedrock.AddChild(meshNode);
            world.AddChild(bedrock);
        }

        string[] grounds = { "Fairway", "RoughLeft", "RoughRight", "TeeBox" };
        foreach (var gName in grounds)
        {
            var g = world.GetNodeOrNull<CsgShape3D>(gName);
            if (g != null)
            {
                if (g is CsgBox3D box && box.Size.Y < 5.0f)
                {
                    float originalYSize = box.Size.Y;
                    float newYSize = 20.0f;
                    box.Size = new Vector3(box.Size.X, newYSize, box.Size.Z);
                    box.Position -= new Vector3(0, (newYSize - originalYSize) / 2.0f, 0);
                }
                g.Reparent(_csgRoot, true);
                g.UseCollision = false;
            }
        }
    }

    public void ModifyElevation(float delta)
    {
        CurrentElevation += delta;
        GD.Print($"BuildManager: Elevation modified to {CurrentElevation}");
        SetPreviewTerrain(_lastSelectedType);
    }

    public void SetFillPercentage(float pct)
    {
        _fillPercentage = pct;
        GD.Print($"BuildManager: Fill percentage set to {_fillPercentage}%");
        SetPreviewTerrain(_lastSelectedType);
    }
}
