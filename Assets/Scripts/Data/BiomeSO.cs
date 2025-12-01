using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Procedural Planet/Biome")]
public class Biome : ScriptableObject
{
    [Header("Identification")]
    public string biomeName = "Default";
    public Color debugColor = Color.green;
    
    [Header("Terrain Shape")]
    [Tooltip("How much this biome affects base terrain height")]
    public float heightMultiplier = 1f;
    
    [Tooltip("Additional height offset")]
    public float heightOffset = 0f;
    
    [Tooltip("Noise layers specific to this biome")]
    public NoiseLayer[] terrainLayers;
    
    [Header("Visuals")]
    public Color groundColor = Color.green;
    public Color cliffColor = Color.gray;
    
    [Header("Vegetation")]
    [Range(0f, 1f)]
    public float vegetationDensity = 0.5f;
    
    // Add more biome-specific settings as needed
}