using Godot;
using System.Collections.Generic;
using Archery;

namespace Archery;

/// <summary>
/// Static helper class for targeting and trajectory calculations.
/// Extracted from ArcherySystem to reduce file size.
/// </summary>
public static class TargetingHelper
{
    /// <summary>
    /// Find all targetable objects in the scene recursively.
    /// </summary>
    public static void FindTargetablesRecursive(Node node, List<Node3D> results, MobaTeam attackerTeam = MobaTeam.None, bool alliesOnly = false)
    {
        if (node is Node3D targetNode)
        {
            bool isTargetable = false;
            MobaTeam targetTeam = MobaTeam.None;

            if (targetNode is InteractableObject io)
            {
                if (io.IsTargetable)
                {
                    isTargetable = true;
                    if (io is MobaMinion minion) targetTeam = minion.Team;
                    else if (io is Monsters monster) targetTeam = MobaTeam.None; // Neutral
                }
            }
            else if (targetNode is MobaTower tower)
            {
                isTargetable = true;
                targetTeam = tower.Team;
            }
            else if (targetNode is MobaNexus nexus)
            {
                isTargetable = true;
                targetTeam = nexus.Team;
            }
            else if (targetNode is PlayerController pc && !pc.IsLocal)
            {
                isTargetable = true;
                targetTeam = pc.Team;
            }

            if (isTargetable && !IsTargetDead(targetNode))
            {
                bool isEnemy = TeamSystem.AreEnemies(attackerTeam, targetTeam);

                if (alliesOnly)
                {
                    if (!isEnemy && attackerTeam != MobaTeam.None && targetTeam != MobaTeam.None)
                        results.Add(targetNode);
                }
                else
                {
                    if (isEnemy || attackerTeam == MobaTeam.None || targetTeam == MobaTeam.None)
                        results.Add(targetNode);
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            FindTargetablesRecursive(child, results, attackerTeam, alliesOnly);
        }
    }

    /// <summary>
    /// Calculate optimal loft angle for projectile to hit a target.
    /// </summary>
    public static float CalculateOptimalLoft(Vector3 start, Vector3 target, float velocity, float gravity)
    {
        Vector3 diff = target - start;
        float y = diff.Y;
        float x = new Vector2(diff.X, diff.Z).Length();
        float v = velocity;
        float g = gravity;

        float v2 = v * v;
        float v4 = v2 * v2;
        float root = v4 - g * (g * x * x + 2 * y * v2);

        if (root < 0)
        {
            return 45.0f; // Out of range
        }

        float angle1 = Mathf.Atan((v2 + Mathf.Sqrt(root)) / (g * x));
        float angle2 = Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * x));

        float deg1 = Mathf.RadToDeg(angle1);
        float deg2 = Mathf.RadToDeg(angle2);

        if (deg2 >= -5.0f && deg2 <= 45.0f) return deg2;
        if (deg1 >= -5.0f && deg1 <= 45.0f) return deg1;

        return 12.0f; // Fallback
    }

    /// <summary>
    /// Gets player color by index (8-color palette).
    /// </summary>
    public static Color GetPlayerColor(int playerIndex)
    {
        Color c = Colors.DodgerBlue;
        switch (playerIndex % 8)
        {
            case 0: c = Colors.DodgerBlue; break;
            case 1: c = Colors.Crimson; break;
            case 2: c = Colors.DarkOrchid; break;
            case 3: c = Colors.Gold; break;
            case 4: c = Colors.OrangeRed; break;
            case 5: c = Colors.Cyan; break;
            case 6: c = Colors.DeepPink; break;
            case 7: c = Colors.Teal; break;
        }
        return c;
    }

