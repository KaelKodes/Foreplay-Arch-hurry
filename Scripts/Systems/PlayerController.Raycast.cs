using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private InteractableObject CheckInteractableRaycast()
    {
        Node3D hit = CheckUnitRaycast();
        return hit as InteractableObject;
    }

    private Node3D CheckUnitRaycast()
    {
        if (_camera == null) return null;

        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var to = from + _camera.ProjectRayNormal(mousePos) * 100.0f;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        // Exclude the caster
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = (Node)result["collider"];

            // 1. Check for InteractableObject
            var interactable = collider.GetNodeOrNull<InteractableObject>(".")
                             ?? collider.GetParentOrNull<InteractableObject>();
            if (interactable == null && collider.GetParent() != null)
                interactable = collider.GetParent().GetParentOrNull<InteractableObject>();

            if (interactable != null) return interactable;

            // 2. Check for other targetables (Remote Players + Bots)
            // Walk up the tree to find PlayerController (collider could be child shape)
            Node current = collider;
            for (int i = 0; i < 4 && current != null; i++)
            {
                if (current is PlayerController pc && pc != this) return pc;
                current = current.GetParent();
            }
        }
        return null;
    }
}
