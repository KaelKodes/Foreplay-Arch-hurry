using Godot;
using System.Collections.Generic;
using Archery;

namespace Archery;

public partial class BuildManager : Node3D
{
    private List<Vector3> _points = new List<Vector3>();
    private List<Node3D> _markers = new List<Node3D>();
    private ImmediateMesh _lineMesh;
    private MeshInstance3D _lineInstance;
    [Signal] public delegate void SurveyUpdatedEventHandler(int pointCount);
    public int PointCount => _points.Count;
    private CsgPolygon3D _previewMeshInstance;

    public float CurrentElevation { get; private set; } = 0.0f;
    private float _fillPercentage = 80.0f;
    private int _smoothingIterations = 0;
    public int SmoothingIterations
    {
        get => _smoothingIterations;
        set { _smoothingIterations = value; SetPreviewTerrain(_lastSelectedType); }
    }

    private CsgCombiner3D _csgRoot = null;
    private SurveyedTerrain _closestTerrain = null;

    public PlayerController Player;
    private ArcherySystem _archerySystem;

    private int _closestMarkerIndex = -1;
    private int _replacingIndex = -1;
    private float _inputCooldown = 0.0f;
    public bool IsPickingTerrain = false;
    private int _lastSelectedType = 0;
    private MainHUDController _hud;

    public override void _Ready()
    {
        _archerySystem = GetParent<ArcherySystem>();
        _lineInstance = new MeshInstance3D();
        _lineMesh = new ImmediateMesh();
        _lineInstance.Mesh = _lineMesh;

        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Yellow, NoDepthTest = true };
        _lineInstance.MaterialOverride = mat;
        _hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;
        AddChild(_lineInstance);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Player == null || Player.CurrentState != PlayerState.BuildMode || _inputCooldown > 0) return;

        bool isSurvey = (_hud != null && _hud.CurrentTool == MainHUDController.BuildTool.Survey);

        if (_replacingIndex != -1)
        {
            if (@event.IsActionPressed("ui_accept") || (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Space))
            {
                _replacingIndex = -1;
                _inputCooldown = 0.3f;
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (_closestTerrain != null)
        {
            if (@event is InputEventKey ek && ek.Pressed)
            {
                if (ek.Keycode == Key.E) { EditTerrain(_closestTerrain); _inputCooldown = 0.5f; GetViewport().SetInputAsHandled(); }
                else if (ek.Keycode == Key.R) { CopyTerrain(_closestTerrain); _inputCooldown = 0.5f; GetViewport().SetInputAsHandled(); }
                else if (ek.Keycode == Key.Delete) { _closestTerrain.QueueFree(); _inputCooldown = 0.5f; GetViewport().SetInputAsHandled(); }
            }
            if (GetViewport().IsInputHandled()) return;
        }

        if (isSurvey)
        {
            if (@event is InputEventKey ek && ek.Pressed && ek.Keycode == Key.T) { ClearSurvey(); _inputCooldown = 0.3f; GetViewport().SetInputAsHandled(); return; }

            if (_closestMarkerIndex != -1 && (Player == null || Player.SelectedObject == null))
            {
                if (@event is InputEventKey mk && mk.Pressed)
                {
                    if (mk.Keycode == Key.X) { RemovePoint(_closestMarkerIndex); _inputCooldown = 0.3f; GetViewport().SetInputAsHandled(); }
                    else if (mk.Keycode == Key.C) { _replacingIndex = _closestMarkerIndex; _inputCooldown = 0.3f; GetViewport().SetInputAsHandled(); }
                }
            }
            else if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                AddPoint(Player.GlobalPosition);
                _inputCooldown = 0.5f;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_inputCooldown > 0) _inputCooldown -= (float)delta;
        if (Player == null || Player.CurrentState != PlayerState.BuildMode) return;

        Vector3 playerPos = Player.GlobalPosition;

        if (_replacingIndex != -1)
        {
            _points[_replacingIndex] = playerPos;
            _markers[_replacingIndex].GlobalPosition = playerPos + new Vector3(0, 0.1f, 0);
            UpdateLines();
            return;
        }

        _closestMarkerIndex = -1;
        float minDist = 2.0f;
        for (int i = 0; i < _points.Count; i++)
        {
            float d = playerPos.DistanceTo(_points[i]);
            if (d < minDist) { minDist = d; _closestMarkerIndex = i; }
            var mesh = _markers[i].GetChild<MeshInstance3D>(0);
            ((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor = (i == _closestMarkerIndex) ? Colors.Cyan : Colors.Yellow;
        }

        _closestTerrain = null;
        if (_closestMarkerIndex == -1 && _replacingIndex == -1)
        {
            var terrains = GetTree().GetNodesInGroup("surveyed_terrain");
            float minTD = 5.0f;
            foreach (Node t in terrains) if (t is SurveyedTerrain st) foreach (var p in st.Points) { float d = playerPos.DistanceTo(p); if (d < minTD) { minTD = d; _closestTerrain = st; } }
        }

        UpdatePrompt();
        if (_points.Count > 0) UpdateLines();
    }

    private void UpdatePrompt()
    {
        if (_replacingIndex != -1) _archerySystem.SetPrompt(true, "REPLACING POINT: SPACEBAR TO SET");
        else if (_closestTerrain != null) _archerySystem.SetPrompt(true, "E: EDIT TERRAIN | R: COPY | DEL: REMOVE");
        else if (_hud != null && _hud.CurrentTool == MainHUDController.BuildTool.Survey) _archerySystem.SetPrompt(true, "LMB: DROP POINT | T: CLEAR");
        else _archerySystem.SetPrompt(false);
    }
}
