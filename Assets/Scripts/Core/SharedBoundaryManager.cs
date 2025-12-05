using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages shared density data at chunk boundaries to prevent duplicate sampling
/// and ensure seamless mesh connections between adjacent chunks.
/// 
/// Boundary ownership convention:
/// - The chunk with the LOWER coordinate owns the boundary between two chunks
/// - For a boundary between chunk (0,0,0) and (1,0,0), chunk (0,0,0) owns it
/// - The +X face of chunk (0,0,0) is the same as the -X face of chunk (1,0,0)
/// </summary>
public class SharedBoundaryManager
{
    // Shared face data: 2D array of density values at chunk face boundaries
    // Key: (ownerChunkCoord, axis) where axis is 0=X, 1=Y, 2=Z
    // The face is always at the POSITIVE side of the owner chunk
    private Dictionary<(Vector3Int, int), float[,]> sharedFaces = new();
    
    // Shared edge data: 1D array of density values at chunk edge boundaries
    // Key: (minimumCornerCoord, edgeAxis) - the edge extends along edgeAxis from minimumCornerCoord
    private Dictionary<(Vector3Int, int), float[]> sharedEdges = new();
    
    // Shared corner data: single density value at chunk corners
    // Key: cornerWorldCoord (the actual corner position in chunk-space coordinates)
    private Dictionary<Vector3Int, float> sharedCorners = new();
    
    // Track which chunks are currently active (for cleanup)
    private HashSet<Vector3Int> activeChunks = new();
    
    private int resolution;
    
    public SharedBoundaryManager(int chunkResolution)
    {
        resolution = chunkResolution;
    }
    
    #region Face Boundaries
    
    /// <summary>
    /// Gets or creates shared face data for a chunk's face.
    /// The returned array is indexed as [u, v] where u and v are the two axes
    /// perpendicular to the face normal.
    /// </summary>
    public float[,] GetOrCreateFace(Vector3Int chunkCoord, FaceDirection direction, 
        DensityGenerator generator, float chunkSize)
    {
        // Determine which chunk owns this face and on which axis
        (Vector3Int ownerCoord, int axis) = GetFaceOwnerAndAxis(chunkCoord, direction);
        
        var key = (ownerCoord, axis);
        
        if (sharedFaces.TryGetValue(key, out float[,] existingFace))
        {
            return existingFace;
        }
        
        // Generate new face data
        float[,] faceData = GenerateFaceData(ownerCoord, axis, generator, chunkSize);
        sharedFaces[key] = faceData;
        
        return faceData;
    }
    
    private (Vector3Int owner, int axis) GetFaceOwnerAndAxis(Vector3Int chunkCoord, FaceDirection direction)
    {
        // The owner is always the chunk on the negative side of the face
        // The face is always stored as the positive face of the owner
        return direction switch
        {
            FaceDirection.PositiveX => (chunkCoord, 0),                              // This chunk owns +X
            FaceDirection.NegativeX => (chunkCoord + Vector3Int.left, 0),            // Left neighbor owns their +X
            FaceDirection.PositiveY => (chunkCoord, 1),                              // This chunk owns +Y
            FaceDirection.NegativeY => (chunkCoord + Vector3Int.down, 1),            // Down neighbor owns their +Y
            FaceDirection.PositiveZ => (chunkCoord, 2),                              // This chunk owns +Z
            FaceDirection.NegativeZ => (chunkCoord + new Vector3Int(0, 0, -1), 2),   // Back neighbor owns their +Z
            _ => (chunkCoord, 0)
        };
    }
    
