using Godot;
using System;

namespace Archery;

public partial class DistanceMarker : InteractableObject
{
    [Export] public string Text = "100y";
    [Export] public Color TextColor = Colors.Black;
    [Export] public bool DynamicDistance = true;

    // Destruction Properties
    [Export] public float MaxHealth = 50.0f;
    private float _currentHealth;
    private bool _isDestroyed = false;

    private Node3D _origin;
    private Label3D _label;
    private Label _label2D; // Explicit declaration needed
    private Vector3 _lastPos;
    private ImageTexture _cachedDebrisTexture; // Explicit definition
    // Visual references
    private MeshInstance3D _boardMesh;
    private CollisionShape3D _collider;

    public override void _Ready()
    {
        base._Ready();
        IsTargetable = true;
        _currentHealth = MaxHealth;

        // GitHub Logic: Find Label3D
        _label = GetNodeOrNull<Label3D>("Board/Label3D") ?? GetNodeOrNull<Label3D>("Label3D");
        _label2D = GetNodeOrNull<Label>("SubViewport/Label");

        _boardMesh = GetNodeOrNull<MeshInstance3D>("Board");
        _collider = GetNodeOrNull<CollisionShape3D>("StaticBody3D/CollisionShape3D");

        CallDeferred(MethodName.FindTarget);

        // Capture texture after a short delay
        GetTree().CreateTimer(0.5f).Timeout += CaptureTexture;
    }

    private void CaptureTexture()
    {
        if (_label2D != null && _label2D.GetParent() is SubViewport vp)
        {
            var vpTex = vp.GetTexture();
            var img = vpTex.GetImage();
            if (img != null && !img.IsEmpty())
            {
                _cachedDebrisTexture = ImageTexture.CreateFromImage(img);
            }
        }
    }

    private void FindTarget()
    {
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("VisualTee", true, false) as Node3D;
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("TeeBox", true, false) as Node3D;
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("Tee", true, false) as Node3D;

        if (_origin == null)
        {
            var targets = GetTree().GetNodesInGroup("targets");
            foreach (Node n in targets)
            {
                if (n is Node3D n3d && n.Name.ToString().ToLower().Contains("tee"))
                {
                    _origin = n3d;
                    break;
                }
            }
        }

        if (_origin != null) UpdateDistance();

        if (_label != null)
        {
            _label.Modulate = TextColor;
            UpdateDistance();
        }
    }

    public void UpdateDistance()
    {
        if (_isDestroyed || !DynamicDistance || _origin == null || _label == null) return;

        float dist = GlobalPosition.DistanceTo(_origin.GlobalPosition);
        float yards = dist * ArcheryConstants.UNIT_RATIO;

        string txt = $"{Mathf.RoundToInt(yards)}y";

        // Only update text if changed to avoid continuous texture rebaking needs
        if (_label.Text != txt)
        {
            _label.Text = txt;
            if (_label2D != null)
            {
                _label2D.Text = txt;
                // Queue a re-capture if text changes (deferred to allow render)
                GetTree().CreateTimer(0.1f).Timeout += CaptureTexture;
            }
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!_isDestroyed && DynamicDistance && (IsSelected || GlobalPosition.DistanceSquaredTo(_lastPos) > 0.001f || Engine.GetFramesDrawn() % 60 == 0))
        {
            UpdateDistance();
            _lastPos = GlobalPosition;
        }
    }

    private bool _hasGrit = true; // Allows surviving the first fatal hit if it's part of a combo
    private Timer _gritTimer;