    /// <summary>
    /// Checks if a target node is dead or destroyed.
    /// </summary>
    public static bool IsTargetDead(Node3D target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target)) return true;

        if (target is Monsters monster) return monster.Health <= 0;
        if (target is MobaTower tower) return tower.IsDestroyed;
        if (target is MobaNexus nexus) return nexus.IsDestroyed;

        // Check for common Health or IsDead properties via Duck Typing if needed, 
        // but for now we stick to known types.
        return false;
    }

    /// <summary>
    /// Risk of Rain 2 Style "Fluid" targeting.
    /// Finds the best target based on proximity to the crosshair (screen center).
    /// </summary>
    public static Node3D GetFluidTarget(Node3D caster, Viewport viewport, float maxDistance = 50.0f, float coneAngleDegrees = 15.0f, MobaTeam attackerTeam = MobaTeam.None, bool alliesOnly = false)
    {
        var camera = viewport.GetCamera3D();
        if (camera == null) return null;

        List<Node3D> potentialTargets = new List<Node3D>();
        // Filter by team: enemies by default, allies when Ctrl held
        FindTargetablesRecursive(caster.GetTree().Root, potentialTargets, attackerTeam, alliesOnly);

        Node3D bestTarget = null;
        float bestScore = float.MaxValue; // Lower is better

        Vector3 camPos = camera.GlobalPosition;
        Vector3 camForward = -camera.GlobalBasis.Z;

        foreach (var target in potentialTargets)
        {
            if (target == caster) continue;

            Vector3 toTarget = target.GlobalPosition - camPos;
            float dist = toTarget.Length();
            if (dist > maxDistance) continue;

            Vector3 dirToTarget = toTarget.Normalized();
            float dot = camForward.Dot(dirToTarget);
            float angle = Mathf.RadToDeg(Mathf.Acos(dot));

            if (angle > coneAngleDegrees) continue;

            var spaceState = caster.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(camPos, target.GlobalPosition);
            if (caster is CollisionObject3D co)
            {
                query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
            }
            var result = spaceState.IntersectRay(query);

            if (result.Count > 0)
            {
                var hitCollider = result["collider"].As<Node>();
                if (hitCollider != target && !target.IsAncestorOf(hitCollider) && !hitCollider.IsAncestorOf(target))
                {
                    continue; // Blocked by environment
                }
            }

            // Scoring: Combination of Angle (high weight) and Distance (low weight)
            // RoR2 prioritizes what you are looking at.
            float score = (angle * 2.0f) + (dist * 0.1f);

            // HERO PRIORITY: Bonus for player heroes
            if (target is PlayerController) score -= 20.0f;

            // DIRECT HIT PRIORITY: Precision bonus for aiming directly at a target in a cluster
            if (angle < 2.5f) score -= 50.0f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Tab-targeting logic for cycling through nearby targets.
    /// </summary>
    public static Node3D GetNextTabTarget(Node3D caster, Node3D currentTarget, bool alliesOnly = false, float maxDistance = 40.0f, MobaTeam attackerTeam = MobaTeam.None)
    {
        List<Node3D> potentialTargets = new List<Node3D>();

        MobaTeam team = attackerTeam;
        if (team == MobaTeam.None && caster is PlayerController pc) team = pc.Team;

        FindTargetablesRecursive(caster.GetTree().Root, potentialTargets, team, alliesOnly);

        if (potentialTargets.Count == 0) return null;

        // Sort by Hero status first, then distance to player
        potentialTargets.Sort((a, b) =>
        {
            bool aIsHero = a is PlayerController;
            bool bIsHero = b is PlayerController;

            if (aIsHero && !bIsHero) return -1;
            if (!aIsHero && bIsHero) return 1;

            return caster.GlobalPosition.DistanceTo(a.GlobalPosition).CompareTo(caster.GlobalPosition.DistanceTo(b.GlobalPosition));
        });

        if (currentTarget == null) return potentialTargets[0];

        int currentIndex = potentialTargets.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % potentialTargets.Count;

        return potentialTargets[nextIndex];
    }

    /// <summary>
    /// Checks if a target is within the specified cone angle of the camera.
    /// </summary>
    public static bool IsTargetInCone(Node3D caster, Viewport viewport, Node3D target, float coneAngleDegrees = 15.0f)
    {
        if (target == null || !GodotObject.IsInstanceValid(target)) return false;
        var camera = viewport.GetCamera3D();
        if (camera == null) return false;

        Vector3 camPos = camera.GlobalPosition;
        Vector3 camForward = -camera.GlobalBasis.Z;

        Vector3 toTarget = target.GlobalPosition - camPos;
        Vector3 dirToTarget = toTarget.Normalized();
        float dot = camForward.Dot(dirToTarget);
        float angle = Mathf.RadToDeg(Mathf.Acos(dot));

        if (angle > coneAngleDegrees) return false;

        // Also check line of sight
        var spaceState = caster.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(camPos, target.GlobalPosition);
        if (caster is CollisionObject3D co)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
        }
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitCollider = result["collider"].As<Node>();
            if (hitCollider != target && !target.IsAncestorOf(hitCollider) && !hitCollider.IsAncestorOf(target))
            {
                return false; // Blocked by environment
            }
        }

        return true;
    }

    /// <summary>
    /// Executes an action on all enemies/targets within a radius.
    /// </summary>
    public static void PerformAoEAction(Node3D caster, Vector3 center, float radius, System.Action<Node3D> action, MobaTeam attackerTeam = MobaTeam.None)
    {
        var spaceState = caster.GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = new SphereShape3D { Radius = radius };
        query.Transform = new Transform3D(Basis.Identity, center);
        query.CollisionMask = 1 | 2; // Targetables and Monsters
        if (caster is CollisionObject3D co)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
        }

        var results = spaceState.IntersectShape(query);
        foreach (var result in results)
        {
            var collider = (Node)result["collider"];
            var target = collider as Node3D;
            if (target == null) target = collider.GetParent() as Node3D;
            if (target == null) continue;

            MobaTeam targetTeam = MobaTeam.None;
            if (target is InteractableObject io) targetTeam = io.Team;
            else if (target is MobaTower tower) targetTeam = tower.Team;
            else if (target is MobaNexus nexus) targetTeam = nexus.Team;

            if (TeamSystem.AreEnemies(attackerTeam, targetTeam) || targetTeam == MobaTeam.None)
            {
                action?.Invoke(target);
            }
        }
    }
}
