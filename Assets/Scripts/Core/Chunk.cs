using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Optimized chunk that only updates affected regions.
/// Key features:
/// 1. Bounded voxel iteration during modification
/// 2. Dirty region tracking for partial mesh updates
/// 3. Configurable immediate vs deferred collider updates
/// 4. LOD support with variable voxel resolution
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public const int SIZE = 16;

    private BiomeManager biomeManager;
    private CaveGenerator caveGenerator;
    private DensityProvider densityProvider;

    // =========================================================================
    // STATE
    // =========================================================================

    private float[,,] densities;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private MeshData meshDataCache;
    private Mesh generatedMesh;

    public Vector3Int ChunkPosition { get; private set; }
    public float VoxelSize { get; private set; }
    public bool IsModified { get; private set; }

    // Dirty tracking
    private bool isDirty = false;
    private bool colliderDirty = false;
    private float colliderUpdateTimer = 0f;

    private const float COLLIDER_UPDATE_DELAY = 0.1f;
    private bool requireImmediateColliderUpdate = false;

    private List<SurfacePoint> surfacePoints = new List<SurfacePoint>();
    public List<SurfacePoint> SurfacePoints => surfacePoints;

    // Bounds of dirty region
    private Vector3Int dirtyMin;
    private Vector3Int dirtyMax;

    // Surface point collection settings
    private const int targetSurfacePointCount = 200;
    private const float minNormalDotThreshold = 0.3f;
    private const float minSurfaceHeight = -5f;

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        meshDataCache = new MeshData();

        if (meshFilter.sharedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "ChunkMesh";
            generatedMesh.MarkDynamic();
            meshFilter.sharedMesh = generatedMesh;
        }
        else
        {
            generatedMesh = meshFilter.sharedMesh;
            generatedMesh.MarkDynamic();
        }

        ResetDirtyBounds();
    }

    private void Update()
    {
        if (colliderDirty && meshCollider != null)
        {
            if (requireImmediateColliderUpdate)
            {
                UpdateCollider();
                colliderDirty = false;
                colliderUpdateTimer = 0f;
                requireImmediateColliderUpdate = false;
            }
            else
            {
                colliderUpdateTimer += Time.deltaTime;
                if (colliderUpdateTimer >= COLLIDER_UPDATE_DELAY)
                {
                    UpdateCollider();
                    colliderDirty = false;
                    colliderUpdateTimer = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Standard initialization with full detail.
    /// </summary>
    public void Initialize(Vector3Int chunkPos, float voxelSize, DensityProvider densityProvider,
        Material material, BiomeManager biomeManager = null, CaveGenerator caveGenerator = null)
    {
        ChunkPosition = chunkPos;
        VoxelSize = voxelSize;
        this.densityProvider = densityProvider;
        this.biomeManager = biomeManager;
        this.caveGenerator = caveGenerator;

        int effectiveSize = SIZE;
        densities = new float[effectiveSize + 1, effectiveSize + 1, effectiveSize + 1];

        transform.position = new Vector3(
            chunkPos.x * SIZE * voxelSize,
            chunkPos.y * SIZE * voxelSize,
            chunkPos.z * SIZE * voxelSize
        );

        meshRenderer.material = material;

        // meshCollider.enabled = false;


        GenerateDensities();
        GenerateMesh();
        UpdateCollider();
    }

    // =========================================================================
    // DENSITY GENERATION
    // =========================================================================

    public void GenerateDensities()
    {
        int effectiveSize = SIZE;
        float effectiveVoxelSize = VoxelSize;

        for (int x = 0; x <= effectiveSize; x++)
        {
            for (int y = 0; y <= effectiveSize; y++)
            {
                for (int z = 0; z <= effectiveSize; z++)
                {
                    Vector3 worldPos = LocalToWorld(x, y, z, effectiveVoxelSize);
                    densities[x, y, z] = densityProvider.GetDensityAt(worldPos);
                }
            }
        }
        isDirty = true;
    }

    private Vector3 LocalToWorld(int x, int y, int z, float voxelSize = -1f)
    {
        if (voxelSize < 0) voxelSize = VoxelSize;
        return transform.position + new Vector3(x, y, z) * voxelSize;
    }

    // =========================================================================
    // OPTIMIZED TERRAIN MODIFICATION
    // =========================================================================

    public bool ModifyTerrainOptimized(Vector3 worldPos, float radius, float strength, bool immediateCollider = true)
    {
        // Use helper for bounds calculation
        ChunkMeshUtility.GetAffectedVoxelBounds(
            worldPos, transform.position, radius, VoxelSize, SIZE,
            out Vector3Int min, out Vector3Int max);

        if (max.x < 0 || max.y < 0 || max.z < 0 ||
            min.x > SIZE || min.y > SIZE || min.z > SIZE)
        {
            return false;
        }

        bool modified = false;
        float radiusSqr = radius * radius;

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    Vector3 voxelWorldPos = LocalToWorld(x, y, z);
                    float distSqr = (voxelWorldPos - worldPos).sqrMagnitude;

                    if (distSqr < radiusSqr)
                    {
                        float dist = Mathf.Sqrt(distSqr);
                        float falloff = 1f - (dist / radius);
                        falloff *= falloff;

                        densities[x, y, z] += strength * falloff;
                        modified = true;
                        ExpandDirtyBounds(x, y, z);
                    }
                }
            }
        }

        if (modified)
        {
            isDirty = true;
            IsModified = true;
            if (immediateCollider) requireImmediateColliderUpdate = true;
        }

        return modified;
    }

    // =========================================================================
    // DIRTY REGION TRACKING
    // =========================================================================

    private void ResetDirtyBounds()
    {
        dirtyMin = new Vector3Int(SIZE + 1, SIZE + 1, SIZE + 1);
        dirtyMax = new Vector3Int(-1, -1, -1);
    }

    private void ExpandDirtyBounds(int x, int y, int z)
    {
        dirtyMin.x = Mathf.Min(dirtyMin.x, x);
        dirtyMin.y = Mathf.Min(dirtyMin.y, y);
        dirtyMin.z = Mathf.Min(dirtyMin.z, z);

        dirtyMax.x = Mathf.Max(dirtyMax.x, x);
        dirtyMax.y = Mathf.Max(dirtyMax.y, y);
        dirtyMax.z = Mathf.Max(dirtyMax.z, z);
    }

    // =========================================================================
    // MESH GENERATION
    // =========================================================================

    public void GenerateMesh()
    {
        if (!isDirty) return;

        int effectiveSize = SIZE;
        float effectiveVoxelSize = VoxelSize;

        if (!ChunkMeshUtility.HasSurfaceCrossing(densities, effectiveSize))
        {
            ClearMesh();
            return;
        }

        MarchingCubes.GenerateMesh(densities, effectiveSize + 1, effectiveVoxelSize, meshDataCache, true);

        if (biomeManager != null)
        {
            VertexColorProvider.ApplyBiomeColors(
                meshDataCache,
                transform,
                densityProvider.Center,
                densityProvider.Radius,
                biomeManager,
                caveGenerator,
                densityProvider.Seed
            );
        }

        generatedMesh.Clear();
        generatedMesh.SetVertices(meshDataCache.vertices);
        generatedMesh.SetTriangles(meshDataCache.triangles, 0);

        if (meshDataCache.normals.Count > 0)
            generatedMesh.SetNormals(meshDataCache.normals);
        else
            generatedMesh.RecalculateNormals();

        if (meshDataCache.colors.Count > 0)
            generatedMesh.SetColors(meshDataCache.colors);

        generatedMesh.RecalculateBounds();
        meshFilter.sharedMesh = generatedMesh;

        CollectSurfacePoints(meshDataCache);

        isDirty = false;
        colliderDirty = true;
        ResetDirtyBounds();
    }

    public async void GenerateMeshAsync()
    {
        if (!isDirty) return;

        var densitiesCopy = (float[,,])densities.Clone();
        int effectiveSize = SIZE + 1;
        float voxelSize = VoxelSize;

        MeshData result = await Task.Run(() =>
        {
            var data = new MeshData();
            MarchingCubes.GenerateMesh(densitiesCopy, effectiveSize, voxelSize, data, true);
            return data;
        });

        // Check if chunk was destroyed while awaiting
        if (this == null || generatedMesh == null) return;

        ApplyMeshData(result);
    }

    private void ApplyMeshData(MeshData meshData)
    {
        generatedMesh.Clear();
        generatedMesh.SetVertices(meshData.vertices);
        generatedMesh.SetTriangles(meshData.triangles, 0);

        if (meshData.normals.Count > 0)
            generatedMesh.SetNormals(meshData.normals);
        else
            generatedMesh.RecalculateNormals();

        if (meshData.colors.Count > 0)
            generatedMesh.SetColors(meshData.colors);

        colliderDirty = true;
        isDirty = false;
        ResetDirtyBounds();
    }

    private void CollectSurfacePoints(MeshData meshData)
    {
        SurfaceSampler.CollectSurfacePointsFromMesh(
        meshFilter.sharedMesh,
        transform,
        densityProvider.Center,
        densityProvider.Radius,
        biomeManager,
        densityProvider.Seed,
        targetSurfacePointCount,
        surfacePoints,
        minNormalDotThreshold,
        minSurfaceHeight
        );
    }

    private void ClearMesh()
    {
        generatedMesh.Clear();
        meshFilter.sharedMesh = generatedMesh;
        if (meshCollider != null) meshCollider.sharedMesh = null;
        isDirty = false;
        colliderDirty = false;
        requireImmediateColliderUpdate = false;
        ResetDirtyBounds();
    }

    private void UpdateCollider()
    {
        if (meshCollider == null) return;

        if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    public void ForceColliderUpdate()
    {
        if (meshCollider == null) return;

        UpdateCollider();
        colliderDirty = false;
        colliderUpdateTimer = 0f;
        requireImmediateColliderUpdate = false;
    }

    // =========================================================================
    // UTILITY
    // =========================================================================

    public Bounds GetWorldBounds()
    {
        float chunkWorldSize = SIZE * VoxelSize;
        Vector3 center = transform.position + Vector3.one * (chunkWorldSize * 0.5f);
        return new Bounds(center, Vector3.one * chunkWorldSize);
    }

    public bool IntersectsSphere(Vector3 center, float radius)
    {
        Bounds bounds = GetWorldBounds();
        bounds.Expand(radius * 2f);
        return bounds.Contains(center);
    }

    public float GetDensity(int x, int y, int z)
    {
        int effectiveSize = SIZE;
        if (x < 0 || x > effectiveSize || y < 0 || y > effectiveSize || z < 0 || z > effectiveSize)
            return 0f;
        return densities[x, y, z];
    }

    private void OnDestroy()
    {
        if (meshFilter.sharedMesh != null)
        {
            Destroy(meshFilter.sharedMesh);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (surfacePoints == null) return;

        Gizmos.color = Color.blue;
        foreach (var point in surfacePoints)
        {
            Gizmos.DrawLine(point.position, point.position + point.normal * 2f);
        }
    }
}