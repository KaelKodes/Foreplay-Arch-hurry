using Godot;

namespace Archery;

/// <summary>
/// MOBA lane minion. Extends Monsters with armor, gold/XP bounties, team affiliation,
/// and wave-based stat scaling.
/// </summary>
public partial class MobaMinion : Monsters
{
    // ── Exports ──────────────────────────────────────────────
    [Export] public MobaMinionType MinionType = MobaMinionType.Melee;
    [Export] public float Armor = 4f;
    [Export] public float AttackDamage = 12f;
    [Export] public float AttackRange = 2f;
    [Export] public float AttackCooldown = 1.2f;
    [Export] public float MoveSpeed = 3.5f;

    // Bounties
    [Export] public float GoldOnLastHit = 20f;
    [Export] public float GoldOnProximity = 8f;
    [Export] public float XpOnLastHit = 60f;
    [Export] public float XpOnProximity = 20f;

    // ── Runtime state ────────────────────────────────────────
    public bool IsSuperCreep { get; private set; } = false;
    public int WaveNumber { get; set; } = 0;
    private MobaMinionAI _mobaAI;

    public override void _Ready()
    {
        base._Ready();

        // ── Fix Floating ──
        // The base class (Monsters) calls CenterAndGroundVisuals which offsets the mesh.
        // For MOBA minions, we want to respect the scene-defined position or force ground.
        var scene = GetNodeOrNull<Node3D>("Visuals/scene");
        if (scene != null)
        {
            scene.Position = Vector3.Zero;
        }
        GlobalPosition = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);

        // Add to team + minion groups for targeting
        AddToGroup("minions");
        AddToGroup($"team_{Team.ToString().ToLower()}");

        // Remove the generic MonsterAI and replace with lane AI
        var genericAI = GetNodeOrNull<MonsterAI>("MonsterAI");
        if (genericAI != null)
        {
            genericAI.QueueFree();
        }

        _mobaAI = GetNodeOrNull<MobaMinionAI>("MobaMinionAI");
        if (_mobaAI == null)
        {
            _mobaAI = new MobaMinionAI();
            _mobaAI.Name = "MobaMinionAI";
            AddChild(_mobaAI);
        }

