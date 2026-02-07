using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Tower type determines behavior.
/// </summary>
public enum TowerType
{
    Outer,  // Defend only
    Inner   // Defend + spawn point
}

/// <summary>
/// MOBA tower that defends its lane with projectile attacks.
/// </summary>
public partial class MobaTower : Node3D
{
    [Export] public MobaTeam Team = MobaTeam.None;
    [Export] public TowerType Type = TowerType.Outer;
    [Export] public float MaxHealth = 3000f;
    [Export] public float AttackRange = 20f;
    [Export] public float AttackDamage = 100f;
    [Export] public float AttackCooldown = 1.0f;

    public float Health { get; private set; }
    public bool IsDestroyed => Health <= 0;

    private Node3D _currentTarget;
    private float _attackTimer = 0f;
    private float _hpBarTimer = 0f;
    private MeshInstance3D _teamColorMesh;

    // HP Bar
    private ProgressBar _hpBar;
    private SubViewport _hpViewport;
    private Sprite3D _hpSprite;
    private bool _hpBarVisible = false;

    public override void _Ready()
    {
        Health = MaxHealth;
        AddToGroup("towers");
        AddToGroup($"team_{Team.ToString().ToLower()}");

        // Find mesh to apply team color
        _teamColorMesh = FindMeshRecursive(this);
        ApplyTeamColor();

        GD.Print($"[MobaTower] {Name} initialized - Team: {Team}, Type: {Type}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDestroyed) return;

        float dt = (float)delta;
        _attackTimer -= dt;

        // Visual health bar check - hide if no damage for a while
        UpdateHpBarVisibility(dt);

        // Find target if we don't have one
        if (_currentTarget == null || !IsValidTarget(_currentTarget))
        {
            _currentTarget = FindBestTarget();
        }

        // Attack if we have a target and cooldown is ready
        if (_currentTarget != null && _attackTimer <= 0)
        {
            Attack(_currentTarget);
            _attackTimer = AttackCooldown;
        }
    }

    private Node3D FindBestTarget()
    {
        Node3D bestTarget = null;
        float bestDistance = AttackRange;
        bool foundMinion = false;

        // Get all potential targets (enemies)
        var enemies = GetTree().GetNodesInGroup($"team_{TeamSystem.GetEnemyTeam(Team).ToString().ToLower()}");

        foreach (var node in enemies)
        {
            if (node is not Node3D target) continue;
            if (!IsValidTarget(target)) continue;

            float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
            if (dist > AttackRange) continue;

            // Priority: Minions first, then champions/players
            bool isMinion = target.IsInGroup("minions");

            // If we already found a minion, skip non-minions
            if (foundMinion && !isMinion) continue;

            // If this is a minion and we haven't found one yet, prefer it
            if (isMinion && !foundMinion)
            {
                foundMinion = true;
                bestTarget = target;
                bestDistance = dist;
            }
            else if (dist < bestDistance)
            {
                bestTarget = target;
                bestDistance = dist;
            }
        }

        return bestTarget;
    }

    private bool IsValidTarget(Node3D target)
    {
        if (target == null) return false;
        if (!IsInstanceValid(target)) return false;
        if (!target.IsInsideTree()) return false;

        // Check if target is dead
        if (target is Monsters monster && monster.Health <= 0) return false;

        return true;
    }

    private void Attack(Node3D target)
    {
        // Spawn a projectile toward the target
        var projectileScene = GD.Load<PackedScene>("res://Scenes/MOBA/MobaProjectile.tscn");
        if (projectileScene != null)
        {
            var projectile = projectileScene.Instantiate<MobaProjectile>();
            projectile.Damage = AttackDamage;
            projectile.SourceTeam = Team;
            projectile.Target = target;
            projectile.ArcHeight = 6f; // Tower shots arc higher
            projectile.Speed = 20f;

            // Add to tree, then set position
            GetTree().CurrentScene.AddChild(projectile);
            projectile.GlobalPosition = GlobalPosition + Vector3.Up * 5f;
            projectile.Initialize();

            // Apply team color to tower projectile
            var mesh = projectile.GetNodeOrNull<MeshInstance3D>("SpikeMesh");
            if (mesh != null)
            {
                var mat = new StandardMaterial3D();
                Color teamColor = TeamSystem.GetTeamColor(Team);
                mat.AlbedoColor = teamColor;
                mat.EmissionEnabled = true;
                mat.Emission = teamColor;
                mat.EmissionEnergyMultiplier = 2f;
                mesh.MaterialOverride = mat;
            }
        }
        else
        {
            // Fallback: direct damage
            if (target is Monsters monster)
            {
                monster.OnHit(AttackDamage, target.GlobalPosition, Vector3.Up);
            }
            else if (target is PlayerController player)
            {
                GD.Print($"[MobaTower] Would deal {AttackDamage} to player {player.Name}");
            }
        }
    }

    /// <summary>
    /// Called when tower takes damage.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        Health -= damage;
        _hpBarTimer = 0f; // Reset visibility timer on damage
        if (_hpSprite != null) _hpSprite.Visible = true;

        // Show/update HP bar
        UpdateHpBar();

        if (Health <= 0)
        {
            OnDestroyed();
        }
    }

