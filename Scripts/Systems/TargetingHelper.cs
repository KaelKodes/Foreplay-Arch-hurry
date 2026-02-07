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

            if (isTargetable)
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
}