        // Apply a light team color tint to all meshes
        ApplyTeamTint();

#if DEBUG
        // GD.Print($"[MobaMinion] {Name} spawned - Team:{Team} Type:{MinionType} HP:{Health} ATK:{AttackDamage} ARM:{Armor}");
#endif
    }

    /// <summary>
    /// Lightly tint all meshes on this minion with the team color.
    /// </summary>
    private void ApplyTeamTint()
    {
        Color teamColor = TeamSystem.GetTeamColor(Team);
        // Blend team color at 25% opacity over the original
        TintMeshesRecursive(this, teamColor, 0.25f);
    }

    private void TintMeshesRecursive(Node node, Color tint, float blend)
    {
        if (node is MeshInstance3D mesh)
        {
            var existingMat = mesh.GetActiveMaterial(0);
            if (existingMat is StandardMaterial3D stdMat)
            {
                var tinted = (StandardMaterial3D)stdMat.Duplicate();
                tinted.AlbedoColor = tinted.AlbedoColor.Lerp(tint, blend);
                mesh.MaterialOverride = tinted;
            }
            else
            {
                // No existing material, create a new tinted one
                var newMat = new StandardMaterial3D();
                newMat.AlbedoColor = tint.Lerp(Colors.White, 0.5f); // Light tint
                newMat.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                mesh.MaterialOverride = newMat;
            }
        }
        foreach (Node child in node.GetChildren())
        {
            TintMeshesRecursive(child, tint, blend);
        }
    }

    // ── Movement & Animation ────────────────────────────────

    /// <summary>
    /// Apply velocity for movement. Uses CharacterBody3D.MoveAndSlide internally.
    /// </summary>
    public override void ApplyMovement(Vector3 velocity, float delta)
    {
        // MobaMinion inherits Node3D chain, use direct position movement
        GlobalPosition += velocity * delta;
    }

    /// <summary>
    /// Play a named animation if an AnimationPlayer exists.
    /// </summary>
    public override void SetAnimation(string animName)
    {
        var animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (animPlayer == null)
        {
            // Search recursively
            animPlayer = FindAnimPlayerRecursive(this);
        }

        if (animPlayer != null && animPlayer.HasAnimation(animName))
        {
            if (animPlayer.CurrentAnimation != animName)
            {
                animPlayer.Play(animName);
            }
        }
    }

    private AnimationPlayer FindAnimPlayerRecursive(Node node)
    {
        if (node is AnimationPlayer ap) return ap;
        foreach (Node child in node.GetChildren())
        {
            var found = FindAnimPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    // ── Damage with Armor ────────────────────────────────────
    public override void OnHit(float rawDamage, Vector3 hitPosition, Vector3 hitDirection)
    {
        // Armor reduces damage: effectiveDmg = raw * (100 / (100 + armor))
        float effectiveDamage = rawDamage * (100f / (100f + Armor));
        base.OnHit(effectiveDamage, hitPosition, hitDirection);
    }

    /// <summary>
    /// Called by MobaMinionAI when this minion attacks a target.
    /// </summary>
    public void PerformAttack(Node3D target)
    {
        if (target is MobaMinion enemyMinion)
        {
            enemyMinion.OnHit(AttackDamage, enemyMinion.GlobalPosition, Vector3.Up);
        }
        else if (target is MobaTower tower)
        {
            tower.TakeDamage(AttackDamage);
        }
        else if (target is MobaNexus nexus)
        {
            nexus.TakeDamage(AttackDamage);
        }
        else if (target is PlayerController player)
        {
            // TODO: player damage system
#if DEBUG
            // GD.Print($"[MobaMinion] {Name} attacks player {player.Name} for {AttackDamage}");
#endif
        }
    }

    // ── Scaling ──────────────────────────────────────────────

    /// <summary>
    /// Apply wave-based stat scaling. Called by MobaGameManager before spawning.
    /// Every 10 waves: +15% stats.
    /// </summary>
    public void ApplyWaveScaling(int waveNumber)
    {
        WaveNumber = waveNumber;
        int tiers = waveNumber / 10;
        if (tiers <= 0) return;

        float multiplier = Mathf.Pow(1.15f, tiers);
        Health *= multiplier;
        MaxHealth = Health;
        AttackDamage *= multiplier;
        Armor *= multiplier;

#if DEBUG
        // GD.Print($"[MobaMinion] Wave scaling x{tiers} applied. HP:{Health:F0} ATK:{AttackDamage:F1} ARM:{Armor:F1}");
#endif
    }

    /// <summary>
    /// Upgrade to Super Creep (+20% scale, +15% stats).
    /// Called when the enemy inner turret has been destroyed.
    /// </summary>
    public void MakeSuperCreep()
    {
        if (IsSuperCreep) return;
        IsSuperCreep = true;

        // +15% stats
        Health *= 1.15f;
        MaxHealth = Health;
        AttackDamage *= 1.15f;
        Armor *= 1.15f;

        // +20% visual scale
        Scale *= 1.2f;

#if DEBUG
        // GD.Print($"[MobaMinion] {Name} upgraded to SUPER CREEP!");
#endif
    }

    protected override void Die()
    {
        base.Die();
        // Thoroughly remove all collision in MOBA to prevent blocking movement
        DisableCollisionRecursive(this);
#if DEBUG
        // GD.Print($"[MobaMinion] {Name} collision completely disabled on death.");
#endif
    }

    private void DisableCollisionRecursive(Node node)
    {
        if (node is CollisionShape3D col) col.SetDeferred("disabled", true);
        foreach (Node child in node.GetChildren())
        {
            DisableCollisionRecursive(child);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
    }
}

/// <summary>
/// Minion type enum.
/// </summary>
public enum MobaMinionType
{
    Melee,
    Ranged
}
