using UnityEngine;

/// <summary>
/// A chunk of the planet that uses shared boundary data with neighbors.
/// This prevents duplicate density sampling at chunk boundaries and ensures
/// seamless mesh connections.
/// 
/// Boundary hierarchy (from most to least shared):
/// - Corners: shared by up to 8 chunks (1 sample each)
/// - Edges: shared by up to 4 chunks (resolution+1 samples each)
/// - Faces: shared by 2 chunks ((resolution+1)² samples each)
/// - Interior: unique to this chunk ((resolution-1)³ samples)
/// </summary>
public class PlanetChunk
{
    public Vector3Int Coordinate { get; private set; }
    public float[,,] DensityField { get; private set; }
    public Mesh Mesh { get; private set; }
    public GameObject GameObject { get; private set; }

    public bool IsDirty { get; set; }
    public bool IsGenerated { get; private set; }

    public bool IsEmpty { get; private set; }
    public bool IsSolid { get; private set; }

    private int resolution;
    private float size;
    private SharedBoundaryManager boundaryManager;

    public PlanetChunk(Vector3Int coord, int resolution, float size, SharedBoundaryManager boundaryManager)
    {
        Coordinate = coord;
        this.resolution = resolution;
        this.size = size;
        this.boundaryManager = boundaryManager;

        // +1 because we sample at corners, not centers
        DensityField = new float[resolution + 1, resolution + 1, resolution + 1];
    }

    public Vector3 GetWorldOrigin()
    {
        return new Vector3(
            Coordinate.x * size,
            Coordinate.y * size,
            Coordinate.z * size
        );
    }

    public Vector3 LocalToWorld(int x, int y, int z)
    {
        float step = size / resolution;
        return GetWorldOrigin() + new Vector3(x * step, y * step, z * step);
    }

    /// <summary>
    /// Generates the density field using shared boundary data where available.
    /// Order of operations:
    /// 1. Get shared corners (8 points)
    /// 2. Get shared edges (12 edges, excluding corners)
    /// 3. Get shared faces (6 faces, excluding edges)
    /// 4. Generate interior points
    /// </summary>
    public void GenerateDensityField(DensityGenerator generator)
    {
        boundaryManager.RegisterChunk(Coordinate);

        // 1. Apply corners first (8 corners, shared by up to 8 chunks)
        ApplyCorners(generator);

        // 2. Apply edges (12 edges, shared by up to 4 chunks)
        ApplyEdges(generator);

        // 3. Apply faces (6 faces, shared by 2 chunks)
        ApplyFaces(generator);

        // 4. Generate interior points (unique to this chunk)
        GenerateInteriorPoints(generator);

        IsGenerated = true;
        IsDirty = true;

        CheckChunkContents();
    }

    private void ApplyCorners(DensityGenerator generator)
    {
        // 8 corners of the chunk
        int r = resolution;

        DensityField[0, 0, 0] = boundaryManager.GetOrCreateCorner(Coordinate, 0, generator, size);
        DensityField[r, 0, 0] = boundaryManager.GetOrCreateCorner(Coordinate, 1, generator, size);
        DensityField[r, 0, r] = boundaryManager.GetOrCreateCorner(Coordinate, 2, generator, size);
        DensityField[0, 0, r] = boundaryManager.GetOrCreateCorner(Coordinate, 3, generator, size);
        DensityField[0, r, 0] = boundaryManager.GetOrCreateCorner(Coordinate, 4, generator, size);
        DensityField[r, r, 0] = boundaryManager.GetOrCreateCorner(Coordinate, 5, generator, size);
        DensityField[r, r, r] = boundaryManager.GetOrCreateCorner(Coordinate, 6, generator, size);
        DensityField[0, r, r] = boundaryManager.GetOrCreateCorner(Coordinate, 7, generator, size);
    }

    private void ApplyEdges(DensityGenerator generator)
    {
        int r = resolution;

        // X-axis edges (4 edges)
        ApplyEdge(ChunkEdge.X_Y0_Z0, generator, (i) => (i, 0, 0));
        ApplyEdge(ChunkEdge.X_Y0_Z1, generator, (i) => (i, 0, r));
        ApplyEdge(ChunkEdge.X_Y1_Z0, generator, (i) => (i, r, 0));
        ApplyEdge(ChunkEdge.X_Y1_Z1, generator, (i) => (i, r, r));

        // Y-axis edges (4 edges)
        ApplyEdge(ChunkEdge.Y_X0_Z0, generator, (i) => (0, i, 0));
        ApplyEdge(ChunkEdge.Y_X0_Z1, generator, (i) => (0, i, r));
        ApplyEdge(ChunkEdge.Y_X1_Z0, generator, (i) => (r, i, 0));
        ApplyEdge(ChunkEdge.Y_X1_Z1, generator, (i) => (r, i, r));

        // Z-axis edges (4 edges)
        ApplyEdge(ChunkEdge.Z_X0_Y0, generator, (i) => (0, 0, i));
        ApplyEdge(ChunkEdge.Z_X0_Y1, generator, (i) => (0, r, i));
        ApplyEdge(ChunkEdge.Z_X1_Y0, generator, (i) => (r, 0, i));
        ApplyEdge(ChunkEdge.Z_X1_Y1, generator, (i) => (r, r, i));
    }