    private float[,] GenerateFaceData(Vector3Int ownerCoord, int axis, 
        DensityGenerator generator, float chunkSize)
    {
        float[,] faceData = new float[resolution + 1, resolution + 1];
        float step = chunkSize / resolution;
        
        Vector3 chunkOrigin = new Vector3(
            ownerCoord.x * chunkSize,
            ownerCoord.y * chunkSize,
            ownerCoord.z * chunkSize
        );
        
        // The face is at the positive side of the chunk along the given axis
        // u and v are the two perpendicular axes
        for (int u = 0; u <= resolution; u++)
        {
            for (int v = 0; v <= resolution; v++)
            {
                Vector3 worldPos = axis switch
                {
                    0 => chunkOrigin + new Vector3(chunkSize, u * step, v * step),  // +X face: vary Y,Z
                    1 => chunkOrigin + new Vector3(u * step, chunkSize, v * step),  // +Y face: vary X,Z
                    2 => chunkOrigin + new Vector3(u * step, v * step, chunkSize),  // +Z face: vary X,Y
                    _ => chunkOrigin
                };
                
                faceData[u, v] = generator.SampleDensity(worldPos);
            }
        }
        
        return faceData;
    }
    
    #endregion
    
    #region Edge Boundaries
    
    /// <summary>
    /// Gets or creates shared edge data. Edges are shared by up to 4 chunks.
    /// 
    /// Edge identification: Each edge is identified by its minimum corner (in chunk coords)
    /// and the axis along which it extends.
    /// 
    /// For a chunk, there are 12 edges:
    /// - 4 edges along X axis (at y=0/max, z=0/max combinations)
    /// - 4 edges along Y axis (at x=0/max, z=0/max combinations)  
    /// - 4 edges along Z axis (at x=0/max, y=0/max combinations)
    /// </summary>
    public float[] GetOrCreateEdge(Vector3Int chunkCoord, ChunkEdge edge,
        DensityGenerator generator, float chunkSize)
    {
        // Get the minimum corner of this edge in world chunk coordinates
        // and the axis along which it extends
        (Vector3Int minCorner, int axis) = GetEdgeIdentifier(chunkCoord, edge);
        
        var key = (minCorner, axis);
        
        if (sharedEdges.TryGetValue(key, out float[] existingEdge))
        {
            return existingEdge;
        }
        
        // Generate new edge data
        float[] edgeData = GenerateEdgeData(minCorner, axis, generator, chunkSize);
        sharedEdges[key] = edgeData;
        
        return edgeData;
    }
    
    private (Vector3Int minCorner, int axis) GetEdgeIdentifier(Vector3Int chunkCoord, ChunkEdge edge)
    {
        // Calculate the minimum corner position for this edge
        // The edge extends from minCorner along the specified axis
        Vector3Int offset = edge switch
        {
            // X-axis edges (axis = 0)
            ChunkEdge.X_Y0_Z0 => new Vector3Int(0, 0, 0),
            ChunkEdge.X_Y0_Z1 => new Vector3Int(0, 0, 1),
            ChunkEdge.X_Y1_Z0 => new Vector3Int(0, 1, 0),
            ChunkEdge.X_Y1_Z1 => new Vector3Int(0, 1, 1),
            
            // Y-axis edges (axis = 1)
            ChunkEdge.Y_X0_Z0 => new Vector3Int(0, 0, 0),
            ChunkEdge.Y_X0_Z1 => new Vector3Int(0, 0, 1),
            ChunkEdge.Y_X1_Z0 => new Vector3Int(1, 0, 0),
            ChunkEdge.Y_X1_Z1 => new Vector3Int(1, 0, 1),
            
            // Z-axis edges (axis = 2)
            ChunkEdge.Z_X0_Y0 => new Vector3Int(0, 0, 0),
            ChunkEdge.Z_X0_Y1 => new Vector3Int(0, 1, 0),
            ChunkEdge.Z_X1_Y0 => new Vector3Int(1, 0, 0),
            ChunkEdge.Z_X1_Y1 => new Vector3Int(1, 1, 0),
            
            _ => Vector3Int.zero
        };
        
        int axis = edge switch
        {
            ChunkEdge.X_Y0_Z0 or ChunkEdge.X_Y0_Z1 or ChunkEdge.X_Y1_Z0 or ChunkEdge.X_Y1_Z1 => 0,
            ChunkEdge.Y_X0_Z0 or ChunkEdge.Y_X0_Z1 or ChunkEdge.Y_X1_Z0 or ChunkEdge.Y_X1_Z1 => 1,
            ChunkEdge.Z_X0_Y0 or ChunkEdge.Z_X0_Y1 or ChunkEdge.Z_X1_Y0 or ChunkEdge.Z_X1_Y1 => 2,
            _ => 0
        };
        
        return (chunkCoord + offset, axis);
    }
    
