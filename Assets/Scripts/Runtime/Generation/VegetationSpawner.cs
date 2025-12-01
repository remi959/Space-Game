using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles vegetation placement for a chunk with natural-looking distribution.
/// Features:
/// - Ground-snapped position jitter for natural placement
/// - Noise-based density for clumping
/// - LOD system with distance-based culling and billboards
/// </summary>
public class VegetationSpawner : MonoBehaviour
{
    [Header("Vegetation Types")]
    [SerializeField] private VegetationAsset[] vegetationAssets;

    [Header("Performance")]
    [SerializeField] private int maxInstancesPerChunk = 100;

    [Header("Natural Distribution")]
    [Tooltip("Random offset applied to break up grid patterns (will snap back to ground)")]
    [SerializeField] private float positionJitter = 0f;

    [Tooltip("Use noise to vary vegetation density across terrain")]
    [SerializeField] private bool useNoiseDensity = true;

    [Tooltip("Scale of the density noise pattern")]
    [SerializeField] private float densityNoiseScale = 15f;

    [Header("Ground Snapping")]
    [Tooltip("Layer mask for ground detection raycasts")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("How far above the jittered position to start the raycast")]
    [SerializeField] private float raycastHeight = 5f;

    [Header("Debug")]
    [SerializeField] private bool logSpawning = false;

    // State
    private Dictionary<Chunk, List<VegetationInstance>> spawnedVegetation =
        new Dictionary<Chunk, List<VegetationInstance>>();

    private Transform vegetationParent;
    private Transform playerTransform;
    private ProceduralPlanet planet;
    private int noiseSeed;

    /// <summary>
    /// Tracks a vegetation instance with LOD data.
    /// </summary>
    private class VegetationInstance
    {
        public GameObject gameObject;
        public Vector3 position;
        public int currentLOD;
        public VegetationAsset asset;
        public float baseScale;
    }

    private void Start()
    {
        EnsureParentExists();
        noiseSeed = Random.Range(0, 100000);

        planet = GetComponent<ProceduralPlanet>();
        if (planet == null)
        {
            planet = GetComponentInParent<ProceduralPlanet>();
        }

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void EnsureParentExists()
    {
        if (vegetationParent == null)
        {
            vegetationParent = new GameObject("Vegetation").transform;
            vegetationParent.SetParent(transform);
        }
    }

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }

    /// <summary>
    /// Spawns vegetation for a chunk with natural distribution.
    /// </summary>
    public void SpawnForChunk(Chunk chunk)
    {
        EnsureParentExists();

        if (spawnedVegetation.ContainsKey(chunk)) return;

        List<VegetationInstance> instances = new List<VegetationInstance>();

        if (!ValidateChunkForSpawning(chunk))
        {
            spawnedVegetation[chunk] = instances;
            return;
        }

        List<Vector3> placedPositions = new List<Vector3>();
        Vector3 planetCenter = planet != null ? planet.PlanetCenter : Vector3.zero;
        float planetRadius = planet != null ? planet.Radius : 0f;

        foreach (var asset in vegetationAssets)
        {
            if (asset == null) continue;

            SpawnVegetationAsset(chunk, asset, instances, placedPositions, planetCenter, planetRadius);

            if (instances.Count >= maxInstancesPerChunk) break;
        }

        spawnedVegetation[chunk] = instances;
    }

    private bool ValidateChunkForSpawning(Chunk chunk)
    {
        List<SurfacePoint> surfacePoints = chunk.SurfacePoints;

        if (surfacePoints == null || surfacePoints.Count == 0)
        {
            if (logSpawning)
                Debug.Log($"[VegetationSpawner] Chunk {chunk.ChunkPosition} has no surface points");
            return false;
        }

        if (logSpawning)
            Debug.Log($"[VegetationSpawner] Chunk {chunk.ChunkPosition} has {surfacePoints.Count} surface points");

        if (vegetationAssets == null || vegetationAssets.Length == 0)
        {
            if (logSpawning)
                Debug.LogWarning("[VegetationSpawner] No vegetation assets assigned!");
            return false;
        }

        return true;
    }

    private void SpawnVegetationAsset(
        Chunk chunk,
        VegetationAsset asset,
        List<VegetationInstance> instances,
        List<Vector3> placedPositions,
        Vector3 planetCenter,
        float planetRadius)
    {
        List<SurfacePoint> surfacePoints = chunk.SurfacePoints;
        SpawnStatistics stats = new SpawnStatistics();

        int attempts = CalculateSpawnAttempts(surfacePoints.Count, asset.density);

        for (int i = 0; i < attempts; i++)
        {
            if (instances.Count >= maxInstancesPerChunk) break;

            SurfacePoint point = surfacePoints[Random.Range(0, surfacePoints.Count)];

            TrySpawnInstance(point, asset, instances, placedPositions, planetCenter, planetRadius, ref stats);
        }

        LogSpawnResults(asset, stats, attempts);
    }

    private int CalculateSpawnAttempts(int surfacePointCount, float density)
    {
        int attempts = Mathf.CeilToInt(surfacePointCount * density);
        return Mathf.Min(attempts, maxInstancesPerChunk);
    }

    private struct SpawnStatistics
    {
        public int successCount;
        public int failedBiome;
        public int failedSlope;
        public int failedDistance;
        public int failedPrefab;
        public int failedNoise;
        public int failedGroundSnap;
    }

