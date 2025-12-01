using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages chunk lifecycle: creation, LOD updates, and destruction.
/// Single source of truth for chunk state.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("Chunk Settings")]
    [SerializeField] private float voxelSize = 1f;
    [SerializeField] private Material terrainMaterial;

    [Header("LOD Settings")]
    [SerializeField] private int viewDistance = 3;
    [SerializeField] private int belowSurfaceViewDistance = 1;

    [Header("Performance")]
    [SerializeField] private int maxChunksPerFrame = 2;
    [SerializeField] private int maxLODUpdatesPerFrame = 2;

    // State
    private Dictionary<Vector3Int, Chunk> chunkLookup = new();
    private Queue<Vector3Int> chunksToGenerate = new();

    private float chunkWorldSize;
    private Vector3Int lastPlayerChunk;

    // Dependencies (injected)
    private DensityProvider densityProvider;
    private BiomeManager biomeManager;
    private CaveGenerator caveGenerator;
    private VegetationSpawner vegetationSpawner;
    private StructurePlacer structurePlacer;

    public IEnumerable<Chunk> ActiveChunks => chunkLookup.Values;
    public float ChunkWorldSize => chunkWorldSize;

    public void Initialize(DensityProvider density, BiomeManager biomes, CaveGenerator caves,
        VegetationSpawner vegetation, StructurePlacer structures)
    {
        densityProvider = density;
        biomeManager = biomes;
        caveGenerator = caves;
        vegetationSpawner = vegetation;
        structurePlacer = structures;

        chunkWorldSize = Chunk.SIZE * voxelSize;

        if (terrainMaterial == null)
        {
            terrainMaterial = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.4f, 0.6f, 0.3f)
            };
        }
    }

    public void UpdateChunks(Vector3Int playerChunk)
    {
        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            UpdateVisibleChunks();
        }

        ProcessChunkGenerationQueue();
    }

    public Chunk GetChunkAt(Vector3Int chunkPos)
    {
        chunkLookup.TryGetValue(chunkPos, out Chunk chunk);
        return chunk;
    }

    public bool TryGetChunk(Vector3Int chunkPos, out Chunk chunk)
    {
        return chunkLookup.TryGetValue(chunkPos, out chunk);
    }

    // REPLACED: Manual floor division -> GridUtility.WorldToCell
    public Vector3Int WorldToChunkPosition(Vector3 worldPos)
    {
        return GridUtility.WorldToCell(worldPos, chunkWorldSize);
    }

    public Vector3 ChunkToWorldPosition(Vector3Int chunkPos)
    {
        return new Vector3(
            chunkPos.x * chunkWorldSize,
            chunkPos.y * chunkWorldSize,
            chunkPos.z * chunkWorldSize
        );
    }

    private void UpdateVisibleChunks()
    {
        HashSet<Vector3Int> chunksNeeded = DetermineNeededChunks();
        UnloadDistantChunks(chunksNeeded);
        QueueNewChunks(chunksNeeded);
    }

    private HashSet<Vector3Int> DetermineNeededChunks()
    {
        HashSet<Vector3Int> chunksNeeded = new();
        int maxViewDist = GetMaxViewDistance();

        Vector3 playerWorldPos = GetPlayerWorldPosition();
        Vector3 playerUp = PlanetMath.GetUpDirection(playerWorldPos, densityProvider.Center);

        for (int x = -maxViewDist; x <= maxViewDist; x++)
        {
            for (int y = -maxViewDist; y <= maxViewDist; y++)
            {
                for (int z = -maxViewDist; z <= maxViewDist; z++)
                {
                    Vector3Int chunkPos = lastPlayerChunk + new Vector3Int(x, y, z);

                    if (ShouldLoadChunk(chunkPos, playerWorldPos, playerUp))
                    {
                        chunksNeeded.Add(chunkPos);
                    }
                }
            }
        }

        return chunksNeeded;
    }

    private Vector3 GetPlayerWorldPosition()
    {
        return ChunkToWorldPosition(lastPlayerChunk) + Vector3.one * (chunkWorldSize * 0.5f);
    }

    private bool ShouldLoadChunk(Vector3Int chunkPos, Vector3 playerWorldPos, Vector3 playerUp)
    {
        if (!IsChunkWithinDirectionalViewDistance(chunkPos, playerWorldPos, playerUp))
            return false;

        return ChunkMightContainSurface(chunkPos);
    }

    private void UnloadDistantChunks(HashSet<Vector3Int> chunksNeeded)
    {
        List<Vector3Int> toUnload = new();

        foreach (var kvp in chunkLookup)
        {
            if (!chunksNeeded.Contains(kvp.Key))
            {
                toUnload.Add(kvp.Key);
            }
        }

        foreach (var pos in toUnload)
        {
            UnloadChunk(pos);
        }
    }

    private void QueueNewChunks(HashSet<Vector3Int> chunksNeeded)
    {
        foreach (var pos in chunksNeeded)
        {
            if (!chunkLookup.ContainsKey(pos) && !chunksToGenerate.Contains(pos))
            {
                chunksToGenerate.Enqueue(pos);
            }
        }
    }

    private bool ChunkMightContainSurface(Vector3Int chunkPos)
    {
        Vector3 chunkWorldPos = ChunkToWorldPosition(chunkPos);
        Vector3 chunkCenter = chunkWorldPos + Vector3.one * (chunkWorldSize * 0.5f);

        // REPLACED: Vector3.Distance -> PlanetMath.GetDistanceFromCenter
        float distToCenter = PlanetMath.GetDistanceFromCenter(chunkCenter, densityProvider.Center);
        float chunkDiagonal = chunkWorldSize * Mathf.Sqrt(3f) * 0.5f;

        float caveDepth = (caveGenerator != null && caveGenerator.enableCaves) ? caveGenerator.maxDepth : 0f;

        float innerBound = densityProvider.Radius - densityProvider.MaxTerrainHeight - caveDepth - chunkDiagonal;
        float outerBound = densityProvider.Radius + densityProvider.MaxTerrainHeight + chunkDiagonal;

        return distToCenter >= innerBound && distToCenter <= outerBound;
    }

    private bool IsChunkWithinDirectionalViewDistance(Vector3Int chunkPos, Vector3 playerWorldPos, Vector3 playerUp)
    {
        Vector3 chunkCenter = ChunkToWorldPosition(chunkPos) + Vector3.one * (chunkWorldSize * 0.5f);
        Vector3 toChunk = chunkCenter - playerWorldPos;

        float verticalAlignment = Vector3.Dot(toChunk.normalized, playerUp);
        int chunkDist = GetChunkDistance(lastPlayerChunk, chunkPos);

        if (verticalAlignment < -0.3f)
        {
            float belowFactor = Mathf.InverseLerp(-0.3f, -1f, verticalAlignment);
            int effectiveViewDist = Mathf.RoundToInt(
                Mathf.Lerp(viewDistance, belowSurfaceViewDistance, belowFactor)
            );
            return chunkDist <= effectiveViewDist;
        }

        return chunkDist <= viewDistance;
    }

    private int GetMaxViewDistance() => viewDistance;

    private void ProcessChunkGenerationQueue()
    {
        for (int i = 0; i < maxChunksPerFrame && chunksToGenerate.Count > 0; i++)
        {
            Vector3Int pos = chunksToGenerate.Dequeue();
            if (!chunkLookup.ContainsKey(pos))
            {
                GenerateChunk(pos);
            }
        }
    }

    private void GenerateChunk(Vector3Int chunkPos)
    {
        GameObject chunkObj = new($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
        chunkObj.transform.SetParent(transform);
        chunkObj.layer = gameObject.layer;

        chunkObj.AddComponent<MeshFilter>();
        chunkObj.AddComponent<MeshRenderer>();
        chunkObj.AddComponent<MeshCollider>();

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.Initialize(chunkPos, voxelSize, densityProvider, terrainMaterial, biomeManager, caveGenerator);

        chunkLookup[chunkPos] = chunk;

        structurePlacer?.PlaceStructuresForChunk(chunk);
        vegetationSpawner?.SpawnForChunk(chunk);
    }

    private void UnloadChunk(Vector3Int chunkPos)
    {
        if (!chunkLookup.TryGetValue(chunkPos, out Chunk chunk)) return;

        vegetationSpawner?.RemoveForChunk(chunk);

        Destroy(chunk.gameObject);
        chunkLookup.Remove(chunkPos);
    }

    private int GetChunkDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Max(Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z)));
    }
}