using UnityEngine;

/// <summary>
/// Defines LOD levels for terrain chunks.
/// Each level has a different voxel size (resolution) and view distance.
/// </summary>
[System.Serializable]
public class ChunkLODLevel
{
    [Tooltip("Name for debugging")]
    public string name = "LOD0";
    
    [Tooltip("Voxel size multiplier relative to base voxel size (1 = full detail, 2 = half resolution)")]
    [Range(1, 8)]
    public int voxelSizeMultiplier = 1;
    
    [Tooltip("View distance for this LOD level (in chunks)")]
    public int viewDistance = 3;
    
    [Tooltip("Whether to generate colliders for this LOD level")]
    public bool generateColliders = true;
    
    [Tooltip("Whether to spawn vegetation at this LOD level")]
    public bool spawnVegetation = true;
}

/// <summary>
/// Configuration for the chunk LOD system.
/// </summary>
[CreateAssetMenu(fileName = "ChunkLODConfig", menuName = "Procedural Planet/Chunk LOD Config")]
public class ChunkLODConfig : ScriptableObject
{
    [Header("LOD Levels")]
    [Tooltip("Define LOD levels from highest to lowest detail")]
    public ChunkLODLevel[] lodLevels = new ChunkLODLevel[]
    {
        new ChunkLODLevel { name = "LOD0 - Full", voxelSizeMultiplier = 1, viewDistance = 2, generateColliders = true, spawnVegetation = true },
        new ChunkLODLevel { name = "LOD1 - Half", voxelSizeMultiplier = 2, viewDistance = 4, generateColliders = true, spawnVegetation = false },
        new ChunkLODLevel { name = "LOD2 - Quarter", voxelSizeMultiplier = 4, viewDistance = 6, generateColliders = false, spawnVegetation = false }
    };
    
    [Header("Transition")]
    [Tooltip("Blend distance for LOD transitions (currently not implemented - would need seam stitching)")]
    public float transitionBlendDistance = 2f;
    
    /// <summary>
    /// Gets the LOD level for a given distance (in chunks).
    /// </summary>
    public int GetLODLevelForDistance(int chunkDistance)
    {
        for (int i = 0; i < lodLevels.Length; i++)
        {
            if (chunkDistance <= lodLevels[i].viewDistance)
            {
                return i;
            }
        }
        return lodLevels.Length - 1; // Return lowest detail if beyond all distances
    }
    
    /// <summary>
    /// Gets the maximum view distance across all LOD levels.
    /// </summary>
    public int GetMaxViewDistance()
    {
        int max = 0;
        foreach (var level in lodLevels)
        {
            if (level.viewDistance > max)
                max = level.viewDistance;
        }
        return max;
    }
    
    /// <summary>
    /// Creates a default configuration if none exists.
    /// </summary>
    public static ChunkLODConfig CreateDefault()
    {
        var config = CreateInstance<ChunkLODConfig>();
        config.lodLevels = new ChunkLODLevel[]
        {
            new ChunkLODLevel { name = "LOD0 - Full", voxelSizeMultiplier = 1, viewDistance = 2, generateColliders = true, spawnVegetation = true },
            new ChunkLODLevel { name = "LOD1 - Half", voxelSizeMultiplier = 2, viewDistance = 4, generateColliders = true, spawnVegetation = true },
            new ChunkLODLevel { name = "LOD2 - Quarter", voxelSizeMultiplier = 4, viewDistance = 8, generateColliders = false, spawnVegetation = false }
        };
        return config;
    }
}