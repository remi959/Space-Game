using UnityEngine;

/// <summary>
/// Represents a point on the terrain surface.
/// Single source of truth for surface point data structure.
/// Used by Chunk, VegetationSpawner, StructurePlacer, and SurfaceSampler.
/// </summary>
[System.Serializable]
public struct SurfacePoint
{
    /// <summary>World position of the surface point</summary>
    public Vector3 position;
    
    /// <summary>Surface normal (up direction at this point)</summary>
    public Vector3 normal;
    
    /// <summary>Slope angle in degrees (0 = flat, 90 = vertical)</summary>
    public float slope;
    
    /// <summary>Biome at this location</summary>
    public Biome biome;
    
    /// <summary>Height above sea level (or planet minimum)</summary>
    public float height;
    
    /// <summary>Is this point valid?</summary>
    public bool isValid;

    public static SurfacePoint Invalid => new SurfacePoint { isValid = false };
    
    /// <summary>
    /// Calculates if this point is suitable for placement.
    /// </summary>
    public bool IsSuitableForPlacement(float maxSlope)
    {
        return isValid && slope <= maxSlope;
    }
}