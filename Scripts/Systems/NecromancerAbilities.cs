using Godot;
using Archery;

/// <summary>
/// Static helper to handle Necromancer-specific ability animations and logic.
/// Slot 0: Lifetap (1H Magic Attack)
/// Slot 1: Plague of Darkness (2H Magic Area Attack)
/// Slot 2: Summon Undead (Corpse Consumption)
/// Slot 3: Lotus Trap (Utility Area)
/// </summary>
public static class NecromancerAbilities
{
    public static void ExecuteAbility(PlayerController caster, int slot)
    {
        var modelMgr = caster.GetNodeOrNull<CharacterModelManager>("ModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("CharacterModelManager")
                    ?? caster.GetNodeOrNull<CharacterModelManager>("Erika/ModelManager");

        switch (slot)
        {
            case 0: // Lifetap
                {
                    if (caster.CurrentTarget == null)
                    {
                        GD.Print("[NecromancerAbilities] No target for Lifetap!");
                        return;
                    }

                    var target = caster.CurrentTarget;
                    if (target != null && !TargetingHelper.IsTargetDead(target))
                    {
                        // 1. Stats and Mana
                        var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
                        var stats = archery?.PlayerStats;

                        // Mana check
                        if (stats != null && stats.CurrentMana < 20)
                        {
                            GD.Print("[NecromancerAbilities] Not enough mana for Lifetap!");
                            return;
                        }
                        if (stats != null) stats.CurrentMana -= 20;

                        // 2. Play Animation
                        modelMgr?.PlayAnimation("Kick"); // Using Kick as the 1H cast animation
                        GD.Print("[NecromancerAbilities] Slot 1 (Lifetap) triggered");

                        // 3. Calculation
                        int intel = stats?.Intelligence ?? 10;
                        float damage = 80f + (1.5f * intel);
                        float heal = damage * 0.4f;

                        // 4. Hit Effect & Visual Feedback
                        if (target is Monsters m)
                        {
                            m.OnHit(damage, m.GlobalPosition, Vector3.Up, caster);
                            m.FlashRed(0.3f);
                        }
                        else if (target is PlayerController pcTarget)
                        {
                            pcTarget.OnHit(damage, pcTarget.GlobalPosition, Vector3.Up, caster);
                            pcTarget.FlashRed(0.3f);
                        }
                        else if (target is InteractableObject io)
                        {
                            io.OnHit(damage, io.GlobalPosition, Vector3.Up, caster);
                            io.FlashRed(0.3f);
                        }

                        // 5. Spawn Life Drain Orb
                        var orbScene = GD.Load<PackedScene>("res://Scenes/VFX/LifetapOrb.tscn");
                        if (orbScene != null)
                        {
                            var orb = orbScene.Instantiate<LifetapOrb>();
                            caster.GetTree().CurrentScene.AddChild(orb);
                            orb.GlobalPosition = target is Node3D n3d ? n3d.GlobalPosition + new Vector3(0, 1.2f, 0) : caster.GlobalPosition;
                            orb.Initialize(caster, heal);
                        }
                    }
                }
                break;

            case 1: // Plague of Darkness
                {
                    // Prioritize current target (hard or soft), then follow with ground point
                    Node3D target = caster.CurrentTarget;
                    Vector3 spawnPos = target != null ? target.GlobalPosition : TargetingHelper.GetGroundPoint(caster, 15.0f);

                    // 1. Stats and Mana
                    var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
                    var stats = archery?.PlayerStats;

                    // Mana check
                    if (stats != null && stats.CurrentMana < 40)
                    {
                        GD.Print("[NecromancerAbilities] Not enough mana for Plague of Darkness!");
                        return;
                    }
                    if (stats != null) stats.CurrentMana -= 40;

                    // 2. Play Animation
                    modelMgr?.PlayAnimation("Standing 2H Magic Area Attack 02.fbx");
                    GD.Print("[NecromancerAbilities] Slot 2 (Plague of Darkness) triggered");

                    // 3. Spawn Plague Cloud
                    var cloudScene = GD.Load<PackedScene>("res://Scenes/VFX/PlagueCloud.tscn");
                    if (cloudScene != null)
                    {
                        var cloud = cloudScene.Instantiate<PlagueCloud>();
                        caster.GetTree().CurrentScene.AddChild(cloud);
                        cloud.GlobalPosition = spawnPos;
                        cloud.Initialize(caster);
                    }
                }
                break;

            case 2: // Summon Undead (Ability 3) - REWORK: Requires Corpse
                {
                    // 1. Mana check
                    var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
                    var stats = archery?.PlayerStats;
                    if (stats != null && stats.CurrentMana < 30)
                    {
                        GD.Print("[NecromancerAbilities] Not enough mana for Summon Undead!");
                        return;
                    }

                    // 2. Search for nearest corpse
                    Node3D nearestCorpse = null;
                    float minDist = 20.0f;
                    foreach (var node in caster.GetTree().GetNodesInGroup("corpses"))
                    {
                        if (node is Node3D corpse)
                        {
                            float dist = caster.GlobalPosition.DistanceTo(corpse.GlobalPosition);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                nearestCorpse = corpse;
                            }
                        }
                    }

                    if (nearestCorpse == null)
                    {
                        GD.Print("[NecromancerAbilities] No corpse nearby to summon from!");
                        return;
                    }

                    // 3. Consume Corpse & Explode
                    if (stats != null) stats.CurrentMana -= 30;
                    Vector3 spawnPos = nearestCorpse.GlobalPosition;

                    var explosionScene = GD.Load<PackedScene>("res://Scenes/VFX/CorpseExplosion.tscn");
                    if (explosionScene != null)
                    {
                        var explosion = explosionScene.Instantiate<Node3D>();
                        caster.GetTree().CurrentScene.AddChild(explosion);
                        explosion.GlobalPosition = spawnPos;
                        var particles = explosion.GetNodeOrNull<GpuParticles3D>("BloodParticles");
                        if (particles != null) particles.Emitting = true;

                        // Corpse Explosion Damage
                        PerformCorpseExplosionDamage(caster, spawnPos, 6.0f, 40.0f);
                    }

                    nearestCorpse.QueueFree(); // Consume!

                    // 4. Summon
                    modelMgr?.PlayAnimation("PowerUp"); // Cast anim
                    var skeletonScene = GD.Load<PackedScene>("res://Scenes/Entities/Skeleton.tscn");
                    if (skeletonScene != null)
                    {
                        var skeleton = skeletonScene.Instantiate<MobaMinion>();
                        caster.GetTree().CurrentScene.AddChild(skeleton);
                        skeleton.GlobalPosition = spawnPos;

                        var ai = skeleton.GetNodeOrNull<SummonedSkeletonAI>("SummonedSkeletonAI")
                               ?? new SummonedSkeletonAI { Name = "SummonedSkeletonAI" };
                        if (ai.GetParent() == null) skeleton.AddChild(ai);

                        ai.Caster = caster;
                        skeleton.Team = caster.Team;
                    }
                }
                break;

            case 3: // Lotus Trap (Ability 4)
                {
                    // 1. Mana check
                    var archery = caster.GetNodeOrNull<ArcherySystem>("ArcherySystem");
                    var stats = archery?.PlayerStats;
                    if (stats != null && stats.CurrentMana < 30)
                    {
                        GD.Print("[NecromancerAbilities] Not enough mana for Lotus Trap!");
                        return;
                    }
                    if (stats != null) stats.CurrentMana -= 30;

                    // 2. Animation
                    modelMgr?.PlayAnimation("Standing 1H Magic Attack 01.fbx");

                    // 3. Spawn Trap
                    var trapScene = GD.Load<PackedScene>("res://Scenes/VFX/LotusTrap.tscn");
                    if (trapScene != null)
                    {
                        var trap = trapScene.Instantiate<Node3D>();
                        caster.GetTree().CurrentScene.AddChild(trap);

                        // Prioritize target for placement, else ground point
                        Node3D target = caster.CurrentTarget;
                        Vector3 spawnPos = target != null ? target.GlobalPosition : TargetingHelper.GetGroundPoint(caster, 12.0f);
                        trap.GlobalPosition = spawnPos;

                        if (trap is LotusTrap lt)
                        {
                            lt.CasterTeam = caster.Team;
                            lt.Caster = caster;
                        }
                    }
                }
                break;
        }
    }

    private static void PerformCorpseExplosionDamage(PlayerController caster, Vector3 position, float radius, float damage)
    {
        var spaceState = caster.GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = new SphereShape3D { Radius = radius };
        query.Transform = new Transform3D(Basis.Identity, position);
        query.CollisionMask = 1 | 2;

        var results = spaceState.IntersectShape(query);
        foreach (var result in results)
        {
            var collider = (Node)result["collider"];
            if (collider is Monsters m) m.OnHit(damage, position, Vector3.Up, caster);
            else if (collider is PlayerController pc && pc != caster) pc.OnHit(damage, position, Vector3.Up, caster);
        }
    }
}
