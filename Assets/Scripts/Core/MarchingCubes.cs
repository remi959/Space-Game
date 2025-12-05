using System.Collections.Generic;
using UnityEngine;

public class MarchingCubes
{
    private List<Vector3> vertices;
    private List<int> triangles;
    
    // Track actual usage for adaptive sizing
    private int peakVertexCount = 0;
    private int peakTriangleCount = 0;
    private int meshesGenerated = 0;
    
    // Default capacity if not specified
    private int defaultVertexCapacity;
    private int defaultTriangleCapacity;

    public MarchingCubes(int resolution = 16, bool hasCaves = false)
    {
        var (vertCap, triCap) = MeshCapacityEstimator.EstimateCapacity(resolution, hasCaves);
        defaultVertexCapacity = vertCap;
        defaultTriangleCapacity = triCap;
        
        vertices = new List<Vector3>(defaultVertexCapacity);
        triangles = new List<int>(defaultTriangleCapacity);
    }

    public Mesh GenerateMesh(float[,,] densityField, float cellSize)
    {
        // Clear but retain capacity
        vertices.Clear();
        triangles.Clear();

        int sizeX = densityField.GetLength(0) - 1;
        int sizeY = densityField.GetLength(1) - 1;
        int sizeZ = densityField.GetLength(2) - 1;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    MarchCube(densityField, x, y, z, cellSize);
                }
            }
        }

        // Track peak usage for diagnostics
        TrackUsage();

        Mesh mesh = new Mesh();
        
        // Use 32-bit indices if needed for large meshes
        if (vertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    /// <summary>
    /// Generate mesh with pre-calculated capacity hint
    /// </summary>
    public Mesh GenerateMesh(float[,,] densityField, float cellSize, int vertexHint, int triangleHint)
    {
        // Ensure capacity before generation
        if (vertices.Capacity < vertexHint)
            vertices.Capacity = vertexHint;
        if (triangles.Capacity < triangleHint)
            triangles.Capacity = triangleHint;
            
        return GenerateMesh(densityField, cellSize);
    }

    private void TrackUsage()
    {
        meshesGenerated++;
        
        if (vertices.Count > peakVertexCount)
        {
            peakVertexCount = vertices.Count;
        }
        if (triangles.Count > peakTriangleCount)
        {
            peakTriangleCount = triangles.Count;
        }
        
        // Log if we're significantly under or over-allocating
        if (meshesGenerated % 100 == 0)
        {
            float vertexEfficiency = (float)peakVertexCount / vertices.Capacity;
            float triangleEfficiency = (float)peakTriangleCount / triangles.Capacity;
            
            if (vertexEfficiency < 0.25f || vertexEfficiency > 1.0f)
            {
                Debug.Log($"[MarchingCubes] Capacity efficiency - Vertices: {vertexEfficiency:P0} ({peakVertexCount}/{vertices.Capacity}), Triangles: {triangleEfficiency:P0}");
            }
        }
    }

    /// <summary>
    /// Get statistics about mesh generation for tuning
    /// </summary>
    public (int peakVerts, int peakTris, int meshCount, float vertEfficiency, float triEfficiency) GetStats()
    {
        return (
            peakVertexCount,
            peakTriangleCount,
            meshesGenerated,
            vertices.Capacity > 0 ? (float)peakVertexCount / vertices.Capacity : 0,
            triangles.Capacity > 0 ? (float)peakTriangleCount / triangles.Capacity : 0
        );
    }

    private void MarchCube(float[,,] field, int x, int y, int z, float cellSize)
    {
        float[] cornerDensities = new float[8];
        Vector3[] cornerPositions = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            Vector3Int offset = CornerOffsets[i];
            cornerDensities[i] = field[x + offset.x, y + offset.y, z + offset.z];
            cornerPositions[i] = new Vector3(
                (x + offset.x) * cellSize,
                (y + offset.y) * cellSize,
                (z + offset.z) * cellSize
            );
        }

        int cubeIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cornerDensities[i] < 0) cubeIndex |= 1 << i;
        }

        if (Tables.EdgeTable[cubeIndex] == 0) return;

        Vector3[] edgeVertices = new Vector3[12];
        for (int i = 0; i < 12; i++)
        {
            if ((Tables.EdgeTable[cubeIndex] & (1 << i)) != 0)
            {
                int c1 = EdgeConnections[i, 0];
                int c2 = EdgeConnections[i, 1];
                edgeVertices[i] = InterpolateEdge(
                    cornerPositions[c1], cornerPositions[c2],
                    cornerDensities[c1], cornerDensities[c2]
                );
            }
        }

        for (int i = 0; Tables.TriangleTable[cubeIndex, i] != -1; i += 3)
        {
            int baseIndex = vertices.Count;

            vertices.Add(edgeVertices[Tables.TriangleTable[cubeIndex, i]]);
            vertices.Add(edgeVertices[Tables.TriangleTable[cubeIndex, i + 1]]);
            vertices.Add(edgeVertices[Tables.TriangleTable[cubeIndex, i + 2]]);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }
    }

    private static readonly Vector3Int[] CornerOffsets = {
        new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(1, 0, 1), new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
        new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1)
    };

    private static readonly int[,] EdgeConnections = new int[12, 2]
    {
        {0, 1}, {1, 2}, {2, 3}, {3, 0},
        {4, 5}, {5, 6}, {6, 7}, {7, 4},
        {0, 4}, {1, 5}, {2, 6}, {3, 7}
    };

    private Vector3 InterpolateEdge(Vector3 p1, Vector3 p2, float v1, float v2)
    {
        if (Mathf.Abs(v1) < 0.00001f) return p1;
        if (Mathf.Abs(v2) < 0.00001f) return p2;
        if (Mathf.Abs(v1 - v2) < 0.00001f) return p1;

        float t = -v1 / (v2 - v1);

        return new Vector3(
            p1.x + t * (p2.x - p1.x),
            p1.y + t * (p2.y - p1.y),
            p1.z + t * (p2.z - p1.z)
        );
    }
}