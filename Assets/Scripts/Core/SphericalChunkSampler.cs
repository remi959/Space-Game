using UnityEngine;

public class SphericalChunkSampler {
    // Only sample within a shell around the planet surface
    // Don't waste samples deep inside or far outside
    
    public static bool ShouldSampleChunk(Vector3Int chunkCoord, ShapeSettings settings, CaveSettings caveSettings) {
        Vector3 chunkCenter = PositionConverter.ChunkCoordToWorldCenter(chunkCoord, settings.ChunkSize);
        float distanceFromPlanetCenter = chunkCenter.magnitude;
        
        float innerBound = settings.PlanetRadius - caveSettings.MaxDepth;
        float outerBound = settings.PlanetRadius + settings.MaxTerrainHeight;
        float chunkDiagonal = settings.ChunkSize * Mathf.Sqrt(3);
        
        // Only generate chunks that could contain surface
        return distanceFromPlanetCenter > innerBound - chunkDiagonal &&
               distanceFromPlanetCenter < outerBound + chunkDiagonal;
    }
}