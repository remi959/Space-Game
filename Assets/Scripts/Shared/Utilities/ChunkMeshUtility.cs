using UnityEngine;

/// <summary>
/// Static utilities for chunk mesh operations.
/// Keeps Chunk.cs focused on state management.
/// </summary>
public static class ChunkMeshUtility
{
    /// <summary>
    /// Checks if a density field contains a surface crossing.
    /// </summary>
    public static bool HasSurfaceCrossing(float[,,] densities, int size)
    {
        float minD = float.MaxValue;
        float maxD = float.MinValue;

        for (int x = 0; x <= size; x++)
        {
            for (int y = 0; y <= size; y++)
            {
                for (int z = 0; z <= size; z++)
                {
                    float d = densities[x, y, z];
                    if (d < minD) minD = d;
                    if (d > maxD) maxD = d;

                    // Early exit if surface found
                    if (minD <= MarchingCubes.SURFACE_LEVEL && maxD >= MarchingCubes.SURFACE_LEVEL)
                        return true;
                }
            }
        }
        return minD <= MarchingCubes.SURFACE_LEVEL && maxD >= MarchingCubes.SURFACE_LEVEL;
    }

    /// <summary>
    /// Calculates the modification falloff for terrain editing.
    /// </summary>
    public static float CalculateModificationFalloff(float distance, float radius)
    {
        float falloff = 1f - (distance / radius);
        return falloff * falloff; // Quadratic falloff
    }

    /// <summary>
    /// Gets the voxel bounds affected by a spherical modification.
    /// </summary>
    public static void GetAffectedVoxelBounds(
        Vector3 worldPos,
        Vector3 chunkWorldPos,
        float radius,
        float voxelSize,
        int chunkSize,
        out Vector3Int min,
        out Vector3Int max)
    {
        Vector3 localPos = (worldPos - chunkWorldPos) / voxelSize;
        float voxelRadius = radius / voxelSize;

        min = new Vector3Int(
            Mathf.Max(0, Mathf.FloorToInt(localPos.x - voxelRadius) - 1),
            Mathf.Max(0, Mathf.FloorToInt(localPos.y - voxelRadius) - 1),
            Mathf.Max(0, Mathf.FloorToInt(localPos.z - voxelRadius) - 1)
        );

        max = new Vector3Int(
            Mathf.Min(chunkSize, Mathf.CeilToInt(localPos.x + voxelRadius) + 1),
            Mathf.Min(chunkSize, Mathf.CeilToInt(localPos.y + voxelRadius) + 1),
            Mathf.Min(chunkSize, Mathf.CeilToInt(localPos.z + voxelRadius) + 1)
        );
    }
}