    public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitDirection)
    {
        if (_isDestroyed) return;

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.One * 1.1f, 0.05f);
        tween.TweenProperty(this, "scale", Vector3.One, 0.05f);

        // Analyze Hit Direction
        Vector3 localDir = ToLocal(GlobalPosition + hitDirection).Normalized();
        bool isHorizontalCut = Mathf.Abs(localDir.X) > Mathf.Abs(localDir.Y);

        if (_hasGrit && _currentHealth - damage <= 0 && isHorizontalCut)
        {
            // Survive with 1 HP to see if a vertical slam combo is incoming
            _currentHealth = 1.0f;
            _hasGrit = false; // Consumed

            // Queue delayed death if no follow-up comes
            if (_gritTimer == null)
            {
                _gritTimer = new Timer();
                _gritTimer.OneShot = true;
                _gritTimer.WaitTime = 0.4f; // Window for combo follow-up
                _gritTimer.Timeout += () =>
                {
                    if (!_isDestroyed) SliceSign(hitDirection); // Die from original hit if still alive
                };
                AddChild(_gritTimer);
            }
            _gritTimer.Start();
            return;
        }

        _currentHealth -= damage;
        if (_currentHealth <= 0)
        {
            // If we are here, either Grit was consumed, or it wasn't a horizontal killing blow, 
            // OR (most importantly) it's the second hit of the combo (vertical)!
            if (_gritTimer != null && !_gritTimer.IsStopped()) _gritTimer.Stop(); // Cancel delayed death
            SliceSign(hitDirection);
        }
    }

    private void SliceSign(Vector3 hitDirection)
    {
        _isDestroyed = true;

        if (_boardMesh != null) _boardMesh.Visible = false;
        if (_label != null) _label.Visible = false;
        if (_collider != null) _collider.Disabled = true;

        // Use cached texture if available
        Texture2D texture = _cachedDebrisTexture;

        // Fallback or retry capture if missing (e.g. destroyed instantly)
        if (texture == null && _label2D != null && _label2D.GetParent() is SubViewport vp)
        {
            var img = vp.GetTexture().GetImage();
            if (img != null) texture = ImageTexture.CreateFromImage(img);
        }

        // Default cut logic
        // Project direction to local space to determine cut axis
        // Similar to previous logic

        // Simplified: Horizontal vs Vertical
        // If swinging sideways (horizontal), we cut horizontally (Top/Bottom pieces)
        // If swinging down (vertical), we cut vertically (Left/Right pieces)
        // Note: hitDirection is World Space velocity.
        // Assuming sign is upright (Y up).

        Vector3 localDir = ToLocal(GlobalPosition + hitDirection).Normalized();
        bool isHorizontalCut = Mathf.Abs(localDir.X) > Mathf.Abs(localDir.Y);

        if (isHorizontalCut)
        {
            // Vertical Cut (Left/Right pieces) - For Side/Radial Impacts (Slam/Shockwave)
            CreateDebrisPiece(new Vector3(-0.375f, 0, 0), new Vector2(0, 0), new Vector2(0.5f, 1), texture); // Left
            CreateDebrisPiece(new Vector3(0.375f, 0, 0), new Vector2(0.5f, 0), new Vector2(1, 1), texture); // Right
        }
        else
        {
            // Horizontal Cut (Top/Bottom pieces) - For Frontal Impacts (Standard Horizontal Swings)
            CreateDebrisPiece(new Vector3(0, 0.2f, 0), new Vector2(0, 0), new Vector2(1, 0.5f), texture); // Top
            CreateDebrisPiece(new Vector3(0, -0.2f, 0), new Vector2(0, 0.5f), new Vector2(1, 1), texture); // Bottom
        }
    }

    // Uncommenting GenerateBoardMesh and CreateDebrisPiece logic

    private void CreateDebrisPiece(Vector3 localOffset, Vector2 uvMin, Vector2 uvMax, Texture2D texture)
    {
        var rb = new RigidBody3D();
        GetParent().AddChild(rb);
        rb.GlobalTransform = GlobalTransform;
        rb.Translate(new Vector3(0, 1.6f, 0.06f) + localOffset);

        rb.ApplyImpulse(new Vector3(GD.Randf() - 0.5f, GD.Randf() * 2f, GD.Randf() * 2f));

        var meshInst = new MeshInstance3D();
        rb.AddChild(meshInst);

        float fullWidth = 1.5f;
        float fullHeight = 0.8f;
        float depth = 0.1f;
        float w = fullWidth * (uvMax.X - uvMin.X);
        float h = fullHeight * (uvMax.Y - uvMin.Y);

        // Map UVs correctly. Debris uses GenerateBoardMesh which expects 0-1 range generally?
        // Wait, GenerateBoardMesh uses the uvMin/Max passed in.
        // And maps them to the vertices.
        // So the Texture (Viewport) covers 0,0 to 1,1.
        // We pass the subset UVs.

        meshInst.Mesh = GenerateBoardMesh(new Vector3(w, h, depth), uvMin, uvMax);

        var mat = new StandardMaterial3D();
        if (texture != null)
        {
            mat.AlbedoTexture = texture;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
            mat.Uv1Scale = Vector3.One;
            mat.Uv1Offset = Vector3.Zero;
            mat.Uv1Triplanar = false;
        }
        else
        {
            mat.AlbedoColor = Color.Color8(200, 200, 200);
        }
        meshInst.MaterialOverride = mat;

        var col = new CollisionShape3D();
        var box = new BoxShape3D();
        box.Size = new Vector3(w, h, depth);
        col.Shape = box;
        rb.AddChild(col);

        rb.CollisionLayer = 0;
        rb.CollisionMask = 1 | 2;
    }

    // This method was not present in the original document, but is required by the new code.
    // A basic implementation is provided to ensure syntactic correctness.
    private ArrayMesh GenerateBoardMesh(Vector3 size, Vector2 uvMin, Vector2 uvMax)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        // Use standard C# Lists for easy handling and conversion
        var vertices = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var indices = new System.Collections.Generic.List<int>();

        float x = size.X / 2f;
        float y = size.Y / 2f;
        float z = size.Z / 2f;

        // Front face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, -y, z), new Vector3(-x, -y, z),
            new Vector3(0, 0, 1),
            uvMin, new Vector2(uvMax.X, uvMin.Y), uvMax, new Vector2(uvMin.X, uvMax.Y));

        // Back face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(x, y, -z), new Vector3(-x, y, -z), new Vector3(-x, -y, -z), new Vector3(x, -y, -z),
            new Vector3(0, 0, -1),
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero); // Map to 0,0 (Clean)

        // Top face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z),
            new Vector3(0, 1, 0),
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero); // Map to 0,0 (Clean)

        // Bottom face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, -y, -z), new Vector3(-x, -y, -z),
            new Vector3(0, -1, 0),
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero); // Map to 0,0 (Clean)

        // Right face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(x, y, z), new Vector3(x, y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z),
            new Vector3(1, 0, 0),
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero); // Map to 0,0 (Clean)

        // Left face
        AddFace(vertices, uvs, normals, indices,
            new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(-x, -y, z), new Vector3(-x, -y, -z),
            new Vector3(-1, 0, 0),
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero); // Map to 0,0 (Clean)

        // ToArray is a standard method on List<T>
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var arrayMesh = new ArrayMesh();
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return arrayMesh;
    }

    private void AddFace(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<int> indices,
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, Vector3 normal,
        Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
    {
        int baseIndex = vertices.Count;

        vertices.Add(v1); uvs.Add(uv1); normals.Add(normal);
        vertices.Add(v2); uvs.Add(uv2); normals.Add(normal);
        vertices.Add(v3); uvs.Add(uv3); normals.Add(normal);
        vertices.Add(v4); uvs.Add(uv4); normals.Add(normal);

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }
}
