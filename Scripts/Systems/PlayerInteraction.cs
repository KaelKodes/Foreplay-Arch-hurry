using Godot;
using Archery;

namespace Archery;

/// <summary>
/// Static helper class for player interaction and vehicle handling.
/// Extracted from PlayerController to reduce file size.
/// </summary>
public static class PlayerInteraction
{
    /// <summary>
    /// Find the nearest collectible arrow within range.
    /// </summary>
    public static ArrowController FindNearestCollectibleArrow(Node root, Vector3 playerPos, float maxDistance)
    {
        ArrowController nearest = null;
        float minDist = maxDistance;

        foreach (Node node in root.GetTree().GetNodesInGroup("arrows"))
        {
            if (node is ArrowController ac && ac.IsCollectible)
            {
                float dist = playerPos.DistanceTo(ac.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = ac;
                }
            }
        }
        return nearest;
    }

    /// <summary>
    /// Check for interactable objects using a forward raycast from camera.
    /// </summary>
    public static Node CheckInteractionForwardRaycast(Camera3D camera, Vector3 playerPos, float maxDistance)
    {
        if (camera == null) return null;

        Vector3 from = camera.GlobalPosition;
        Vector3 to = from + (-camera.GlobalBasis.Z) * maxDistance;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            return (Node)result["collider"];
        }
        return null;
    }


    /// <summary>
    /// Check for interactable objects using a forward raycast from the player's body.
    /// </summary>
    public static Node CheckInteractionForwardRaycast(CharacterBody3D player)
    {
        var spaceState = player.GetWorld3D().DirectSpaceState;
        var from = player.GlobalPosition + new Vector3(0, 1.5f, 0);
        var to = from + (-player.GlobalTransform.Basis.Z) * 3.0f;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 3;
        query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var hitNode = (Node)result["collider"];
            Node n = hitNode;
            while (n != null)
            {
                if (n is InteractableObject io) return io;
                if (n is ArrowController ac) return ac;
                n = n.GetParent();
            }
        }
        return null;
    }

    /// <summary>
    /// Handle proximity prompts for interaction.
    /// </summary>
    public static void HandleProximityPrompts(PlayerController player, ArcherySystem archerySystem)
    {
        if (archerySystem == null) return;

        // 1. Proximity Check for Arrows
        ArrowController nearestArrow = FindNearestCollectibleArrow(player, player.GlobalPosition, 3.0f);
        if (nearestArrow != null && nearestArrow.IsCollectible)
        {
            string prompt = nearestArrow.GetInteractionPrompt();
            if (!string.IsNullOrEmpty(prompt))
            {
                archerySystem.SetPrompt(true, prompt);
                if (Input.IsKeyPressed(Key.E))
                {
                    nearestArrow.OnInteract(player);
                }
                return;
            }
        }

        // 2. Generic Interaction Check
        Node hitNode = CheckInteractionForwardRaycast(player);
        if (hitNode != null)
        {
            float dist = player.GlobalPosition.DistanceTo((hitNode is Node3D n3d ? n3d.GlobalPosition : player.GlobalPosition));
            if (dist < 5.0f)
            {
                string prompt = (hitNode is InteractableObject io) ? io.GetInteractionPrompt() : ((hitNode is ArrowController ac) ? ac.GetInteractionPrompt() : "");
                if (!string.IsNullOrEmpty(prompt))
                {
                    archerySystem.SetPrompt(true, prompt);
                    if (Input.IsKeyPressed(Key.E))
                    {
                        if (hitNode is InteractableObject io2) io2.OnInteract(player); else if (hitNode is ArrowController ac2) ac2.OnInteract(player);
                    }
                    return;
                }
            }
        }

        archerySystem.SetPrompt(false);
    }
}
