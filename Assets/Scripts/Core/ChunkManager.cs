using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages chunk loading/unloading with shared boundary support.
/// Uses SharedBoundaryManager to ensure seamless mesh connections between chunks.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("Settings")]
    public ShapeSettings settings;
    public CaveSettings caveSettings;
    public Material terrainMaterial;
    public Transform player;

    [Header("Loading Parameters")]
    [Tooltip("Distance in world units to load chunks")]
    public float loadDistance = 80f;

    [Tooltip("Distance in world units to unload chunks (should be > loadDistance)")]
    public float unloadDistance = 120f;

    [Tooltip("How often to check for new chunks to load (seconds)")]
    public float chunkSearchInterval = 0.2f;

    public int chunksPerFrame = 2;
    public int meshesPerFrame = 2;

    [Header("Debug")]
    public bool showChunkBounds = false;
    public bool showLoadRadius = false;
    public bool logPerformanceStats = false;

    // Core systems
    private SharedBoundaryManager boundaryManager;
    private DensityGenerator densityGenerator;
    private MarchingCubes meshGenerator;

    // Chunk storage
    private Dictionary<Vector3Int, PlanetChunk> activeChunks = new();

    // Pending chunks tracking - HashSet for O(1) contains check
    private HashSet<Vector3Int> pendingChunkCoords = new();
    
    // Sorted load queue - only rebuilt when needed
    private List<ChunkLoadRequest> sortedLoadQueue = new();
    private bool loadQueueDirty = false;
    
    // Track chunks currently being processed to prevent duplicate work
    private HashSet<Vector3Int> chunksInProgress = new();

    // Mesh generation queue
    private Queue<PlanetChunk> chunksToMesh = new();

    // Cached player position for priority calculations
    private Vector3 lastPlayerPos;
    private Vector3Int lastPlayerChunk;
    
    // Timer for chunk search
    private float chunkSearchTimer = 0f;

    // Performance tracking
    private int totalChunksGenerated = 0;
    private int totalChunksUnloaded = 0;

    void Start()
    {
        ValidateSettings();
        
        boundaryManager = new SharedBoundaryManager(settings.ChunkResolution);
        densityGenerator = new DensityGenerator(settings, caveSettings);
        
        // Initialize mesh generator with estimated capacity
        bool hasCaves = caveSettings != null && caveSettings.NoiseLayers?.Length > 0;
        meshGenerator = new MarchingCubes(settings.ChunkResolution, hasCaves);

        lastPlayerPos = player.position;
        lastPlayerChunk = PositionConverter.WorldToChunkCoord(player.position, settings.ChunkSize);
        
        // Initial chunk search
        SearchForChunksToLoad();
    }

    void ValidateSettings()
    {
        if (unloadDistance <= loadDistance)
        {
            unloadDistance = loadDistance + settings.ChunkSize * 2;
            Debug.LogWarning($"Unload distance was <= load distance. Adjusted to {unloadDistance}");
        }

        if (player == null)
        {
            Debug.LogError("ChunkManager: Player transform not assigned!");
            enabled = false;
        }
    }

    void Update()
    {
        Vector3 playerPos = player.position;
        Vector3Int currentPlayerChunk = PositionConverter.WorldToChunkCoord(playerPos, settings.ChunkSize);
        
        // Check if player moved to a new chunk
        bool playerMovedChunk = currentPlayerChunk != lastPlayerChunk;
        
        // Periodic chunk search or when player moves chunks
        chunkSearchTimer += Time.deltaTime;
        if (chunkSearchTimer >= chunkSearchInterval || playerMovedChunk)
        {
            chunkSearchTimer = 0f;
            lastPlayerPos = playerPos;
            lastPlayerChunk = currentPlayerChunk;
            
            SearchForChunksToLoad();
            UnloadDistantChunks();
            
            // Mark queue dirty if player moved significantly
            if (playerMovedChunk)
            {
                loadQueueDirty = true;
            }
        }
        
        // Update priorities if player moved significantly
        float moveDelta = Vector3.Distance(playerPos, lastPlayerPos);
        if (moveDelta > settings.ChunkSize * 0.5f)
        {
            loadQueueDirty = true;
            lastPlayerPos = playerPos;
        }

        ProcessGenerationQueue();
        ProcessMeshQueue();
    }

    void SearchForChunksToLoad()
    {
        Vector3 playerPos = player.position;
        Vector3Int playerChunk = PositionConverter.WorldToChunkCoord(playerPos, settings.ChunkSize);
        int searchRadius = Mathf.CeilToInt(loadDistance / settings.ChunkSize) + 1;

        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                for (int z = -searchRadius; z <= searchRadius; z++)
                {
                    Vector3Int coord = playerChunk + new Vector3Int(x, y, z);

                    // Skip if already loaded, pending, or in progress
                    if (activeChunks.ContainsKey(coord)) continue;
                    if (pendingChunkCoords.Contains(coord)) continue;
                    if (chunksInProgress.Contains(coord)) continue;
                    
                    // Skip if chunk shouldn't exist (outside planet shell)
                    if (!SphericalChunkSampler.ShouldSampleChunk(coord, settings, caveSettings)) continue;

                    // Check spherical distance
                    Vector3 chunkCenter = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
                    float distance = Vector3.Distance(chunkCenter, playerPos);

                    if (distance <= loadDistance)
                    {
                        pendingChunkCoords.Add(coord);
                        loadQueueDirty = true;
                    }
                }
            }
        }
    }

    void UnloadDistantChunks()
    {
        Vector3 playerPos = player.position;
        List<Vector3Int> toUnload = null;

        foreach (var kvp in activeChunks)
        {
            Vector3Int coord = kvp.Key;
            Vector3 chunkCenter = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
            float distance = Vector3.Distance(chunkCenter, playerPos);

            if (distance > unloadDistance)
            {
                toUnload ??= new List<Vector3Int>();
                toUnload.Add(coord);
            }
        }

        if (toUnload != null)
        {
            foreach (var coord in toUnload)
            {
                UnloadChunk(coord);
            }
        }
    }

    void RebuildSortedLoadQueue()
    {
        if (!loadQueueDirty && sortedLoadQueue.Count > 0) return;
        
        Vector3 playerPos = player.position;
        
        sortedLoadQueue.Clear();
        
        // Ensure capacity to avoid reallocation
        if (sortedLoadQueue.Capacity < pendingChunkCoords.Count)
        {
            sortedLoadQueue.Capacity = pendingChunkCoords.Count;
        }

        foreach (var coord in pendingChunkCoords)
        {
            // Double-check chunk isn't already loaded (could have loaded since added to pending)
            if (activeChunks.ContainsKey(coord) || chunksInProgress.Contains(coord))
            {
                continue;
            }

            Vector3 chunkCenter = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
            float distance = Vector3.Distance(chunkCenter, playerPos);
            
            // Remove from pending if now out of range
            if (distance > loadDistance)
            {
                continue;
            }

            sortedLoadQueue.Add(new ChunkLoadRequest
            {
                Coord = coord,
                Priority = distance
            });
        }

        // Sort by priority (closest first)
        sortedLoadQueue.Sort();
        
        // Clean up pending set to match sorted queue
        pendingChunkCoords.Clear();
        foreach (var request in sortedLoadQueue)
        {
            pendingChunkCoords.Add(request.Coord);
        }

        loadQueueDirty = false;
    }

    void ProcessGenerationQueue()
    {
        // Rebuild sorted queue if needed
        if (loadQueueDirty || (sortedLoadQueue.Count == 0 && pendingChunkCoords.Count > 0))
        {
            RebuildSortedLoadQueue();
        }

        if (sortedLoadQueue.Count == 0) return;

        int processed = 0;
        int index = 0;

        while (processed < chunksPerFrame && index < sortedLoadQueue.Count)
        {
            var request = sortedLoadQueue[index];
            Vector3Int coord = request.Coord;

            // Skip if already loaded (shouldn't happen, but safety check)
            if (activeChunks.ContainsKey(coord))
            {
                RemoveFromQueues(coord, index);
                continue;
            }

            // Mark as in progress
            chunksInProgress.Add(coord);
            pendingChunkCoords.Remove(coord);

            // Create and generate chunk
            var chunk = new PlanetChunk(
                coord,
                settings.ChunkResolution,
                settings.ChunkSize,
                boundaryManager
            );

            chunk.GenerateDensityField(densityGenerator);

            activeChunks[coord] = chunk;
            chunksToMesh.Enqueue(chunk);

            chunksInProgress.Remove(coord);
            totalChunksGenerated++;

            // Remove from sorted queue
            sortedLoadQueue.RemoveAt(index);
            processed++;
            
            // Don't increment index since we removed current element
        }

        if (logPerformanceStats && processed > 0)
        {
            Debug.Log($"[ChunkManager] Generated {processed} chunks. Pending: {pendingChunkCoords.Count}, Queue: {sortedLoadQueue.Count}");
        }
    }

    void RemoveFromQueues(Vector3Int coord, int sortedQueueIndex)
    {
        pendingChunkCoords.Remove(coord);
        if (sortedQueueIndex >= 0 && sortedQueueIndex < sortedLoadQueue.Count)
        {
            sortedLoadQueue.RemoveAt(sortedQueueIndex);
        }
    }

    void ProcessMeshQueue()
    {
        for (int i = 0; i < meshesPerFrame && chunksToMesh.Count > 0; i++)
        {
            var chunk = chunksToMesh.Dequeue();

            chunk.GenerateMesh(meshGenerator);
            
            // Only create GameObject if chunk has geometry
            if (chunk.Mesh != null && chunk.Mesh.vertexCount > 0)
            {
                chunk.CreateGameObject(terrainMaterial, transform);
            }
        }
    }

    void UnloadChunk(Vector3Int coord)
    {
        if (activeChunks.TryGetValue(coord, out var chunk))
        {
            chunk.Destroy();
            activeChunks.Remove(coord);
            totalChunksUnloaded++;
        }

        // Also remove from pending if present
        pendingChunkCoords.Remove(coord);
    }

    // bool ShouldChunkExist(Vector3Int coord)
    // {
    //     Vector3 center = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
    //     float dist = center.magnitude;
    //     float margin = settings.ChunkSize * 2;

    //     return dist > settings.PlanetRadius - settings.MaxTerrainDepth - margin &&
    //            dist < settings.PlanetRadius + settings.MaxTerrainHeight + margin;
    // }

    #region Public API

    /// <summary>
    /// Forces regeneration of a specific chunk (e.g., after terrain modification)
    /// </summary>
    public void RegenerateChunk(Vector3Int coord)
    {
        if (activeChunks.TryGetValue(coord, out var chunk))
        {
            chunk.GenerateDensityField(densityGenerator);
            chunksToMesh.Enqueue(chunk);
        }
    }

    /// <summary>
    /// Regenerates all chunks within a radius of a world position.
    /// Useful after terrain modifications.
    /// </summary>
    public void RegenerateChunksInRadius(Vector3 worldPos, float radius)
    {
        Vector3Int centerChunk = PositionConverter.WorldToChunkCoord(worldPos, settings.ChunkSize);
        int chunkRadius = Mathf.CeilToInt(radius / settings.ChunkSize) + 1;

        for (int x = -chunkRadius; x <= chunkRadius; x++)
        {
            for (int y = -chunkRadius; y <= chunkRadius; y++)
            {
                for (int z = -chunkRadius; z <= chunkRadius; z++)
                {
                    Vector3Int coord = centerChunk + new Vector3Int(x, y, z);
                    RegenerateChunk(coord);
                }
            }
        }
    }

    /// <summary>
    /// Gets a chunk at the specified coordinate, or null if not loaded.
    /// </summary>
    public PlanetChunk GetChunk(Vector3Int coord)
    {
        return activeChunks.TryGetValue(coord, out var chunk) ? chunk : null;
    }

    /// <summary>
    /// Checks if a chunk is currently loaded.
    /// </summary>
    public bool IsChunkLoaded(Vector3Int coord)
    {
        return activeChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Checks if a chunk is pending generation.
    /// </summary>
    public bool IsChunkPending(Vector3Int coord)
    {
        return pendingChunkCoords.Contains(coord);
    }

    /// <summary>
    /// Forces an immediate priority update (useful after teleportation).
    /// </summary>
    public void ForceQueueUpdate()
    {
        loadQueueDirty = true;
        chunkSearchTimer = chunkSearchInterval; // Trigger immediate search
    }

    /// <summary>
    /// Gets current chunk loading statistics.
    /// </summary>
    public ChunkManagerStats GetStats()
    {
        return new ChunkManagerStats
        {
            ActiveChunks = activeChunks.Count,
            PendingChunks = pendingChunkCoords.Count,
            QueuedForMesh = chunksToMesh.Count,
            InProgress = chunksInProgress.Count,
            TotalGenerated = totalChunksGenerated,
            TotalUnloaded = totalChunksUnloaded
        };
    }

    #endregion

    void OnDestroy()
    {
        foreach (var chunk in activeChunks.Values)
        {
            chunk.Destroy();
        }
        activeChunks.Clear();
        pendingChunkCoords.Clear();
        sortedLoadQueue.Clear();
        boundaryManager?.Clear();
    }

    #region Debug Visualization

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (settings == null) return;

        if (showChunkBounds)
        {
            // Active chunks in green
            Gizmos.color = Color.green;
            foreach (var kvp in activeChunks)
            {
                Vector3 center = PositionConverter.ChunkCoordToWorldCenter(kvp.Key, settings.ChunkSize);
                Gizmos.DrawWireCube(center, Vector3.one * settings.ChunkSize);
            }

            // Chunks in progress in yellow
            Gizmos.color = Color.yellow;
            foreach (var coord in chunksInProgress)
            {
                Vector3 center = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
                Gizmos.DrawWireCube(center, Vector3.one * settings.ChunkSize * 0.9f);
            }

            // Pending chunks in cyan (only first 50 to avoid performance issues)
            Gizmos.color = Color.cyan;
            int count = 0;
            foreach (var coord in pendingChunkCoords)
            {
                if (count++ > 50) break;
                Vector3 center = PositionConverter.ChunkCoordToWorldCenter(coord, settings.ChunkSize);
                Gizmos.DrawWireCube(center, Vector3.one * settings.ChunkSize * 0.8f);
            }
        }

        if (showLoadRadius && player != null)
        {
            // Load distance sphere (green)
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(player.position, loadDistance);

            // Unload distance sphere (red)
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(player.position, unloadDistance);
        }
    }

    void OnGUI()
    {
        if (!logPerformanceStats) return;

        var stats = GetStats();
        GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 150));
        GUILayout.BeginVertical("box");
        GUILayout.Label("Chunk Manager Stats");
        GUILayout.Label($"Active: {stats.ActiveChunks}");
        GUILayout.Label($"Pending: {stats.PendingChunks}");
        GUILayout.Label($"Mesh Queue: {stats.QueuedForMesh}");
        GUILayout.Label($"In Progress: {stats.InProgress}");
        GUILayout.Label($"Total Generated: {stats.TotalGenerated}");
        GUILayout.Label($"Total Unloaded: {stats.TotalUnloaded}");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion

    #region Helper Classes

    private class ChunkLoadRequest : IComparable<ChunkLoadRequest>
    {
        public Vector3Int Coord;
        public float Priority;

        public int CompareTo(ChunkLoadRequest other)
        {
            return Priority.CompareTo(other.Priority);
        }
    }

    #endregion
}

/// <summary>
/// Statistics about chunk manager state.
/// </summary>
public struct ChunkManagerStats
{
    public int ActiveChunks;
    public int PendingChunks;
    public int QueuedForMesh;
    public int InProgress;
    public int TotalGenerated;
    public int TotalUnloaded;
}