using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Facade for the procedural planet system.
/// Coordinates subsystems but delegates actual work.
/// </summary>
public class ProceduralPlanet : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private DensityProvider densityProvider;
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private BiomeManager biomeManager;
    [SerializeField] private CaveGenerator caveGenerator;
    [SerializeField] private VegetationSpawner vegetationSpawner;
    [SerializeField] private StructurePlacer structurePlacer;

    [Header("Settings")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private Transform playerTransform;

    [Header("Terrain Modification")]
    [SerializeField] private int maxMeshUpdatesPerFrame = 4;
    [SerializeField] private bool immediateColliderOnModify = true;

    private HashSet<Chunk> chunksNeedingMeshUpdate = new();
    private List<Vector3Int> affectedChunksBuffer = new(27);
    private readonly List<Chunk> toRemoveBuffer = new(8);

    // Public API
    public Vector3 PlanetCenter => transform.position;
    public float Radius => densityProvider.Radius;
    public int Seed => seed;
    public float MaxTerrainHeight => densityProvider.MaxTerrainHeight;

    private void Start()
    {
        densityProvider.Initialize(transform.position, seed);
        chunkManager.Initialize(densityProvider, biomeManager, caveGenerator, vegetationSpawner, structurePlacer);

        if (vegetationSpawner != null && playerTransform != null)
        {
            vegetationSpawner.SetPlayerTransform(playerTransform);
        }
    }

    private void Update()
    {
        if (playerTransform != null)
        {
            Vector3Int playerChunk = chunkManager.WorldToChunkPosition(playerTransform.position);
            chunkManager.UpdateChunks(playerChunk);
        }

        ProcessMeshUpdateQueue();
    }

    public float GetDensityAt(Vector3 worldPos) => densityProvider.GetDensityAt(worldPos);

    public void ModifyTerrain(Vector3 worldPos, float radius, float strength)
    {
        GetChunksInRadius(worldPos, radius, affectedChunksBuffer);

        foreach (Vector3Int chunkPos in affectedChunksBuffer)
        {
            if (chunkManager.TryGetChunk(chunkPos, out Chunk chunk))
            {
                if (chunk.ModifyTerrainOptimized(worldPos, radius, strength, immediateColliderOnModify))
                {
                    chunksNeedingMeshUpdate.Add(chunk);
                }
            }
        }
    }

    private void GetChunksInRadius(Vector3 worldPos, float radius, List<Vector3Int> result)
    {
        result.Clear();
        Vector3Int minChunk = chunkManager.WorldToChunkPosition(worldPos - Vector3.one * radius);
        Vector3Int maxChunk = chunkManager.WorldToChunkPosition(worldPos + Vector3.one * radius);

        for (int x = minChunk.x; x <= maxChunk.x; x++)
            for (int y = minChunk.y; y <= maxChunk.y; y++)
                for (int z = minChunk.z; z <= maxChunk.z; z++)
                    result.Add(new Vector3Int(x, y, z));
    }

    private void ProcessMeshUpdateQueue()
    {
        if (chunksNeedingMeshUpdate.Count == 0) return;

        int updatesThisFrame = 0;

        // Use a static/cached list to avoid allocation every frame
        List<Chunk> toRemove = toRemoveBuffer;
        toRemove.Clear();

        foreach (var chunk in chunksNeedingMeshUpdate)
        {
            if (updatesThisFrame >= maxMeshUpdatesPerFrame) break;
            if (chunk == null) continue;  // Add null check

            chunk.GenerateMeshAsync();
            toRemove.Add(chunk);
            updatesThisFrame++;
        }

        foreach (var chunk in toRemove)
        {
            chunksNeedingMeshUpdate.Remove(chunk);
        }
    }

    public IEnumerable<Chunk> GetActiveChunks() => chunkManager.ActiveChunks;
    public Chunk GetChunkAt(Vector3Int pos) => chunkManager.GetChunkAt(pos);
    public Vector3Int WorldToChunkPosition(Vector3 pos) => chunkManager.WorldToChunkPosition(pos);
}