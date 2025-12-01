using UnityEngine;

/// <summary>
/// Configuration asset for placeable structures (buildings, landmarks, etc).
/// Create in Unity: Assets > Create > Procedural Planet > Structure Asset
/// </summary>
[CreateAssetMenu(fileName = "NewStructure", menuName = "Procedural Planet/Structure Asset")]
public class StructureAsset : ScriptableObject
{
    [Header("=== IDENTITY ===")]
    public string structureName = "Structure";
    
    [Header("=== PREFABS ===")]
    [Tooltip("Main structure prefab to instantiate")]
    public GameObject prefab;
    
    [Tooltip("Alternative prefabs for variety (randomly selected)")]
    public GameObject[] prefabVariants;
    
    [Header("=== SPAWN SETTINGS ===")]
    [Tooltip("Chance to spawn when placement is attempted (0-1)")]
    [Range(0f, 1f)]
    public float spawnChance = 0.5f;
    
    [Tooltip("Maximum slope angle for placement")]
    [Range(0f, 90f)]
    public float maxSlope = 15f;
    
    [Tooltip("Minimum distance from other structures")]
    public float minDistanceFromOthers = 50f;
    
    [Tooltip("Minimum distance from same structure type")]
    public float minDistanceFromSame = 100f;
    
    [Header("=== BIOME CONSTRAINTS ===")]
    [Tooltip("If set, structure only spawns in these biomes")]
    public Biome[] allowedBiomes;
    
    [Tooltip("Structure will not spawn in these biomes")]
    public Biome[] excludedBiomes;
    
    [Header("=== HEIGHT CONSTRAINTS ===")]
    [Tooltip("Minimum height above sea level")]
    public float minHeight = 0f;
    
    [Tooltip("Maximum height above sea level")]
    public float maxHeight = 100f;
    
    [Header("=== TERRAIN MODIFICATION ===")]
    [Tooltip("Flatten terrain under structure")]
    public bool flattenTerrain = false;
    
    [Tooltip("Radius to flatten around structure")]
    public float flattenRadius = 10f;
    
    [Tooltip("Blend distance for terrain flattening")]
    public float flattenBlend = 5f;
    
    [Tooltip("Target height offset from surface for flattening")]
    public float flattenHeightOffset = 0f;
    
    [Header("=== PLACEMENT ===")]
    [Tooltip("Offset from surface (positive = above surface)")]
    public float heightOffset = 0f;
    
    [Tooltip("Random rotation on Y axis")]
    public bool randomYRotation = true;
    
    [Tooltip("Align to surface normal")]
    public bool alignToSurface = true;
    
    [Tooltip("Maximum tilt when aligning to surface")]
    [Range(0f, 90f)]
    public float maxTiltAngle = 30f;
    
    [Header("=== SCALE ===")]
    [Tooltip("Base scale of structure")]
    public float baseScale = 1f;
    
    [Tooltip("Random scale variation")]
    [Range(0f, 0.5f)]
    public float scaleVariation = 0.1f;
    
    // =========================================================================
    // METHODS
    // =========================================================================
    
    /// <summary>
    /// Gets a random prefab from the available options.
    /// </summary>
    public GameObject GetRandomPrefab()
    {
        if (prefabVariants != null && prefabVariants.Length > 0)
        {
            // Include main prefab in random selection
            int totalCount = 1 + prefabVariants.Length;
            int index = Random.Range(0, totalCount);
            
            if (index == 0 || prefab == null)
            {
                return prefab;
            }
            
            return prefabVariants[index - 1] ?? prefab;
        }
        
        return prefab;
    }
    
    /// <summary>
    /// Gets a random scale value.
    /// </summary>
    public float GetRandomScale()
    {
        return baseScale * Random.Range(1f - scaleVariation, 1f + scaleVariation);
    }
    
    /// <summary>
    /// Checks if this structure can be placed at the given surface point.
    /// </summary>
    public bool CanPlaceAt(SurfacePoint point)
    {
        if (!point.isValid) return false;
        
        // Check slope
        if (point.slope > maxSlope) return false;
        
        // Check height
        if (point.height < minHeight || point.height > maxHeight) return false;
        
        // Check allowed biomes
        if (allowedBiomes != null && allowedBiomes.Length > 0)
        {
            bool allowed = false;
            foreach (var biome in allowedBiomes)
            {
                if (biome == point.biome)
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed) return false;
        }
        
        // Check excluded biomes
        if (excludedBiomes != null && excludedBiomes.Length > 0)
        {
            foreach (var biome in excludedBiomes)
            {
                if (biome == point.biome) return false;
            }
        }
        
        return true;
    }
}