    private void ApplyEdge(ChunkEdge edge, DensityGenerator generator,
        System.Func<int, (int x, int y, int z)> indexMapper)
    {
        float[] edgeData = boundaryManager.GetOrCreateEdge(Coordinate, edge, generator, size);

        // Skip first and last indices (corners already applied)
        for (int i = 1; i < resolution; i++)
        {
            var (x, y, z) = indexMapper(i);
            DensityField[x, y, z] = edgeData[i];
        }
    }

    private void ApplyFaces(DensityGenerator generator)
    {
        int r = resolution;

        // -X face (x = 0)
        ApplyFace(FaceDirection.NegativeX, generator, (u, v) => (0, u, v));

        // +X face (x = resolution)
        ApplyFace(FaceDirection.PositiveX, generator, (u, v) => (r, u, v));

        // -Y face (y = 0)
        ApplyFace(FaceDirection.NegativeY, generator, (u, v) => (u, 0, v));

        // +Y face (y = resolution)
        ApplyFace(FaceDirection.PositiveY, generator, (u, v) => (u, r, v));

        // -Z face (z = 0)
        ApplyFace(FaceDirection.NegativeZ, generator, (u, v) => (u, v, 0));

        // +Z face (z = resolution)
        ApplyFace(FaceDirection.PositiveZ, generator, (u, v) => (u, v, r));
    }

    private void ApplyFace(FaceDirection direction, DensityGenerator generator,
        System.Func<int, int, (int x, int y, int z)> indexMapper)
    {
        float[,] faceData = boundaryManager.GetOrCreateFace(Coordinate, direction, generator, size);

        // Skip edges (u=0, u=resolution, v=0, v=resolution) - already applied
        for (int u = 1; u < resolution; u++)
        {
            for (int v = 1; v < resolution; v++)
            {
                var (x, y, z) = indexMapper(u, v);
                DensityField[x, y, z] = faceData[u, v];
            }
        }
    }

    private void GenerateInteriorPoints(DensityGenerator generator)
    {
        // Interior points: not on any face (1 <= x,y,z <= resolution-1)
        for (int x = 1; x < resolution; x++)
        {
            for (int y = 1; y < resolution; y++)
            {
                for (int z = 1; z < resolution; z++)
                {
                    Vector3 worldPos = LocalToWorld(x, y, z);
                    DensityField[x, y, z] = generator.SampleDensity(worldPos);
                }
            }
        }
    }

    private void CheckChunkContents()
    {
        bool hasNegative = false;
        bool hasPositive = false;

        for (int x = 0; x <= resolution && !(hasNegative && hasPositive); x++)
        {
            for (int y = 0; y <= resolution && !(hasNegative && hasPositive); y++)
            {
                for (int z = 0; z <= resolution && !(hasNegative && hasPositive); z++)
                {
                    if (DensityField[x, y, z] < 0) hasNegative = true;
                    else hasPositive = true;
                }
            }
        }

        IsEmpty = !hasNegative;  // All air
        IsSolid = !hasPositive;  // All solid
    }

    public void GenerateMesh(MarchingCubes meshGenerator)
    {
        if (!IsGenerated) return;

        // Skip mesh generation for empty or solid chunks
        if (IsEmpty || IsSolid)
        {
            Mesh = null;
            IsDirty = false;
            return;
        }

        Mesh = meshGenerator.GenerateMesh(DensityField, size / resolution);
        IsDirty = false;
    }

    public void CreateGameObject(Material material, Transform parent, bool includeCollider = true)
    {
        GameObject = new GameObject($"Chunk {Coordinate}");
        GameObject.transform.parent = parent;
        GameObject.transform.position = GetWorldOrigin();

        var meshFilter = GameObject.AddComponent<MeshFilter>();
        var meshRenderer = GameObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = Mesh;
        meshRenderer.material = material;

        // Only add collider if needed (e.g., close to player)
        if (includeCollider && Mesh.vertexCount > 0)
        {
            var meshCollider = GameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = Mesh;
        }
    }

    public void Destroy()
    {
        boundaryManager?.UnregisterChunk(Coordinate);

        if (Mesh != null) Object.Destroy(Mesh);
        if (GameObject != null) Object.Destroy(GameObject);
    }

    /// <summary>
    /// Marks this chunk for regeneration (e.g., after terrain modification)
    /// </summary>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    /// <summary>
    /// Gets the number of samples that would be duplicated without boundary sharing
    /// </summary>
    public static (int shared, int unique, float savingsPercent) CalculateSampleStats(int resolution)
    {
        int totalSamples = (resolution + 1) * (resolution + 1) * (resolution + 1);

        // Unique interior samples
        int interior = (resolution - 1) * (resolution - 1) * (resolution - 1);

        // Shared = total - interior
        int shared = totalSamples - interior;

        float savings = (shared / (float)totalSamples) * 100f;

        return (shared, interior, savings);
    }
}