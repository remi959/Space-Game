using UnityEngine;

public static class MeshCapacityEstimator
{
    /// <summary>
    /// Estimates vertex and triangle capacity for a chunk's mesh.
    /// </summary>
    /// <param name="resolution">Voxels per chunk edge (e.g., 16, 32)</param>
    /// <param name="hasCaves">Whether cave generation is enabled</param>
    /// <param name="caveComplexity">0-1 value indicating cave density</param>
    /// <returns>Tuple of (vertexCapacity, triangleIndexCapacity)</returns>
    public static (int vertices, int triangles) EstimateCapacity(
        int resolution, 
        bool hasCaves = false, 
        float caveComplexity = 0.5f)
    {
        // Base surface: approximately resolution² cells intersected
        // Each intersected cell produces ~2 triangles on average
        float baseSurfaceCells = resolution * resolution;
        float avgTrianglesPerCell = 2.0f;
        
        // Surface complexity multiplier
        // 1.0 = smooth sphere
        // 1.5 = terrain with noise
        // 2.0+ = terrain with caves
        float complexityMultiplier = 1.5f;
        
        if (hasCaves)
        {
            // Caves add internal surfaces
            // At max complexity, could roughly double the surface area
            complexityMultiplier += caveComplexity * 1.5f;
        }
        
        float expectedTriangles = baseSurfaceCells * avgTrianglesPerCell * complexityMultiplier;
        
        // Current MarchingCubes doesn't share vertices, so 3 vertices per triangle
        float expectedVertices = expectedTriangles * 3;
        
        // Add 25% buffer for worst-case scenarios
        int vertexCapacity = Mathf.CeilToInt(expectedVertices * 1.25f);
        int triangleCapacity = Mathf.CeilToInt(expectedTriangles * 3 * 1.25f); // 3 indices per triangle
        
        return (vertexCapacity, triangleCapacity);
    }
    
    /// <summary>
    /// Estimates capacity based on chunk type (surface vs deep cave chunk)
    /// </summary>
    public static (int vertices, int triangles) EstimateCapacityForChunk(
        Vector3Int chunkCoord,
        ShapeSettings settings,
        CaveSettings caveSettings)
    {
        Vector3 chunkCenter = PositionConverter.ChunkCoordToWorldCenter(chunkCoord, settings.ChunkSize);
        float distanceFromCenter = chunkCenter.magnitude;
        float surfaceDistance = Mathf.Abs(distanceFromCenter - settings.PlanetRadius);
        
        int resolution = settings.ChunkResolution;
        float chunkDiagonal = settings.ChunkSize * Mathf.Sqrt(3f);
        
        // Determine chunk type based on position
        bool isOnSurface = surfaceDistance < chunkDiagonal;
        bool isUnderground = distanceFromCenter < settings.PlanetRadius - settings.ChunkSize;
        bool hasCaves = caveSettings != null && caveSettings.NoiseLayers != null && caveSettings.NoiseLayers.Length > 0;
        
        if (!isOnSurface && !isUnderground)
        {
            // Above surface - likely empty
            return (64, 64); // Minimal allocation
        }
        
        if (isUnderground && !hasCaves)
        {
            // Deep underground with no caves - solid, no geometry
            return (64, 64);
        }
        
        if (isUnderground && hasCaves)
        {
            // Underground cave chunk - only cave surfaces
            float caveComplexity = EstimateCaveComplexity(caveSettings);
            float caveSurfaceCells = resolution * resolution * caveComplexity * 0.5f;
            int verts = Mathf.CeilToInt(caveSurfaceCells * 2 * 3 * 1.25f);
            int tris = Mathf.CeilToInt(caveSurfaceCells * 2 * 3 * 1.25f);
            return (Mathf.Max(256, verts), Mathf.Max(256, tris));
        }
        
        // Surface chunk - full estimation
        float complexity = hasCaves ? EstimateCaveComplexity(caveSettings) : 0f;
        return EstimateCapacity(resolution, hasCaves, complexity);
    }
    
    private static float EstimateCaveComplexity(CaveSettings caveSettings)
    {
        if (caveSettings?.NoiseLayers == null) return 0f;
        
        float complexity = 0f;
        foreach (var layer in caveSettings.NoiseLayers)
        {
            if (layer.Enabled)
            {
                // Higher strength and lower threshold = more caves
                complexity += layer.Settings.strength * (1f - layer.Settings.threshold);
            }
        }
        return Mathf.Clamp01(complexity / 5f); // Normalize
    }
    
    /// <summary>
    /// Returns theoretical maximum (worst case) for a chunk.
    /// Use this if you want to guarantee no reallocation.
    /// </summary>
    public static (int vertices, int triangles) GetMaxCapacity(int resolution)
    {
        // Absolute maximum: every cell generates 5 triangles
        // This never happens in practice but guarantees no reallocation
        int maxCells = resolution * resolution * resolution;
        int maxTriangles = maxCells * 5;
        int maxVertices = maxTriangles * 3;
        
        return (maxVertices, maxTriangles * 3);
    }
}