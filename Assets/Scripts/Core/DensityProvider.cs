using UnityEngine;

/// <summary>
/// Single source of truth for terrain density calculations.
/// Combines base sphere, noise layers, biomes, and caves.
/// </summary>
public class DensityProvider : MonoBehaviour
{
    [Header("Planet Shape")]
    [SerializeField] private float planetRadius = 50f;
    [SerializeField] private float surfaceBlendDistance = 40f;
    [SerializeField] private float maxInteriorDensity = 10f;

    [Header("Terrain Layers")]
    [SerializeField] private NoiseLayer[] noiseLayers = new NoiseLayer[] { };

    [Header("References")]
    [SerializeField] private BiomeManager biomeManager;
    [SerializeField] private CaveGenerator caveGenerator;

    private int seed;
    private Vector3 planetCenter;
    private float maxTerrainHeight;

    public float Radius => planetRadius;
    public Vector3 Center => planetCenter;
    public float MaxTerrainHeight => maxTerrainHeight;
    public int Seed => seed;
    public BiomeManager BiomeManager => biomeManager;
    public CaveGenerator CaveGenerator => caveGenerator;

    public void Initialize(Vector3 center, int seed)
    {
        this.seed = seed;
        planetCenter = center;
        maxTerrainHeight = CalculateMaxTerrainHeight();

        Debug.Log($"[DensityProvider] Initialized with radius={planetRadius}, maxHeight={maxTerrainHeight}");
    }

    public float GetDensityAt(Vector3 worldPos)
    {
        Vector3 toPoint = worldPos - planetCenter;
        float distanceFromCenter = toPoint.magnitude;
        float baseDensity = planetRadius - distanceFromCenter;
        float surfaceBlend = Mathf.Clamp01(1f - Mathf.Abs(baseDensity) / surfaceBlendDistance);

        Vector3 normalizedPos = toPoint.normalized;
        float totalNoise = CalculateTerrainNoise(normalizedPos);
        float terrainDensity = baseDensity + totalNoise * surfaceBlend;

        float caveDensity = 0f;
        if (caveGenerator != null) caveDensity = caveGenerator.GetCaveDensity(worldPos, planetCenter, planetRadius, seed);

        if (caveDensity < 0f && terrainDensity > maxInteriorDensity) terrainDensity = maxInteriorDensity;

        return terrainDensity + caveDensity;
    }

    private float CalculateTerrainNoise(Vector3 normalizedPos)
    {
        if (biomeManager != null) return CalculateBiomeNoise(normalizedPos);

        float totalNoise = 0f;

        foreach (var layer in noiseLayers)
            if (layer.enabled) totalNoise += layer.Evaluate(normalizedPos * planetRadius, seed, 0f);

        return totalNoise;
    }

    private float CalculateBiomeNoise(Vector3 normalizedPos)
    {
        BiomeWeight[] biomeWeights = biomeManager.GetBiomesAt(normalizedPos, seed);

        float totalNoise = 0f;
        float totalWeight = 0f;

        foreach (var bw in biomeWeights)
        {
            if (bw.biome == null) continue;

            float biomeNoise = 0f;
            if (bw.biome.terrainLayers != null)
                foreach (var layer in bw.biome.terrainLayers)
                    biomeNoise += layer.Evaluate(normalizedPos * planetRadius, seed, 0f);

            biomeNoise *= bw.biome.heightMultiplier;
            biomeNoise += bw.biome.heightOffset;

            totalNoise += biomeNoise * bw.weight;
            totalWeight += bw.weight;
        }

        return totalWeight > 0f ? totalNoise / totalWeight : 0f;
    }

    private float CalculateMaxTerrainHeight()
    {
        float globalLayersMax = 0f;

        foreach (var layer in noiseLayers)
            if (layer.enabled) globalLayersMax += layer.settings.strength;

        float biomesMax = 0f;
        if (biomeManager != null && biomeManager.biomes != null)
        {
            foreach (var biome in biomeManager.biomes)
            {
                if (biome == null || biome.terrainLayers == null) continue;

                float biomeLayersTotal = 0f;
                foreach (var layer in biome.terrainLayers)
                    if (layer.enabled) biomeLayersTotal += layer.settings.strength;

                float biomeMax = biomeLayersTotal * biome.heightMultiplier + Mathf.Abs(biome.heightOffset);
                biomesMax = Mathf.Max(biomesMax, biomeMax);
            }
        }

        return Mathf.Max(globalLayersMax, biomesMax);
    }
}