using Godot;
using System;

namespace Archery;

public partial class Monsters
{
    public virtual void ApplyMovement(Vector3 velocity, float delta)
    {
        Vector3 newPos = GlobalPosition;
        newPos.X += velocity.X * delta;
        newPos.Z += velocity.Z * delta;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(
            newPos + Vector3.Up * 2.0f,
            newPos + Vector3.Down * 5.0f
        );
        query.CollisionMask = 2; // Terrain layer

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            newPos.Y = ((Vector3)result["position"]).Y;
        }

        GlobalPosition = newPos;
    }
}
