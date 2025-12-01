using UnityEngine;

[CreateAssetMenu(fileName = "New Vegetation", menuName = "Procedural Planet/Vegetation")]
public class VegetationAsset : ScriptableObject
{
    [Header("Basic Info")]
    public string vegetationName = "Tree";
    public GameObject[] prefabVariants;
    
    [Header("Placement Rules")]
    [Range(0f, 90f)]
    public float maxSlope = 30f;
    
    [Range(0f, 5f)]
    public float density = 0.5f;
    
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    
    [Header("Spacing")]
    public float minDistance = 2f;  // Minimum distance between instances
    
    [Header("Biome Affinity")]
    [Tooltip("Which biomes this vegetation appears in")]
    public Biome[] allowedBiomes;
    
    [Header("Height Range")]
    public float minHeight = -10f;
    public float maxHeight = 100f;
    
    /// <summary>
    /// Checks if this vegetation can be placed at the given surface point.
    /// </summary>
    public bool CanPlaceAt(SurfacePoint point)
    {
        // Check slope
        if (point.slope > maxSlope) return false;
        
        // Check height
        if (point.height < minHeight || point.height > maxHeight) return false;
        
        // Check biome
        if (allowedBiomes != null && allowedBiomes.Length > 0)
        {
            bool biomeAllowed = false;
            foreach (var biome in allowedBiomes)
            {
                if (biome == point.biome)
                {
                    biomeAllowed = true;
                    break;
                }
            }
            if (!biomeAllowed) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Gets a random prefab variant.
    /// </summary>
    public GameObject GetRandomPrefab()
    {
        if (prefabVariants == null || prefabVariants.Length == 0)
            return null;
        return prefabVariants[Random.Range(0, prefabVariants.Length)];
    }
    
    /// <summary>
    /// Gets a random scale within the defined range.
    /// </summary>
    public float GetRandomScale()
    {
        return Random.Range(minScale, maxScale);
    }
}