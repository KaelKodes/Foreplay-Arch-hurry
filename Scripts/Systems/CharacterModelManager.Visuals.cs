using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager : Node
{
    private Tween _glowTween;

    /// <summary>
    /// Applies a visual glow to the character's active meshes.
    /// </summary>
    public void ApplyGlow(Color color, float duration)
    {
        if (_glowTween != null) _glowTween.Kill();
        _glowTween = CreateTween();

        List<MeshInstance3D> meshes = new();
        if (_meleeModel != null) FindMeshes(_meleeModel, meshes);
        if (_archeryModel != null) FindMeshes(_archeryModel, meshes);
        if (_currentCustomModel != null) FindMeshes(_currentCustomModel, meshes);

        foreach (var mesh in meshes)
        {
            // Use MaterialOverride for the glow effect if it's not already overridden
            // Or better, modulate the material if it's unique
            var mat = mesh.GetActiveMaterial(0) as StandardMaterial3D;
            if (mat != null)
            {
                var uniqueMat = (StandardMaterial3D)mat.Duplicate();
                uniqueMat.EmissionEnabled = true;
                uniqueMat.Emission = color;
                uniqueMat.EmissionEnergyMultiplier = 0f;
                mesh.MaterialOverride = uniqueMat;

                _glowTween.Parallel().TweenProperty(uniqueMat, "emission_energy_multiplier", 2.0f, 0.5f);
            }
        }

        _glowTween.Chain().TweenInterval(duration - 1.0f);

        // Fade out
        foreach (var mesh in meshes)
        {
            if (mesh.MaterialOverride is StandardMaterial3D sm)
            {
                _glowTween.Parallel().TweenProperty(sm, "emission_energy_multiplier", 0f, 0.5f);
            }
        }

        _glowTween.Chain().TweenCallback(Callable.From(() =>
        {
            foreach (var mesh in meshes) mesh.MaterialOverride = null;
        }));
    }

    private void FindMeshes(Node node, List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (Node child in node.GetChildren()) FindMeshes(child, meshes);
    }
}
