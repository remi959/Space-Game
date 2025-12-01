using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Provides methods to find and sample surface points on procedural terrain.
/// Supports both density-based and raycast-based surface detection.
/// </summary>
public class SurfaceSampler : MonoBehaviour
{
    [SerializeField] private DensityProvider densityProvider;
    [SerializeField] private BiomeManager biomeManager;

    public DensityProvider DensityProvider => densityProvider;
    public BiomeManager BiomeManager => biomeManager;

    public void Initialize(DensityProvider density, BiomeManager biomes)
    {
        Debug.Log("[SurfaceSampler] Initialized.");
        densityProvider = density;
        biomeManager = biomes;
    }

    /// <summary>
    /// Gets surface point using density-based binary search (no colliders needed).
    /// </summary>
    public SurfacePoint GetSurfaceAt(Vector3 direction)
    {
        if (densityProvider == null)
        {
            Debug.LogWarning("[SurfaceSampler] No density provider set!");
            return SurfacePoint.Invalid;
        }

        Vector3 planetCenter = densityProvider.Center;
        float radius = densityProvider.Radius;
        float maxHeight = densityProvider.MaxTerrainHeight;

        float minDist = radius - maxHeight;
        float maxDist = radius + maxHeight;

        Vector3 surfacePos = Vector3.zero;
        bool found = false;

        for (int i = 0; i < 32; i++)
        {
            float midDist = (minDist + maxDist) * 0.5f;
            Vector3 testPos = planetCenter + direction.normalized * midDist;
            float density = densityProvider.GetDensityAt(testPos);

            if (Mathf.Abs(density) < 0.1f)
            {
                surfacePos = testPos;
                found = true;
                break;
            }

            if (density > 0)
                minDist = midDist;
            else
                maxDist = midDist;
        }

        if (!found)
        {
            float midDist = (minDist + maxDist) * 0.5f;
            surfacePos = planetCenter + direction.normalized * midDist;
        }

        Vector3 normal = CalculateNormalFromDensity(surfacePos);
        
        // REPLACED: Manual height calculation -> PlanetMath.GetHeightAboveSurface
        float height = PlanetMath.GetHeightAboveSurface(surfacePos, planetCenter, radius);
        
        // REPLACED: Manual slope calculation -> PlanetMath.CalculateSlope
        float slope = PlanetMath.CalculateSlope(surfacePos, normal, planetCenter);

        Biome biome = null;
        if (biomeManager != null)
        {
            var weights = biomeManager.GetBiomesAt(direction.normalized, densityProvider.Seed);
            if (weights.Length > 0) biome = weights[0].biome;
        }

        return new SurfacePoint
        {
            position = surfacePos,
            normal = normal,
            slope = slope,
            height = height,
            biome = biome,
            isValid = true
        };
    }

    /// <summary>
    /// Gets surface point using raycast (requires colliders).
    /// </summary>
    public SurfacePoint GetSurfaceAtRaycast(Vector3 worldPosition, LayerMask terrainLayer, float raycastHeight = 50f)
    {
        if (densityProvider == null)
        {
            Debug.LogWarning("[SurfaceSampler] No density provider set!");
            return SurfacePoint.Invalid;
        }

        SurfacePoint result = PlanetRaycast.FindSurface(
            worldPosition,
            densityProvider.Center,
            densityProvider.Radius,
            terrainLayer,
            raycastHeight
        );

        // Add biome info
        if (result.isValid && biomeManager != null)
        {
            Vector3 normalizedPos = PlanetMath.GetUpDirection(result.position, densityProvider.Center);
            var weights = biomeManager.GetBiomesAt(normalizedPos, densityProvider.Seed);
            if (weights.Length > 0) result.biome = weights[0].biome;
        }

        return result;
    }

    private Vector3 CalculateNormalFromDensity(Vector3 worldPos)
    {
        const float epsilon = 0.1f;

        float dx = densityProvider.GetDensityAt(worldPos + Vector3.right * epsilon)
                 - densityProvider.GetDensityAt(worldPos - Vector3.right * epsilon);
        float dy = densityProvider.GetDensityAt(worldPos + Vector3.up * epsilon)
                 - densityProvider.GetDensityAt(worldPos - Vector3.up * epsilon);
        float dz = densityProvider.GetDensityAt(worldPos + Vector3.forward * epsilon)
                 - densityProvider.GetDensityAt(worldPos - Vector3.forward * epsilon);

        return new Vector3(-dx, -dy, -dz).normalized;
    }

    // =========================================================================
    // STATIC UTILITIES
    // =========================================================================

    /// <summary>
    /// Static raycast-based surface detection.
    /// </summary>
    public static SurfacePoint RaycastSurface(
        Vector3 rayStart,
        Vector3 rayDirection,
        float rayLength,
        LayerMask terrainLayer,
        Vector3? planetCenter = null,
        float planetRadius = 0f,
        BiomeManager biomeManager = null,
        int seed = 0)
    {
        if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, rayLength, terrainLayer))
        {
            // REPLACED: Manual up/height/slope calculations -> PlanetMath methods
            Vector3 upDir = planetCenter.HasValue
                ? PlanetMath.GetUpDirection(hit.point, planetCenter.Value)
                : Vector3.up;

            float height = planetCenter.HasValue
                ? PlanetMath.GetHeightAboveSurface(hit.point, planetCenter.Value, planetRadius)
                : hit.point.y;

            float slope = planetCenter.HasValue
                ? PlanetMath.CalculateSlope(hit.point, hit.normal, planetCenter.Value)
                : Vector3.Angle(hit.normal, Vector3.up);

            Biome biome = null;
            if (biomeManager != null && planetCenter.HasValue)
            {
                Vector3 normalizedPos = upDir;
                var weights = biomeManager.GetBiomesAt(normalizedPos, seed);
                if (weights.Length > 0) biome = weights[0].biome;
            }

            return new SurfacePoint
            {
                position = hit.point,
                normal = hit.normal,
                slope = slope,
                height = height,
                biome = biome,
                isValid = true
            };
        }

        return SurfacePoint.Invalid;
    }

    /// <summary>
    /// Static utility for collecting surface points from a mesh.
    /// </summary>
    public static void CollectSurfacePointsFromMesh(
        Mesh mesh,
        Transform meshTransform,
        Vector3 planetCenter,
        float planetRadius,
        BiomeManager biomeManager,
        int seed,
        int targetCount,
        List<SurfacePoint> results,
        float minNormalDot = 0.3f,
        float minHeight = -5f)
    {
        results.Clear();

        if (mesh == null || mesh.vertexCount == 0) return;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        int step = Mathf.Max(1, vertices.Length / targetCount);

        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 worldNormal = meshTransform.TransformDirection(normals[i]).normalized;
            
            Vector3 toSurface = PlanetMath.GetUpDirection(worldPos, planetCenter);

            float normalDot = Vector3.Dot(worldNormal, toSurface);
            if (normalDot < minNormalDot) continue;

            float height = PlanetMath.GetHeightAboveSurface(worldPos, planetCenter, planetRadius);
            if (height < minHeight) continue;

            Biome biome = null;
            if (biomeManager != null)
            {
                var weights = biomeManager.GetBiomesAt(toSurface, seed);
                if (weights.Length > 0) biome = weights[0].biome;
            }

            results.Add(new SurfacePoint
            {
                position = worldPos,
                normal = worldNormal,
                slope = Vector3.Angle(worldNormal, toSurface),
                height = height,
                biome = biome,
                isValid = true
            });

            if (results.Count >= targetCount) break;
        }
    }
}