using UnityEngine;

public enum CaveType
{
    [Tooltip("Long winding tunnels")]
    Worm,
    
    [Tooltip("Large open underground spaces")]
    Cavern,
    
    [Tooltip("Thin vertical cracks and fissures")]
    Fracture,
    
    [Tooltip("Horizontal layered caves, good for underground lakes")]
    Stratified,
    
    [Tooltip("Dense sponge-like cave networks")]
    Sponge,
    
    [Tooltip("Combines multiple techniques for variety")]
    Hybrid
}

public class CaveNoiseFilter : INoiseFilter
{
    private NoiseSettings.CaveNoiseSettings settings;
    private Noise primaryNoise;
    private Noise secondaryNoise;
    private Noise warpNoise;

    public CaveNoiseFilter(NoiseSettings.CaveNoiseSettings settings)
    {
        this.settings = settings;
        primaryNoise = new Noise(settings.seed);
        secondaryNoise = new Noise(settings.seed + 1);
        warpNoise = new Noise(settings.seed + 2);
    }

    public float Evaluate(Vector3 point)
    {
        // Apply domain warping if enabled
        Vector3 warpedPoint = settings.useWarping ? ApplyWarping(point) : point;

        // Evaluate based on cave type
        float noiseValue = settings.caveType switch
        {
            CaveType.Worm => EvaluateWormCave(warpedPoint),
            CaveType.Cavern => EvaluateCavernCave(warpedPoint),
            CaveType.Fracture => EvaluateFractureCave(warpedPoint),
            CaveType.Stratified => EvaluateStratifiedCave(warpedPoint),
            CaveType.Sponge => EvaluateSpongeCave(warpedPoint),
            CaveType.Hybrid => EvaluateHybridCave(warpedPoint),
            _ => EvaluateWormCave(warpedPoint)
        };

        // Apply threshold to create discrete caves
        float caveValue = Mathf.Max(0f, noiseValue - settings.threshold);

        return caveValue * settings.strength;
    }

    #region Domain Warping

    private Vector3 ApplyWarping(Vector3 point)
    {
        float warpStrength = settings.warpStrength;
        float warpFrequency = settings.baseFrequency * 0.5f;

        return point + new Vector3(
            warpNoise.Evaluate(point * warpFrequency + Vector3.right * 100f) * warpStrength,
            warpNoise.Evaluate(point * warpFrequency + Vector3.up * 100f) * warpStrength,
            warpNoise.Evaluate(point * warpFrequency + Vector3.forward * 100f) * warpStrength
        );
    }

    #endregion

    #region Cave Type Evaluators

    /// <summary>
    /// Worm caves: Long, winding tunnels using standard multi-octave noise.
    /// Good for interconnected tunnel systems.
    /// </summary>
    private float EvaluateWormCave(Vector3 point)
    {
        float noiseValue = 0f;
        float frequency = settings.baseFrequency;
        float amplitude = 1f;
        float maxAmplitude = 0f;

        for (int i = 0; i < settings.octaves; i++)
        {
            noiseValue += primaryNoise.Evaluate(point * frequency) * amplitude;
            maxAmplitude += amplitude;
            frequency *= settings.lacunarity;
            amplitude *= settings.persistence;
        }

        // Normalize to roughly -1 to 1 range
        return noiseValue / maxAmplitude;
    }

    /// <summary>
    /// Cavern caves: Large open spaces using low-frequency noise with sharp falloff.
    /// Creates big underground chambers.
    /// </summary>
    private float EvaluateCavernCave(Vector3 point)
    {
        // Use much lower frequency for larger spaces
        float frequency = settings.baseFrequency * 0.3f;
        
        // Single octave for smooth, large shapes
        float noiseValue = primaryNoise.Evaluate(point * frequency);
        
        // Add slight variation with secondary noise
        float detail = secondaryNoise.Evaluate(point * frequency * 2f) * 0.2f;
        noiseValue += detail;

        // Sharpen the edges to create more defined cavern boundaries
        // This creates a more "bubble-like" shape
        noiseValue = Mathf.Pow(Mathf.Abs(noiseValue), 0.7f) * Mathf.Sign(noiseValue);

        return noiseValue;
    }