    private void TrySpawnInstance(
        SurfacePoint point,
        VegetationAsset asset,
        List<VegetationInstance> instances,
        List<Vector3> placedPositions,
        Vector3 planetCenter,
        float planetRadius,
        ref SpawnStatistics stats)
    {
        // Ground snap with jitter
        if (!TryGetSnappedPosition(point, planetCenter, planetRadius, out Vector3 finalPosition, out Vector3 finalNormal))
        {
            stats.failedGroundSnap++;
            return;
        }

        // Noise density check
        if (useNoiseDensity && !PassesNoiseDensityCheck(finalPosition, asset.density))
        {
            stats.failedNoise++;
            return;
        }

        // Create snapped surface point
        SurfacePoint snappedPoint = CreateSnappedSurfacePoint(point, finalPosition, finalNormal, planetCenter);

        // Placement validation
        if (!asset.CanPlaceAt(snappedPoint))
        {
            if (snappedPoint.slope > asset.maxSlope) stats.failedSlope++;
            else stats.failedBiome++;
            return;
        }

        // Distance check
        if (IsTooCloseToExisting(finalPosition, placedPositions, asset.minDistance))
        {
            stats.failedDistance++;
            return;
        }

        // Get prefab
        GameObject prefab = asset.GetRandomPrefab();
        if (prefab == null)
        {
            stats.failedPrefab++;
            return;
        }

        // Spawn the instance
        VegetationInstance instance = CreateVegetationInstance(prefab, finalPosition, finalNormal, asset);
        instances.Add(instance);
        placedPositions.Add(finalPosition);
        stats.successCount++;
    }

    private bool TryGetSnappedPosition(
        SurfacePoint point,
        Vector3 planetCenter,
        float planetRadius,
        out Vector3 finalPosition,
        out Vector3 finalNormal)
    {
        return PlanetRaycast.FindSurfaceWithJitter(
            point.position,
            point.normal,
            planetCenter,
            planetRadius,
            positionJitter,
            groundLayer,
            raycastHeight,
            out finalPosition,
            out finalNormal);
    }

    private SurfacePoint CreateSnappedSurfacePoint(
        SurfacePoint original,
        Vector3 position,
        Vector3 normal,
        Vector3 planetCenter)
    {
        SurfacePoint snapped = original;
        snapped.position = position;
        snapped.normal = normal;
        snapped.slope = planet != null
            ? PlanetMath.CalculateSlope(position, normal, planetCenter)
            : Vector3.Angle(normal, Vector3.up);
        return snapped;
    }

    private bool IsTooCloseToExisting(Vector3 position, List<Vector3> placedPositions, float minDistance)
    {
        float minDistSqr = minDistance * minDistance;

        foreach (var pos in placedPositions)
        {
            if ((pos - position).sqrMagnitude < minDistSqr)
            {
                return true;
            }
        }

        return false;
    }

    private VegetationInstance CreateVegetationInstance(
        GameObject prefab,
        Vector3 position,
        Vector3 normal,
        VegetationAsset asset)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, vegetationParent);

        ApplyRotation(instance, normal);
        float baseScale = ApplyScale(instance, asset);

        return new VegetationInstance
        {
            gameObject = instance,
            position = position,
            currentLOD = 0,
            asset = asset,
            baseScale = baseScale
        };
    }

    private void ApplyRotation(GameObject instance, Vector3 normal)
    {
        // Align to surface
        instance.transform.up = normal;

        // Random Y rotation
        instance.transform.Rotate(normal, Random.Range(0f, 360f), Space.World);

        // Random tilt for natural look
        float tiltAmount = Random.Range(-5f, 5f);
        Vector3 tiltAxis = Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized;

        if (tiltAxis.sqrMagnitude > 0.01f)
        {
            instance.transform.Rotate(tiltAxis, tiltAmount, Space.World);
        }
    }

    private float ApplyScale(GameObject instance, VegetationAsset asset)
    {
        float baseScale = asset.GetRandomScale();
        float scaleVariation = Random.Range(0.95f, 1.05f);

        instance.transform.localScale = new Vector3(
            baseScale * scaleVariation,
            baseScale,
            baseScale * scaleVariation
        );

        return baseScale;
    }

    private void LogSpawnResults(VegetationAsset asset, SpawnStatistics stats, int attempts)
    {
        if (!logSpawning) return;

        Debug.Log($"[VegetationSpawner] {asset.vegetationName}: Spawned {stats.successCount}/{attempts} " +
                  $"(Biome: {stats.failedBiome}, Slope: {stats.failedSlope}, Distance: {stats.failedDistance}, " +
                  $"Noise: {stats.failedNoise}, GroundSnap: {stats.failedGroundSnap}, Prefab: {stats.failedPrefab})");
    }

    private bool PassesNoiseDensityCheck(Vector3 position, float baseDensity)
    {
        // REPLACED: Manual seed offset -> HashUtility.GetSeedOffset
        Vector3 samplePos = position / densityNoiseScale + HashUtility.GetSeedOffset(noiseSeed);
        float noiseValue = NoiseGenerator.Simplex3D(samplePos.x, samplePos.y, samplePos.z);

        noiseValue = (noiseValue + 1f) * 0.5f;
        float threshold = 1f - Mathf.Clamp01(baseDensity);

        return noiseValue > threshold * 0.5f;
    }

    public void RemoveForChunk(Chunk chunk)
    {
        if (!spawnedVegetation.TryGetValue(chunk, out List<VegetationInstance> instances))
            return;

        foreach (var instance in instances)
        {
            if (instance.gameObject != null)
            {
                Destroy(instance.gameObject);
            }
        }

        spawnedVegetation.Remove(chunk);
    }
}