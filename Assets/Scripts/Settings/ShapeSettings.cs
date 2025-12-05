using UnityEngine;

[CreateAssetMenu(menuName = "Planet/Shape Settings")]
public class ShapeSettings : ScriptableObject
{
    [Header("Planet Dimensions")]
    [Tooltip("Radius of the planet in world units")]
    public float PlanetRadius = 100f;

    [Tooltip("Maximum height terrain can rise above the base radius")]
    public float MaxTerrainHeight = 10f;

    [Tooltip("Maximum depth terrain can go below the base radius (for caves)")]
    public float MaxTerrainDepth = 5f;

    [Header("Chunk Settings")]
    [Tooltip("Size of each chunk in world units")]
    public int ChunkSize = 16;

    [Tooltip("Number of voxels per chunk edge (higher = more detail, slower)")]
    [Range(4, 32)]
    public int ChunkResolution = 16;

    [Header("Terrain Noise")]
    [Tooltip("Noise layers that define terrain shape")]
    public NoiseLayer[] NoiseLayers;

    void OnValidate()
    {
        // Ensure reasonable values
        PlanetRadius = Mathf.Max(1f, PlanetRadius);
        MaxTerrainHeight = Mathf.Max(0f, MaxTerrainHeight);
        MaxTerrainDepth = Mathf.Max(0f, MaxTerrainDepth);
        ChunkSize = Mathf.Max(1, ChunkSize);
        ChunkResolution = Mathf.Clamp(ChunkResolution, 4, 32);
    }
}