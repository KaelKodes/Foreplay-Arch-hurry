using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class BuildManager
{
    public List<Vector3> GetSmoothedPoints()
    {
        if (_smoothingIterations <= 0 || _points.Count < 3) return new List<Vector3>(_points);

        List<Vector3> currentPoints = new List<Vector3>(_points);
        for (int iter = 0; iter < _smoothingIterations; iter++)
        {
            List<Vector3> nextPoints = new List<Vector3>();
            for (int i = 0; i < currentPoints.Count; i++)
            {
                Vector3 p0 = currentPoints[i];
                Vector3 p1 = currentPoints[(i + 1) % currentPoints.Count];
                nextPoints.Add(p0.Lerp(p1, 0.25f));
                nextPoints.Add(p0.Lerp(p1, 0.75f));
            }
            currentPoints = nextPoints;
        }
        return currentPoints;
    }

    public void SetPreviewTerrain(int terrainType)
    {
        if (terrainType < 0) terrainType = 0;
        _lastSelectedType = terrainType;

        var pointsToUse = GetSmoothedPoints();
        if (pointsToUse.Count < 3) return;

        if (_previewMeshInstance == null)
        {
            _previewMeshInstance = new CsgPolygon3D { Name = "SurveyPreview", Mode = CsgPolygon3D.ModeEnum.Depth };
            _previewMeshInstance.RotationDegrees = new Vector3(90, 0, 0);
            AddChild(_previewMeshInstance);
        }

        IsPickingTerrain = true;
        UpdateLines();

        Vector3 centroid = Vector3.Zero;
        foreach (var p in pointsToUse) centroid += p;
        centroid /= pointsToUse.Count;

        Vector2[] poly = pointsToUse.Select(p => new Vector2(p.X - centroid.X, p.Z - centroid.Z)).ToArray();
        _previewMeshInstance.Polygon = poly;
        _previewMeshInstance.Show();

        if (CurrentElevation >= 0)
        {
            _previewMeshInstance.Depth = 0.1f + CurrentElevation;
            _previewMeshInstance.GlobalPosition = new Vector3(centroid.X, 0.05f, centroid.Z);
        }
        else
        {
            float depth = Mathf.Abs(CurrentElevation);
            _previewMeshInstance.Depth = depth;
            _previewMeshInstance.GlobalPosition = new Vector3(centroid.X, 0.05f - depth, centroid.Z);
        }

        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        mat.AlbedoColor = terrainType switch
        {
            0 => new Color(0.1f, 0.4f, 0.1f, 0.6f),
            1 => new Color(0.05f, 0.2f, 0.05f, 0.6f),
            2 => new Color(0.02f, 0.15f, 0.02f, 0.6f),
            3 => new Color(0, 1, 0, 0.6f),
            4 => new Color(1, 0.9f, 0.6f, 0.6f),
            5 => new Color(0, 0.4f, 1, 0.6f),
            _ => new Color(1, 1, 1, 0.6f)
        };

        _previewMeshInstance.MaterialOverride = mat;
    }
}