    private void UpdateHpBar()
    {
        if (!_hpBarVisible)
        {
            CreateHpBar();
            _hpBarVisible = true;
        }

        if (_hpBar != null)
        {
            _hpBar.Value = (Health / MaxHealth) * 100.0;
        }
    }

    private void CreateHpBar()
    {
        // Create a SubViewport + Sprite3D combo for the HP bar
        _hpViewport = new SubViewport();
        _hpViewport.Size = new Vector2I(200, 20);
        _hpViewport.TransparentBg = true;
        _hpViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        _hpBar = new ProgressBar();
        _hpBar.MinValue = 0;
        _hpBar.MaxValue = 100;
        _hpBar.Value = (Health / MaxHealth) * 100.0;
        _hpBar.ShowPercentage = false;
        _hpBar.Size = new Vector2(200, 20);
        _hpBar.Position = Vector2.Zero;

        // Style the bar with team colors
        var styleFill = new StyleBoxFlat();
        Color teamColor = TeamSystem.GetTeamColor(Team);
        styleFill.BgColor = teamColor;
        _hpBar.AddThemeStyleboxOverride("fill", styleFill);

        var styleBg = new StyleBoxFlat();
        styleBg.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        _hpBar.AddThemeStyleboxOverride("background", styleBg);

        _hpViewport.AddChild(_hpBar);
        AddChild(_hpViewport);

        _hpSprite = new Sprite3D();
        _hpSprite.Texture = _hpViewport.GetTexture();
        _hpSprite.PixelSize = 0.02f;
        _hpSprite.Position = new Vector3(0, 8f, 0); // Above tower
        _hpSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        AddChild(_hpSprite);
    }

    private void OnDestroyed()
    {
        GD.Print($"[MobaTower] {Name} DESTROYED!");

        // Notify game manager
        var gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
        gameManager?.OnTowerDestroyed(this);

        // Clean up HP bar
        _hpSprite?.QueueFree();
        _hpViewport?.QueueFree();

        // Visual: could hide, play effect, etc.
        Visible = false;
        SetPhysicsProcess(false);
    }

    private void ApplyTeamColor()
    {
        if (_teamColorMesh == null) return;

        bool colorblind = false; // TODO: Get from GameSettings
        Color teamColor = TeamSystem.GetTeamColor(Team, colorblind);

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = teamColor;
        mat.EmissionEnabled = true;
        mat.Emission = teamColor;
        mat.EmissionEnergyMultiplier = 0.3f;

        _teamColorMesh.MaterialOverride = mat;
    }

    private MeshInstance3D FindMeshRecursive(Node node)
    {
        if (node is MeshInstance3D mesh) return mesh;
        foreach (Node child in node.GetChildren())
        {
            var found = FindMeshRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateHpBarVisibility(float dt)
    {
        if (!_hpBarVisible) return;

        _hpBarTimer += dt;
        if (_hpBarTimer > 5.0f) // Hide after 5 seconds of peace
        {
            if (_hpSprite != null) _hpSprite.Visible = false;
            _hpBarVisible = false;
        }
    }
}
