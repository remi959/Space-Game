using UnityEngine;

/// <summary>
/// Grid-based spatial utilities.
/// </summary>
public static class GridUtility
{
    /// <summary>
    /// Gets the grid cell coordinate for a world position.
    /// </summary>
    public static Vector3Int WorldToCell(Vector3 worldPos, float cellSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.y / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );
    }

    /// <summary>
    /// Gets the world position of a cell's CENTER.
    /// </summary>
    public static Vector3 CellToWorld(Vector3Int cellPos, float cellSize)
    {
        return new Vector3(
            (cellPos.x + 0.5f) * cellSize,
            (cellPos.y + 0.5f) * cellSize,
            (cellPos.z + 0.5f) * cellSize
        );
    }

    /// <summary>
    /// Gets a deterministic random position within a cell.
    /// </summary>
    public static Vector3 GetRandomPositionInCell(Vector3Int cell, float cellSize, int seed, float margin = 0.2f)
    {
        float offsetX = HashUtility.Hash3DToFloat(cell.x, cell.y, cell.z, seed + 100);
        float offsetY = HashUtility.Hash3DToFloat(cell.x, cell.y, cell.z, seed + 200);
        float offsetZ = HashUtility.Hash3DToFloat(cell.x, cell.y, cell.z, seed + 300);

        Vector3 cellOrigin = new Vector3(cell.x, cell.y, cell.z) * cellSize;
        Vector3 offset = new Vector3(
            Mathf.Lerp(margin, 1f - margin, offsetX),
            Mathf.Lerp(margin, 1f - margin, offsetY),
            Mathf.Lerp(margin, 1f - margin, offsetZ)
        ) * cellSize;

        return cellOrigin + offset;
    }
}