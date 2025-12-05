using UnityEngine;

public static class PositionConverter
{
    // World position to chunk coordinate
    public static Vector3Int WorldToChunkCoord(Vector3 worldPos, int chunkSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.y / chunkSize),
            Mathf.FloorToInt(worldPos.z / chunkSize)
        );
    }

    // Chunk coordinate + local position to world position
    public static Vector3 ChunkLocalToWorld(Vector3Int chunkCoord, Vector3 localPos, int chunkSize)
    {
        return new Vector3(
            chunkCoord.x * chunkSize + localPos.x,
            chunkCoord.y * chunkSize + localPos.y,
            chunkCoord.z * chunkSize + localPos.z
        );
    }

    // Voxel index to local position within chunk
    public static Vector3 VoxelToLocal(int x, int y, int z, int chunkSize, int chunkResolution)
    {
        float step = chunkSize / chunkResolution;
        return new Vector3(x * step, y * step, z * step);
    }

    public static Vector3 ChunkCoordToWorldCenter(Vector3Int chunkCoord, int chunkSize)
    {
        float halfChunk = chunkSize / 2f;
        return new Vector3(
            chunkCoord.x * chunkSize + halfChunk,
            chunkCoord.y * chunkSize + halfChunk,
            chunkCoord.z * chunkSize + halfChunk
        );
    }
}