using UnityEngine;

[System.Serializable]
public class CaveNoiseLayer
{
    public string Name = "Cave Layer";
    public bool Enabled = true;
    
    [Tooltip("Use the first cave layer as a mask for this layer.")]
    public bool UseFirstLayerAsMask;
    
    [Tooltip("Blend weight when combining multiple cave layers.")]
    [Range(0f, 1f)]
    public float BlendWeight = 1f;

    [Tooltip("Cave-specific noise settings")]
    public NoiseSettings.CaveNoiseSettings Settings;

    /// <summary>
    /// Creates a new cave layer with default settings for the specified type.
    /// </summary>
    public static CaveNoiseLayer Create(CaveType type, string name = null)
    {
        return new CaveNoiseLayer
        {
            Name = name ?? $"{type} Caves",
            Enabled = true,
            BlendWeight = 1f,
            Settings = NoiseSettings.CaveNoiseSettings.CreateDefault(type)
        };
    }
}