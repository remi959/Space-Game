using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Optimized cave generator with worm caves and random tunnel networks.
/// </summary>
[System.Serializable]
public class CaveGenerator : MonoBehaviour
{
    [Header("Cave Settings")]
    public bool enableCaves = true;

    [Range(0f, 1f)]
    public float caveDensity = 0.3f;

    [Header("Cave Colors")]
    public Color caveColor = new(0.4f, 0.35f, 0.3f);
    public Color deepCaveColor = new(0.25f, 0.2f, 0.18f);
    public float deepCaveDepth = 30f;

    [Header("Worm Caves (Noise-based Tunnels)")]
    public bool enableWormCaves = true;
    public NoiseGenerator.NoiseSettings wormNoise = new()
    {
        octaves = 2,
        scale = 20f,
        strength = 1f
    };

    [Range(0f, 1f)]
    public float wormThreshold = 0.7f;
    public float wormWidth = 3f;

    [Header("Depth Control")]
    public float minDepth = 5f;
    public float maxDepth = 40f;

    [Header("Debug")]
    public bool debugLogCaves = false;

    // Caching
    private Dictionary<long, float> wormNoiseCache = new();
    private const float NOISE_CACHE_RESOLUTION = 2f;
    private int cachedSeed = int.MinValue;
    private float cachedPlanetRadius;
    private Vector3 cachedPlanetCenter;

    public struct CaveColorInfo
    {
        public bool isInCave;
        public Color color;
        public float blendWeight;
    }

    // =========================================================================
    // MAIN ENTRY POINT
    // =========================================================================

    public float GetCaveDensity(Vector3 worldPos, Vector3 planetCenter, float planetRadius, int seed)
    {
        if (!enableCaves) return 0f;

        UpdateCacheIfNeeded(seed, planetRadius, planetCenter);

        float depthBelowSurface = CalculateDepthBelowSurface(worldPos, planetCenter, planetRadius);

        if (!IsWithinCaveDepthRange(depthBelowSurface)) return 0f;

        float depthFade = CalculateDepthFade(depthBelowSurface);
        float totalCaveDensity = 0f;

        if (enableWormCaves)
        {
            float wormDensity = GetWormCaveDensityFast(worldPos, depthFade, seed);
            totalCaveDensity = Mathf.Min(totalCaveDensity, wormDensity);
        }

        return totalCaveDensity;
    }

    public CaveColorInfo GetCaveColorInfo(Vector3 worldPos, Vector3 planetCenter, float planetRadius)
    {
        CaveColorInfo info = new() { isInCave = false, color = caveColor, blendWeight = 0f };

        if (!enableCaves) return info;

        float depthBelowSurface = PlanetMath.GetDepthBelowSurface(worldPos, planetCenter, planetRadius);

        if (depthBelowSurface >= minDepth)
        {
            info.isInCave = true;
            float depthFactor = Mathf.Clamp01((depthBelowSurface - minDepth) / (deepCaveDepth - minDepth));
            info.color = Color.Lerp(caveColor, deepCaveColor, depthFactor);
            info.blendWeight = Mathf.Clamp01(depthFactor);
        }

        return info;
    }

    // =========================================================================
    // CACHE MANAGEMENT
    // =========================================================================

    private void UpdateCacheIfNeeded(int seed, float planetRadius, Vector3 planetCenter)
    {
        if (seed != cachedSeed || planetRadius != cachedPlanetRadius || planetCenter != cachedPlanetCenter)
        {
            ClearCache();
            cachedSeed = seed;
            cachedPlanetRadius = planetRadius;
            cachedPlanetCenter = planetCenter;
        }
    }

    public void ClearCache()
    {
        wormNoiseCache.Clear();
        cachedSeed = int.MinValue;
    }

    // =========================================================================
    // DEPTH CALCULATION
    // =========================================================================

    private float CalculateDepthBelowSurface(Vector3 worldPos, Vector3 planetCenter, float planetRadius)
    {
        return PlanetMath.GetDepthBelowSurface(worldPos, planetCenter, planetRadius);
    }

    private bool IsWithinCaveDepthRange(float depthBelowSurface)
    {
        return depthBelowSurface >= minDepth && depthBelowSurface <= maxDepth;
    }

    private float CalculateDepthFade(float depthBelowSurface)
    {
        float fadeRange = 5f;
        float fade;

        if (depthBelowSurface < minDepth + fadeRange)
        {
            fade = (depthBelowSurface - minDepth) / fadeRange;
        }
        else if (depthBelowSurface > maxDepth - fadeRange)
        {
            fade = (maxDepth - depthBelowSurface) / fadeRange;
        }
        else
        {
            return 1f;
        }

        // Apply smoothstep for gradual transitions
        fade = Mathf.Clamp01(fade);
        return fade * fade * (3f - 2f * fade);
    }

    // =========================================================================
    // WORM CAVES
    // =========================================================================

    private float GetWormCaveDensityFast(Vector3 worldPos, float depthFade, int seed)
    {
        float wormValue = GetCachedWormNoise(worldPos, seed);

        if (wormValue > wormThreshold)
        {
            float caveStrength = (wormValue - wormThreshold) / (1f - wormThreshold);
            caveStrength *= caveDensity * depthFade;
            return -caveStrength * wormWidth;
        }

        return 0f;
    }

    private float GetCachedWormNoise(Vector3 worldPos, int seed)
    {
        // Get fractional position within cell for interpolation
        float fx = worldPos.x / NOISE_CACHE_RESOLUTION;
        float fy = worldPos.y / NOISE_CACHE_RESOLUTION;
        float fz = worldPos.z / NOISE_CACHE_RESOLUTION;

        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int z0 = Mathf.FloorToInt(fz);

        float tx = fx - x0;
        float ty = fy - y0;
        float tz = fz - z0;

        // Smoothstep the interpolation factors
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        tz = tz * tz * (3f - 2f * tz);

        // Sample 8 corners and trilinearly interpolate
        float c000 = GetNoiseAtCell(x0, y0, z0, seed);
        float c100 = GetNoiseAtCell(x0 + 1, y0, z0, seed);
        float c010 = GetNoiseAtCell(x0, y0 + 1, z0, seed);
        float c110 = GetNoiseAtCell(x0 + 1, y0 + 1, z0, seed);
        float c001 = GetNoiseAtCell(x0, y0, z0 + 1, seed);
        float c101 = GetNoiseAtCell(x0 + 1, y0, z0 + 1, seed);
        float c011 = GetNoiseAtCell(x0, y0 + 1, z0 + 1, seed);
        float c111 = GetNoiseAtCell(x0 + 1, y0 + 1, z0 + 1, seed);

        // Trilinear interpolation
        float c00 = Mathf.Lerp(c000, c100, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);

        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);

        return Mathf.Lerp(c0, c1, tz);
    }

    private float GetNoiseAtCell(int x, int y, int z, int seed)
{
    long key = ((long)x << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);

    if (wormNoiseCache.TryGetValue(key, out float cached))
        return cached;

    Vector3 cellPos = new(
        x * NOISE_CACHE_RESOLUTION,
        y * NOISE_CACHE_RESOLUTION,
        z * NOISE_CACHE_RESOLUTION
    );

    float value = NoiseGenerator.Sample3D(cellPos, wormNoise, seed + 100);
    value = (value + 1f) * 0.5f;

    wormNoiseCache[key] = value;
    return value;
}
}