    private float[] GenerateEdgeData(Vector3Int minCorner, int axis,
        DensityGenerator generator, float chunkSize)
    {
        float[] edgeData = new float[resolution + 1];
        float step = chunkSize / resolution;
        
        Vector3 startPos = new Vector3(
            minCorner.x * chunkSize,
            minCorner.y * chunkSize,
            minCorner.z * chunkSize
        );
        
        Vector3 direction = axis switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            2 => Vector3.forward,
            _ => Vector3.right
        };
        
        for (int i = 0; i <= resolution; i++)
        {
            Vector3 worldPos = startPos + direction * (i * step);
            edgeData[i] = generator.SampleDensity(worldPos);
        }
        
        return edgeData;
    }
    
    #endregion
    
    #region Corner Boundaries
    
    /// <summary>
    /// Gets or creates shared corner data. Corners are shared by up to 8 chunks.
    /// Corners are identified by their position in chunk-space coordinates.
    /// </summary>
    public float GetOrCreateCorner(Vector3Int chunkCoord, int cornerIndex,
        DensityGenerator generator, float chunkSize)
    {
        // The corner's world position in chunk coordinates determines ownership
        Vector3Int cornerChunkCoord = GetCornerChunkCoord(chunkCoord, cornerIndex);
        
        if (sharedCorners.TryGetValue(cornerChunkCoord, out float existingValue))
        {
            return existingValue;
        }
        
        // Generate new corner value
        Vector3 worldPos = new Vector3(
            cornerChunkCoord.x * chunkSize,
            cornerChunkCoord.y * chunkSize,
            cornerChunkCoord.z * chunkSize
        );
        
        float cornerValue = generator.SampleDensity(worldPos);
        sharedCorners[cornerChunkCoord] = cornerValue;
        
        return cornerValue;
    }
    
    private Vector3Int GetCornerChunkCoord(Vector3Int chunkCoord, int cornerIndex)
    {
        // Corner indices match marching cubes convention
        // Returns the position of the corner in chunk-space coordinates
        Vector3Int offset = cornerIndex switch
        {
            0 => new Vector3Int(0, 0, 0),     // Origin corner
            1 => new Vector3Int(1, 0, 0),     // +X
            2 => new Vector3Int(1, 0, 1),     // +X+Z
            3 => new Vector3Int(0, 0, 1),     // +Z
            4 => new Vector3Int(0, 1, 0),     // +Y
            5 => new Vector3Int(1, 1, 0),     // +X+Y
            6 => new Vector3Int(1, 1, 1),     // +X+Y+Z
            7 => new Vector3Int(0, 1, 1),     // +Y+Z
            _ => Vector3Int.zero
        };
        
        return chunkCoord + offset;
    }
    
    #endregion
    
    #region Chunk Lifecycle
    
    public void RegisterChunk(Vector3Int coord)
    {
        activeChunks.Add(coord);
    }
    
    public void UnregisterChunk(Vector3Int coord)
    {
        activeChunks.Remove(coord);
        
        // Optionally clean up unused boundaries
        // This is a trade-off between memory and potential regeneration cost
        if (activeChunks.Count == 0)
        {
            // If no chunks are active, clear everything
            Clear();
        }
        // For partial cleanup, you could implement reference counting
        // but for most cases, keeping the cache is fine
    }
    
    /// <summary>
    /// Invalidates all boundary data touching a specific chunk.
    /// Call this when terrain is modified.
    /// </summary>
    public void InvalidateChunkBoundaries(Vector3Int chunkCoord)
    {
        // Remove faces owned by this chunk
        for (int axis = 0; axis < 3; axis++)
        {
            sharedFaces.Remove((chunkCoord, axis));
            
            // Also remove faces from neighbors that this chunk touches
            Vector3Int neighborOffset = axis switch
            {
                0 => Vector3Int.left,
                1 => Vector3Int.down,
                2 => new Vector3Int(0, 0, -1),
                _ => Vector3Int.zero
            };
            sharedFaces.Remove((chunkCoord + neighborOffset, axis));
        }
        
        // Remove edges (simplified - removes all edges touching this chunk)
        // A full implementation would be more selective
        List<(Vector3Int, int)> edgesToRemove = new();
        foreach (var key in sharedEdges.Keys)
        {
            Vector3Int edgeMin = key.Item1;
            // Check if this edge touches the chunk
            if (EdgeTouchesChunk(edgeMin, key.Item2, chunkCoord))
            {
                edgesToRemove.Add(key);
            }
        }
        foreach (var key in edgesToRemove)
        {
            sharedEdges.Remove(key);
        }
        
        // Remove corners touching this chunk
        for (int i = 0; i < 8; i++)
        {
            Vector3Int cornerCoord = GetCornerChunkCoord(chunkCoord, i);
            sharedCorners.Remove(cornerCoord);
        }
    }
    
    private bool EdgeTouchesChunk(Vector3Int edgeMin, int axis, Vector3Int chunkCoord)
    {
        // An edge touches a chunk if the edge's min corner is within one chunk of the chunk's corners
        Vector3Int diff = edgeMin - chunkCoord;
        
        return axis switch
        {
            0 => diff.x == 0 && diff.y >= 0 && diff.y <= 1 && diff.z >= 0 && diff.z <= 1,
            1 => diff.y == 0 && diff.x >= 0 && diff.x <= 1 && diff.z >= 0 && diff.z <= 1,
            2 => diff.z == 0 && diff.x >= 0 && diff.x <= 1 && diff.y >= 0 && diff.y <= 1,
            _ => false
        };
    }
    
    public void Clear()
    {
        sharedFaces.Clear();
        sharedEdges.Clear();
        sharedCorners.Clear();
        activeChunks.Clear();
    }
    
    #endregion
    
    #region Statistics
    
    public (int faces, int edges, int corners, int chunks) GetStats()
    {
        return (sharedFaces.Count, sharedEdges.Count, sharedCorners.Count, activeChunks.Count);
    }
    
    #endregion
}

public enum FaceDirection
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

/// <summary>
/// Identifies the 12 edges of a chunk.
/// Named as {Axis}_{OtherAxis1}{Value}_{OtherAxis2}{Value}
/// e.g., X_Y0_Z1 means an edge along X axis at Y=0, Z=max
/// </summary>
public enum ChunkEdge
{
    // Edges along X axis (4 edges)
    X_Y0_Z0,  // Bottom-back edge
    X_Y0_Z1,  // Bottom-front edge
    X_Y1_Z0,  // Top-back edge
    X_Y1_Z1,  // Top-front edge
    
    // Edges along Y axis (4 edges)
    Y_X0_Z0,  // Left-back edge
    Y_X0_Z1,  // Left-front edge
    Y_X1_Z0,  // Right-back edge
    Y_X1_Z1,  // Right-front edge
    
    // Edges along Z axis (4 edges)
    Z_X0_Y0,  // Left-bottom edge
    Z_X0_Y1,  // Left-top edge
    Z_X1_Y0,  // Right-bottom edge
    Z_X1_Y1   // Right-top edge
}