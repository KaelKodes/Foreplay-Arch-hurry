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
    /// Find the nearest golf cart within range.
    /// </summary>
    public static GolfCart FindNearestCart(SceneTree tree, Vector3 playerPos, float maxDistance)
    {
        GolfCart nearestCart = null;
        float minDist = maxDistance;

        foreach (Node node in tree.GetNodesInGroup("carts"))
        {
            if (node is GolfCart cart && !cart.IsBeingDriven)
            {
                float d = playerPos.DistanceTo(cart.GlobalPosition);
                if (d < minDist)
                {
                    minDist = d;
                    nearestCart = cart;
                }
            }
        }
        return nearestCart;
    }
}
