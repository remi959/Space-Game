using UnityEngine;

[System.Serializable]
public class NoiseLayer
{
    /// <summary>
    /// How this noise layer blends with others.
    /// - Add: Adds the noise value to the existing value.
    /// - Multiply: Multiplies the existing value by the noise value.
    /// - Max: Takes the maximum of the existing value and the noise value.
    /// - Min: Takes the minimum of the existing value and the noise value.
    /// </summary>
    public enum BlendMode { Add, Multiply, Max, Min }
    public string layerName = "Noise Layer";
    public bool enabled = true;
    public BlendMode blendMode = BlendMode.Add;

    [Header("Noise Settings")]
    public bool invert = false;
    public NoiseGenerator.NoiseSettings settings = new();

    [Header("Filters")]
    [Tooltip("Only apply this noise above this base density")]
    public bool useFloor = false;
    public float floorValue = 0f;

    [Tooltip("Use first layer as mask for this layer")]
    public bool useFirstLayerAsMask = false;

    /// <summary>
    /// Evaluates this noise layer at a point.
    /// </summary>
    public float Evaluate(Vector3 point, int seed, float firstLayerValue = 0f)
    {
        if (!enabled) return 0f;

        float noiseValue = NoiseGenerator.Sample3D(point, settings, seed);

        // INVERT: turns peaks into craters
        if (invert)
        {
            noiseValue = -noiseValue;
        }

        if (useFloor)
        {
            noiseValue = Mathf.Max(0, noiseValue - floorValue);
        }

        if (useFirstLayerAsMask && firstLayerValue > 0)
        {
            noiseValue *= firstLayerValue;
        }

        return noiseValue;
    }
}