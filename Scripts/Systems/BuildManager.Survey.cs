using Godot;
using System.Collections.Generic;

namespace Archery;

public partial class BuildManager
{
    public void AddPoint(Vector3 position)
    {
        _points.Add(position);
        CreateMarker(position);
        UpdateLines();
        EmitSignal(SignalName.SurveyUpdated, _points.Count);
    }

    private void CreateMarker(Vector3 position)
    {
        var root = new Node3D();
        AddChild(root);
        root.GlobalPosition = position + new Vector3(0, 0.1f, 0);

        var mesh = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.2f, Height = 0.4f };
        mesh.Mesh = sphere;

        var mat = new StandardMaterial3D { AlbedoColor = Colors.Yellow };
        mesh.MaterialOverride = mat;
        root.AddChild(mesh);

        var label = new Label3D();
        label.Name = "Label3D";
        label.Text = (_points.Count).ToString();
        label.FontSize = 48;
        label.OutlineSize = 12;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.Position = new Vector3(0, 0.5f, 0);
        root.AddChild(label);

        _markers.Add(root);
    }

    private void UpdateMarkerLabels()
    {
        for (int i = 0; i < _markers.Count; i++)
        {
            var label = _markers[i].GetNodeOrNull<Label3D>("Label3D");
            if (label != null) label.Text = (i + 1).ToString();
        }
    }

    public void UpdateLines()
    {
        _lineMesh.ClearSurfaces();
        if (_points.Count < 2) return;

        _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < _points.Count - 1; i++)
        {
            _lineMesh.SurfaceAddVertex(_points[i] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(_points[i + 1] + new Vector3(0, 0.2f, 0));
        }

        if (!IsPickingTerrain && Player != null && Player.CurrentState == PlayerState.BuildMode && _replacingIndex == -1)
        {
            _lineMesh.SurfaceAddVertex(_points[_points.Count - 1] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(Player.GlobalPosition + new Vector3(0, 0.2f, 0));

            if (_points.Count >= 2)
            {
                _lineMesh.SurfaceAddVertex(Player.GlobalPosition + new Vector3(0, 0.2f, 0));
                _lineMesh.SurfaceAddVertex(_points[0] + new Vector3(0, 0.2f, 0));
            }
        }
        else if (_points.Count >= 3)
        {
            _lineMesh.SurfaceAddVertex(_points[_points.Count - 1] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(_points[0] + new Vector3(0, 0.2f, 0));
        }

        _lineMesh.SurfaceEnd();
    }

    private void RemovePoint(int index)
    {
        _points.RemoveAt(index);
        _markers[index].QueueFree();
        _markers.RemoveAt(index);
        UpdateMarkerLabels();
        UpdateLines();
        EmitSignal(SignalName.SurveyUpdated, _points.Count);
    }

    public void ClearSurvey()
    {
        _points.Clear();
        foreach (var m in _markers) m.QueueFree();
        _markers.Clear();
        _lineMesh.ClearSurfaces();
        EmitSignal(SignalName.SurveyUpdated, 0);
        if (_previewMeshInstance != null) _previewMeshInstance.QueueFree();
        _previewMeshInstance = null;
        IsPickingTerrain = false;
        CurrentElevation = 0.0f;
        SmoothingIterations = 0; // Uses the property now
    }
}