    /// <summary>
    /// Fracture caves: Thin vertical cracks and fissures.
    /// Good for creating narrow crevasses and vertical shafts.
    /// </summary>
    private float EvaluateFractureCave(Vector3 point)
    {
        // Stretch coordinates to create vertical bias
        Vector3 stretchedPoint = new Vector3(
            point.x * settings.fractureHorizontalScale,
            point.y * settings.fractureVerticalScale,
            point.z * settings.fractureHorizontalScale
        );

        float frequency = settings.baseFrequency;
        float noiseValue = 0f;
        float amplitude = 1f;
        float maxAmplitude = 0f;

        for (int i = 0; i < Mathf.Min(settings.octaves, 3); i++)
        {
            // Use absolute value for ridge-like patterns
            float sample = primaryNoise.Evaluate(stretchedPoint * frequency);
            
            // Invert to create cracks (high values become low)
            noiseValue += (1f - Mathf.Abs(sample) * 2f) * amplitude;
            
            maxAmplitude += amplitude;
            frequency *= settings.lacunarity;
            amplitude *= settings.persistence;
        }

        noiseValue /= maxAmplitude;

        // Sharpen to create thin cracks
        noiseValue = Mathf.Pow(Mathf.Max(0f, noiseValue), settings.fractureSharpness);

        return noiseValue;
    }

    /// <summary>
    /// Stratified caves: Horizontal layered caves.
    /// Good for underground lakes, flat-floored caverns.
    /// </summary>
    private float EvaluateStratifiedCave(Vector3 point)
    {
        // Horizontal layers based on Y coordinate
        float layerFrequency = settings.baseFrequency * settings.stratifiedLayerScale;
        float layerNoise = Mathf.Sin(point.y * layerFrequency * Mathf.PI * 2f);
        
        // Add horizontal variation
        float horizontalVariation = primaryNoise.Evaluate(
            new Vector3(point.x, 0f, point.z) * settings.baseFrequency
        );
        
        // Combine: layers modified by horizontal noise
        float noiseValue = layerNoise * 0.6f + horizontalVariation * 0.4f;
        
        // Add some vertical connectivity
        float verticalConnector = secondaryNoise.Evaluate(point * settings.baseFrequency * 0.5f);
        noiseValue = Mathf.Max(noiseValue, verticalConnector * 0.5f);

        return noiseValue;
    }

    /// <summary>
    /// Sponge caves: Dense network of small interconnected caves.
    /// Creates a Swiss cheese-like structure.
    /// </summary>
    private float EvaluateSpongeCave(Vector3 point)
    {
        float frequency = settings.baseFrequency * 1.5f;
        
        // Sample 3D noise at multiple scales
        float noise1 = primaryNoise.Evaluate(point * frequency);
        float noise2 = secondaryNoise.Evaluate(point * frequency * 1.7f);
        float noise3 = primaryNoise.Evaluate(point * frequency * 2.3f + Vector3.one * 50f);

        // Combine using intersection (all must be high for cave)
        // This creates isolated pockets
        float combined = Mathf.Min(noise1, Mathf.Min(noise2, noise3));
        
        // Alternative: use addition for more connected caves
        // float combined = (noise1 + noise2 + noise3) / 3f;

        // Add connectivity paths
        float connector = primaryNoise.Evaluate(point * frequency * 0.5f);
        combined = Mathf.Max(combined, connector * settings.spongeConnectivity);

        return combined;
    }

    /// <summary>
    /// Hybrid caves: Combines multiple techniques based on depth and position.
    /// Most natural-looking but most expensive to compute.
    /// </summary>
    private float EvaluateHybridCave(Vector3 point)
    {
        // Use position to blend between cave types
        float blendNoise = primaryNoise.Evaluate(point * settings.baseFrequency * 0.2f);
        blendNoise = (blendNoise + 1f) * 0.5f; // Normalize to 0-1
        
        // Get values from different cave types
        float wormValue = EvaluateWormCave(point);
        float cavernValue = EvaluateCavernCave(point);
        
        // Blend based on noise
        float blended = Mathf.Lerp(wormValue, cavernValue, blendNoise);
        
        // Add fractures in certain areas
        float fractureBlend = secondaryNoise.Evaluate(point * settings.baseFrequency * 0.3f);
        if (fractureBlend > 0.3f)
        {
            float fractureValue = EvaluateFractureCave(point);
            float fractureMix = (fractureBlend - 0.3f) / 0.7f; // 0 to 1 in fracture zones
            blended = Mathf.Max(blended, fractureValue * fractureMix * 0.7f);
        }

        return blended;
    }

    #endregion
}