using UnityEngine;

/// <summary>
/// Static utilities for spherical planet calculations.
/// Eliminates duplicate math across CaveGenerator, SurfaceSampler, VegetationSpawner, etc.
/// </summary>
public static class PlanetMath
{
    /// <summary>
    /// Gets the "up" direction at a world position (away from planet center).
    /// </summary>
    public static Vector3 GetUpDirection(Vector3 worldPos, Vector3 planetCenter)
    {
        return (worldPos - planetCenter).normalized;
    }

    /// <summary>
    /// Gets distance from planet center.
    /// </summary>
    public static float GetDistanceFromCenter(Vector3 worldPos, Vector3 planetCenter)
    {
        return Vector3.Distance(worldPos, planetCenter);
    }

    /// <summary>
    /// Calculates depth below the planet's base radius.
    /// </summary>
    public static float GetDepthBelowSurface(Vector3 worldPos, Vector3 planetCenter, float planetRadius)
    {
        return planetRadius - GetDistanceFromCenter(worldPos, planetCenter);
    }

    /// <summary>
    /// Calculates height above the planet's base radius.
    /// </summary>
    public static float GetHeightAboveSurface(Vector3 worldPos, Vector3 planetCenter, float planetRadius)
    {
        return GetDistanceFromCenter(worldPos, planetCenter) - planetRadius;
    }

    /// <summary>
    /// Gets a random point on the surface tangent plane at a position.
    /// Used by VegetationSpawner and StructurePlacer for jitter/random placement.
    /// </summary>
    public static Vector3 GetRandomTangentOffset(Vector3 position, Vector3 planetCenter, float maxRadius)
    {
        Vector3 up = GetUpDirection(position, planetCenter);
        Vector3 randomDir = Random.onUnitSphere;
        Vector3 tangent = Vector3.ProjectOnPlane(randomDir, up).normalized;
        return tangent * Random.Range(0f, maxRadius);
    }

    /// <summary>
    /// Calculates slope angle relative to planet surface.
    /// </summary>
    public static float CalculateSlope(Vector3 position, Vector3 normal, Vector3 planetCenter)
    {
        Vector3 radialUp = GetUpDirection(position, planetCenter);
        return Vector3.Angle(normal, radialUp);
    }

    /// <summary>
    /// Projects a direction onto the tangent plane at a position.
    /// </summary>
    public static Vector3 ProjectOnTangentPlane(Vector3 direction, Vector3 position, Vector3 planetCenter)
    {
        Vector3 up = GetUpDirection(position, planetCenter);
        return Vector3.ProjectOnPlane(direction, up).normalized;
    }

    public static float Smoothstep(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }
}