using UnityEngine;

/// <summary>
/// Raycast utilities for spherical planets.
/// Consolidates duplicate raycast logic from SurfaceSampler, VegetationSpawner, StructurePlacer.
/// </summary>
public static class PlanetRaycast
{
    /// <summary>
    /// Raycasts toward planet center to find ground.
    /// </summary>
    public static bool RaycastTowardCenter(
        Vector3 worldPosition,
        Vector3 planetCenter,
        LayerMask groundLayer,
        float raycastHeight,
        out RaycastHit hit)
    {
        Vector3 up = PlanetMath.GetUpDirection(worldPosition, planetCenter);
        Vector3 rayStart = worldPosition + up * raycastHeight;
        Vector3 rayDir = -up;
        float rayLength = raycastHeight * 2f;

        return Physics.Raycast(rayStart, rayDir, out hit, rayLength, groundLayer);
    }

    /// <summary>
    /// Raycasts toward planet center with fallback attempts.
    /// Returns SurfacePoint for consistency.
    /// </summary>
    public static SurfacePoint FindSurface(
        Vector3 worldPosition,
        Vector3 planetCenter,
        float planetRadius,
        LayerMask groundLayer,
        float raycastHeight = 50f)
    {
        if (RaycastTowardCenter(worldPosition, planetCenter, groundLayer, raycastHeight, out RaycastHit hit))
        {
            return new SurfacePoint
            {
                position = hit.point,
                normal = hit.normal,
                slope = PlanetMath.CalculateSlope(hit.point, hit.normal, planetCenter),
                height = PlanetMath.GetHeightAboveSurface(hit.point, planetCenter, planetRadius),
                isValid = true
            };
        }

        return SurfacePoint.Invalid;
    }

    /// <summary>
    /// Finds surface with position jitter (for natural vegetation placement).
    /// </summary>
    public static bool FindSurfaceWithJitter(
        Vector3 basePosition,
        Vector3 baseNormal,
        Vector3 planetCenter,
        float planetRadius,
        float maxJitter,
        LayerMask groundLayer,
        float raycastHeight,
        out Vector3 snappedPosition,
        out Vector3 snappedNormal)
    {
        snappedPosition = basePosition;
        snappedNormal = baseNormal;

        if (maxJitter <= 0f)
            return true;

        Vector3 jitteredPosition = basePosition + PlanetMath.GetRandomTangentOffset(basePosition, planetCenter, maxJitter);

        if (RaycastTowardCenter(jitteredPosition, planetCenter, groundLayer, raycastHeight, out RaycastHit hit))
        {
            snappedPosition = hit.point;
            snappedNormal = hit.normal;
            return true;
        }

        return false;
    }
}