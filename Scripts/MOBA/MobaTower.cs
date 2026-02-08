using Godot;
using System.Collections.Generic;

namespace Archery;

public enum TowerType
{
    Outer,  // Defend only
    Inner   // Defend + spawn point
}

public partial class MobaTower : InteractableObject
{
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

    private ProgressBar _hpBar;
    private SubViewport _hpViewport;
    private Sprite3D _hpSprite;
    private bool _hpBarVisible = false;

    public override void _Ready()
    {
        Health = MaxHealth;
        AddToGroup("towers");
        AddToGroup($"team_{Team.ToString().ToLower()}");

        _teamColorMesh = FindMeshRecursive(this);
        ApplyTeamColor();

        base._Ready();
        GD.Print($"[MobaTower] {Name} initialized - Team: {Team}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDestroyed) return;

        float dt = (float)delta;
        _attackTimer -= dt;
        UpdateHpBarVisibility(dt);

        if (_currentTarget == null || !IsValidTarget(_currentTarget))
            _currentTarget = FindBestTarget();

        if (_currentTarget != null && _attackTimer <= 0)
        {
            Attack(_currentTarget);
            _attackTimer = AttackCooldown;
        }
    }

    public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;
        Health -= damage;
        _hpBarTimer = 0f;
        if (_hpSprite != null) _hpSprite.Visible = true;
        UpdateHpBar();
        if (Health <= 0) OnDestroyed();
    }

    private Node3D FindBestTarget()
    {
        Node3D bestTarget = null;
        float bestDistance = AttackRange;
        bool foundMinion = false;
        var enemies = GetTree().GetNodesInGroup($"team_{TeamSystem.GetEnemyTeam(Team).ToString().ToLower()}");

        foreach (var node in enemies)
        {
            if (node is not Node3D target || !IsValidTarget(target)) continue;
            float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
            if (dist > AttackRange) continue;
            bool isMinion = target.IsInGroup("minions");
            if (foundMinion && !isMinion) continue;
            if (isMinion && !foundMinion) { foundMinion = true; bestTarget = target; bestDistance = dist; }
            else if (dist < bestDistance) { bestTarget = target; bestDistance = dist; }
        }
        return bestTarget;
    }

    private bool IsValidTarget(Node3D target)
    {
        if (target == null || !IsInstanceValid(target)) return false;
        if (target is Monsters monster && monster.Health <= 0) return false;
        if (target is InteractableObject io && (io.Team == Team || io.Team == MobaTeam.None)) return false;
        return true;
    }

    private void Attack(Node3D target)
    {
        var projectileScene = GD.Load<PackedScene>("res://Scenes/MOBA/MobaProjectile.tscn");
        if (projectileScene != null)
        {
            var projectile = projectileScene.Instantiate<MobaProjectile>();
            projectile.Damage = AttackDamage;
            projectile.SourceTeam = Team;
            projectile.Target = target;
            projectile.ArcHeight = 6f;
            projectile.Speed = 20f;
            GetTree().CurrentScene.AddChild(projectile);
            projectile.GlobalPosition = GlobalPosition + Vector3.Up * 5f;
            projectile.Initialize();
        }
    }

    private void UpdateHpBar()
    {
        if (!_hpBarVisible) { CreateHpBar(); _hpBarVisible = true; }
        if (_hpBar != null) _hpBar.Value = (Health / MaxHealth) * 100.0;
    }

    private void CreateHpBar()
    {
        _hpViewport = new SubViewport();
        _hpViewport.Size = new Vector2I(200, 20);
        _hpViewport.TransparentBg = true;
        _hpBar = new ProgressBar();
        _hpBar.Size = new Vector2(200, 20);
        _hpBar.ShowPercentage = false;
        _hpViewport.AddChild(_hpBar);
        AddChild(_hpViewport);

        _hpSprite = new Sprite3D();
        _hpSprite.Texture = _hpViewport.GetTexture();
        _hpSprite.Position = new Vector3(0, 8f, 0);
        _hpSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        AddChild(_hpSprite);
    }

    private void OnDestroyed()
    {
        GD.Print($"[MobaTower] {Name} DESTROYED!");
        var gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
        gameManager?.OnTowerDestroyed(this);
        Visible = false;
        SetPhysicsProcess(false);
    }

    private void ApplyTeamColor()
    {
        if (_teamColorMesh == null) return;
        Color teamColor = TeamSystem.GetTeamColor(Team);
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = teamColor;
        _teamColorMesh.MaterialOverride = mat;
    }

    private void UpdateHpBarVisibility(float dt)
    {
        if (!_hpBarVisible) return;
        _hpBarTimer += dt;
        if (_hpBarTimer > 5.0f) { if (_hpSprite != null) _hpSprite.Visible = false; _hpBarVisible = false; }
    }
}
