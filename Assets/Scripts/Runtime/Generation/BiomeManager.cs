using UnityEngine;

[System.Serializable]
public class BiomeManager : MonoBehaviour
{
    [Header("Biome Selection Noise")]
    public NoiseGenerator.NoiseSettings biomeNoise = new NoiseGenerator.NoiseSettings
    {
        octaves = 2,
        scale = 50f,
        strength = 1f,
        minValue = -1f
    };

    [Header("Available Biomes")]
    public Biome[] biomes;

    [Header("Blending")]
    [Tooltip("Width of blend zone at each boundary (0 = hard edges)")]
    [Range(0f, 0.15f)]
    public float blendWidth = 0.05f;

    [Header("Distribution")]
    [Tooltip("Biases distribution: <1 = favor middle biomes, 1 = even, >1 = favor edge biomes")]
    [Range(0.5f, 3f)]
    public float contrast = 1f;

    [Tooltip("Base sampling radius - increase for smaller biome regions")]
    public float sampleRadius = 100f;

    private Vector3 lastSamplePos;
    private int lastSeed;
    private BiomeWeight[] cachedWeights;

    /// <summary>
    /// Gets the biome(s) at a position with blend weights.
    /// </summary>
    public BiomeWeight[] GetBiomesAt(Vector3 normalizedPosition, int seed)
    {
        // Return cached if same position
        if (cachedWeights != null && lastSeed == seed &&
            (normalizedPosition - lastSamplePos).sqrMagnitude < 0.0001f)
        {
            return cachedWeights;
        }

        lastSamplePos = normalizedPosition;
        lastSeed = seed;

        if (biomes == null || biomes.Length == 0)
        {
            cachedWeights = new BiomeWeight[0];
            return cachedWeights;
        }

        if (biomes.Length == 1)
        {
            cachedWeights = new BiomeWeight[] { new() { biome = biomes[0], weight = 1f } };
            return cachedWeights;
        }

        Vector3 samplePoint = normalizedPosition * sampleRadius;

        float rawNoise = NoiseGenerator.Sample3D(
            samplePoint,
            biomeNoise,
            seed + 9999
        );

        float noiseValue = Mathf.Clamp01((rawNoise + 1f) * 0.5f);

        if (Mathf.Abs(contrast - 1f) > 0.01f) noiseValue = ApplyContrast(noiseValue, contrast);

        if (blendWidth <= 0.001f)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(noiseValue * biomes.Length), 0, biomes.Length - 1);
            cachedWeights = new BiomeWeight[] { new() { biome = biomes[index], weight = 1f } };
            return cachedWeights;
        }

        cachedWeights = CalculateBlendedWeights(noiseValue);
        return cachedWeights;
    }

    private float ApplyContrast(float value, float contrastAmount)
    {
        float centered = value - 0.5f;
        float sign = Mathf.Sign(centered);
        float magnitude = Mathf.Abs(centered) * 2f;
        float contrasted = Mathf.Pow(magnitude, 1f / contrastAmount);
        return Mathf.Clamp01(sign * contrasted * 0.5f + 0.5f);
    }

    /// <summary>
    /// Calculates smooth blend weights based on distance to biome boundaries.
    /// Fixed: Uses 0.5 to 1.0 range so boundaries are 50/50, not 0/100.
    /// </summary>
    private BiomeWeight[] CalculateBlendedWeights(float noiseValue)
    {
        int biomeCount = biomes.Length;
        float biomeWidth = 1f / biomeCount;

        int primaryIndex = Mathf.FloorToInt(noiseValue * biomeCount);
        primaryIndex = Mathf.Clamp(primaryIndex, 0, biomeCount - 1);

        float biomeStart = primaryIndex * biomeWidth;
        float biomeEnd = biomeStart + biomeWidth;

        float distToLower = noiseValue - biomeStart;
        float distToUpper = biomeEnd - noiseValue;

        // Near lower boundary - blend with previous biome
        if (distToLower < blendWidth && primaryIndex > 0)
        {
            float t = distToLower / blendWidth;  // 0 at boundary, 1 at blend edge

            float primaryWeight = 0.5f + 0.5f * Smoothstep(t);

            return new BiomeWeight[]
            {
                new() { biome = biomes[primaryIndex], weight = primaryWeight },
                new() { biome = biomes[primaryIndex - 1], weight = 1f - primaryWeight }
            };
        }

        // Near upper boundary - blend with next biome
        if (distToUpper < blendWidth && primaryIndex < biomeCount - 1)
        {
            float t = distToUpper / blendWidth;  // 0 at boundary, 1 at blend edge

            float primaryWeight = 0.5f + 0.5f * Smoothstep(t);

            return new BiomeWeight[]
            {
                new() { biome = biomes[primaryIndex], weight = primaryWeight },
                new() { biome = biomes[primaryIndex + 1], weight = 1f - primaryWeight }
            };
        }

        return new BiomeWeight[]
        {
            new() { biome = biomes[primaryIndex], weight = 1f }
        };
    }

    public BiomeBlend GetBiomeBlendAt(Vector3 normalizedPosition, int seed)
    {
        if (biomes == null || biomes.Length == 0)
            return new BiomeBlend { primary = null, secondary = null, blendFactor = 0f };

        Vector3 samplePoint = normalizedPosition * sampleRadius;

        float rawNoise = NoiseGenerator.Sample3D(
            samplePoint,
            biomeNoise,
            seed + 9999
        );

        float noiseValue = Mathf.Clamp01((rawNoise + 1f) * 0.5f);

        if (Mathf.Abs(contrast - 1f) > 0.01f)
        {
            noiseValue = ApplyContrast(noiseValue, contrast);
        }

        int biomeCount = biomes.Length;
        float biomeWidth = 1f / biomeCount;

        int primaryIndex = Mathf.Clamp(Mathf.FloorToInt(noiseValue * biomeCount), 0, biomeCount - 1);

        float biomeStart = primaryIndex * biomeWidth;
        float biomeEnd = biomeStart + biomeWidth;

        float distToLower = noiseValue - biomeStart;
        float distToUpper = biomeEnd - noiseValue;

        BiomeBlend result = new()
        {
            primary = biomes[primaryIndex],
            secondary = null,
            blendFactor = 0f
        };

        if (distToLower < blendWidth && primaryIndex > 0)
        {
            float t = distToLower / blendWidth;
            result.secondary = biomes[primaryIndex - 1];

            result.blendFactor = 0.5f * (1f - Smoothstep(t));
        }
        else if (distToUpper < blendWidth && primaryIndex < biomeCount - 1)
        {
            float t = distToUpper / blendWidth;
            result.secondary = biomes[primaryIndex + 1];
            result.blendFactor = 0.5f * (1f - Smoothstep(t));
        }

        return result;
    }

    private float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Set biomes from code (used by SolarSystemManager).
    /// </summary>
    public void SetBiomes(Biome[] newBiomes)
    {
        biomes = newBiomes;
        Debug.Log($"[BiomeManager] Set {biomes?.Length ?? 0} biomes");

        // Log biome details for debugging
        if (biomes != null) foreach (var biome in biomes)
                if (biome != null) Debug.Log($"[BiomeManager] - {biome.biomeName}: {biome.terrainLayers?.Length ?? 0} layers, heightMult={biome.heightMultiplier}");
    }
}

[System.Serializable]
public struct BiomeWeight
{
    public Biome biome;
    public float weight;
}

[System.Serializable]
public struct BiomeBlend
{
    public Biome primary;
    public Biome secondary;
    public float blendFactor;
}