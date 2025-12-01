using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Post-process mesh smoothing for marching cubes output.
/// </summary>
public static class MeshSmoother
{
    /// <summary>
    /// Applies Laplacian smoothing to reduce blockiness.
    /// </summary>
    public static void SmoothMesh(MeshData meshData, int iterations = 1, float strength = 0.5f)
    {
        if (meshData.vertices.Count == 0) return;

        // Build adjacency map
        Dictionary<int, List<int>> adjacency = BuildAdjacencyMap(meshData);

        Vector3[] smoothedPositions = new Vector3[meshData.vertices.Count];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < meshData.vertices.Count; i++)
            {
                if (adjacency.TryGetValue(i, out List<int> neighbors) && neighbors.Count > 0)
                {
                    Vector3 avg = Vector3.zero;
                    foreach (int neighbor in neighbors)
                    {
                        avg += meshData.vertices[neighbor];
                    }
                    avg /= neighbors.Count;

                    smoothedPositions[i] = Vector3.Lerp(meshData.vertices[i], avg, strength);
                }
                else
                {
                    smoothedPositions[i] = meshData.vertices[i];
                }
            }

            // Apply smoothed positions
            for (int i = 0; i < meshData.vertices.Count; i++)
            {
                meshData.vertices[i] = smoothedPositions[i];
            }
        }
    }

    private static Dictionary<int, List<int>> BuildAdjacencyMap(MeshData meshData)
    {
        var adjacency = new Dictionary<int, List<int>>();

        for (int i = 0; i < meshData.triangles.Count; i += 3)
        {
            int v0 = meshData.triangles[i];
            int v1 = meshData.triangles[i + 1];
            int v2 = meshData.triangles[i + 2];

            AddAdjacency(adjacency, v0, v1);
            AddAdjacency(adjacency, v0, v2);
            AddAdjacency(adjacency, v1, v0);
            AddAdjacency(adjacency, v1, v2);
            AddAdjacency(adjacency, v2, v0);
            AddAdjacency(adjacency, v2, v1);
        }

        return adjacency;
    }

    private static void AddAdjacency(Dictionary<int, List<int>> adjacency, int from, int to)
    {
        if (!adjacency.ContainsKey(from))
            adjacency[from] = new List<int>(6);

        if (!adjacency[from].Contains(to))
            adjacency[from].Add(to);
    }
}