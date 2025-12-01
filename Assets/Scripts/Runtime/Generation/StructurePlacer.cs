using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles placement of structures (buildings, landmarks, etc) on procedural terrain.
/// Uses SurfaceSampler for surface detection.
/// </summary>
public class StructurePlacer : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    [SerializeField] private DensityProvider densityProvider;
    [SerializeField] private SurfaceSampler surfaceSampler;
    [SerializeField] private BiomeManager biomeManager;

    [Header("=== STRUCTURES ===")]
    [SerializeField] private StructureAsset[] structures;

    [Header("=== PLACEMENT SETTINGS ===")]
    [SerializeField] private int maxPlacementAttempts = 30;
    [SerializeField] private LayerMask terrainLayer = ~0;
    [SerializeField] private float raycastHeight = 50f;

    [Header("=== REGION SETTINGS ===")]
    [SerializeField] private bool autoPlaceOnChunkGeneration = false;
    [SerializeField] private int structuresPerChunk = 1;

    [Header("=== DEBUG ===")]
    [SerializeField] private bool logPlacements = false;
    [SerializeField] private bool showPlacementGizmos = false;

    // State
    private List<PlacedStructure> allPlacedStructures = new List<PlacedStructure>();
    private Dictionary<Chunk, List<PlacedStructure>> structuresPerChunkMap = new();
    private Transform structureParent;

    public class PlacedStructure
    {
        public GameObject gameObject;
        public StructureAsset asset;
        public Vector3 position;
        public SurfacePoint surfacePoint;
    }

    public void Initialize(DensityProvider density, SurfaceSampler sampler, BiomeManager biomes)
    {
        densityProvider = density;
        surfaceSampler = sampler;
        biomeManager = biomes;
    }

    private void Start() => EnsureParentExists();

    private void EnsureParentExists()
    {
        if (structureParent == null)
        {
            structureParent = new GameObject("Structures").transform;
            structureParent.SetParent(transform);
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void PlaceStructuresInRegion(Vector3 regionCenter, float regionRadius)
    {
        EnsureParentExists();

        foreach (var structure in structures)
        {
            if (structure == null) continue;
            if (Random.value > structure.spawnChance) continue;

            SurfacePoint? location = FindSuitableLocation(regionCenter, regionRadius, structure);

            if (location.HasValue)
            {
                PlaceStructure(structure, location.Value);
            }
        }
    }

    public void PlaceStructuresForChunk(Chunk chunk)
    {
        if (!autoPlaceOnChunkGeneration) return;
        if (structures == null || structures.Length == 0) return;
        if (structuresPerChunkMap.ContainsKey(chunk)) return;

        EnsureParentExists();

        List<PlacedStructure> chunkStructures = new List<PlacedStructure>();
        Bounds chunkBounds = chunk.GetWorldBounds();

        int placedCount = 0;
        int attempts = 0;
        int maxTotalAttempts = structuresPerChunk * maxPlacementAttempts;

        while (placedCount < structuresPerChunk && attempts < maxTotalAttempts)
        {
            StructureAsset structure = structures[Random.Range(0, structures.Length)];
            if (structure == null || Random.value > structure.spawnChance)
            {
                attempts++;
                continue;
            }

            SurfacePoint? location = FindSuitableLocationInBounds(chunkBounds, structure);

            if (location.HasValue)
            {
                PlacedStructure placed = PlaceStructure(structure, location.Value);
                if (placed != null)
                {
                    chunkStructures.Add(placed);
                    placedCount++;
                }
            }

            attempts++;
        }

        structuresPerChunkMap[chunk] = chunkStructures;

        if (logPlacements)
        {
            Debug.Log($"[StructurePlacer] Placed {placedCount} structures in chunk {chunk.ChunkPosition}");
        }
    }

    public void RemoveStructuresForChunk(Chunk chunk)
    {
        if (!structuresPerChunkMap.TryGetValue(chunk, out List<PlacedStructure> chunkStructures))
            return;

        foreach (var structure in chunkStructures)
        {
            if (structure.gameObject != null)
            {
                Destroy(structure.gameObject);
            }
            allPlacedStructures.Remove(structure);
        }

        structuresPerChunkMap.Remove(chunk);
    }

    public PlacedStructure TryPlaceStructureAt(StructureAsset structure, Vector3 worldPosition)
    {
        EnsureParentExists();

        SurfacePoint? surfacePoint = GetSurfacePointAt(worldPosition);

        if (!surfacePoint.HasValue)
        {
            if (logPlacements) Debug.Log($"[StructurePlacer] No surface found at {worldPosition}");
            return null;
        }

        if (!structure.CanPlaceAt(surfacePoint.Value))
        {
            if (logPlacements) Debug.Log($"[StructurePlacer] Surface not suitable at {worldPosition}");
            return null;
        }

        if (!CheckDistanceConstraints(surfacePoint.Value.position, structure))
        {
            if (logPlacements) Debug.Log($"[StructurePlacer] Distance constraint failed at {worldPosition}");
            return null;
        }

        return PlaceStructure(structure, surfacePoint.Value);
    }

    // =========================================================================
    // LOCATION FINDING
    // =========================================================================

    private SurfacePoint? FindSuitableLocation(Vector3 center, float radius, StructureAsset structure)
    {
        Vector3 planetCenter = densityProvider?.Center ?? Vector3.zero;

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            // REPLACED: GetRandomPointInRadius -> PlanetMath.GetRandomTangentOffset
            Vector3 testPoint = center + PlanetMath.GetRandomTangentOffset(center, planetCenter, radius);
            SurfacePoint? surfacePoint = GetSurfacePointAt(testPoint);

            if (!surfacePoint.HasValue) continue;
            if (!structure.CanPlaceAt(surfacePoint.Value)) continue;
            if (!CheckDistanceConstraints(surfacePoint.Value.position, structure)) continue;

            return surfacePoint;
        }

        return null;
    }

    private SurfacePoint? FindSuitableLocationInBounds(Bounds bounds, StructureAsset structure)
    {
        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            Vector3 testPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            SurfacePoint? surfacePoint = GetSurfacePointAt(testPoint);

            if (!surfacePoint.HasValue) continue;
            if (!structure.CanPlaceAt(surfacePoint.Value)) continue;
            if (!CheckDistanceConstraints(surfacePoint.Value.position, structure)) continue;

            return surfacePoint;
        }

        return null;
    }

    // REMOVED: GetRandomPointInRadius - now using PlanetMath.GetRandomTangentOffset

    private SurfacePoint? GetSurfacePointAt(Vector3 worldPosition)
    {
        if (surfaceSampler != null)
        {
            SurfacePoint result = surfaceSampler.GetSurfaceAtRaycast(worldPosition, terrainLayer, raycastHeight);
            return result.isValid ? result : null;
        }

        // REPLACED: Manual raycast setup -> PlanetRaycast.FindSurface
        if (densityProvider != null)
        {
            SurfacePoint result = PlanetRaycast.FindSurface(
                worldPosition,
                densityProvider.Center,
                densityProvider.Radius,
                terrainLayer,
                raycastHeight
            );

            // Add biome info if available
            if (result.isValid && biomeManager != null)
            {
                Vector3 normalizedPos = PlanetMath.GetUpDirection(result.position, densityProvider.Center);
                var weights = biomeManager.GetBiomesAt(normalizedPos, densityProvider.Seed);
                if (weights.Length > 0) result.biome = weights[0].biome;
            }

            return result.isValid ? result : null;
        }

        return null;
    }

    // =========================================================================
    // DISTANCE CONSTRAINTS
    // =========================================================================

    private bool CheckDistanceConstraints(Vector3 position, StructureAsset structure)
    {
        foreach (var placed in allPlacedStructures)
        {
            if (placed.gameObject == null) continue;

            float distance = Vector3.Distance(position, placed.position);

            if (placed.asset == structure && distance < structure.minDistanceFromSame)
                return false;

            if (distance < structure.minDistanceFromOthers)
                return false;

            if (distance < placed.asset.minDistanceFromOthers)
                return false;
        }

        return true;
    }

    // =========================================================================
    // STRUCTURE PLACEMENT
    // =========================================================================

    private PlacedStructure PlaceStructure(StructureAsset structure, SurfacePoint surfacePoint)
    {
        GameObject prefab = structure.GetRandomPrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"[StructurePlacer] No prefab for structure {structure.structureName}");
            return null;
        }

        Vector3 position = CalculateStructurePosition(surfacePoint, structure);
        Quaternion rotation = CalculateStructureRotation(surfacePoint, structure);

        return InstantiateStructure(prefab, position, rotation, structure, surfacePoint);
    }

    private Vector3 CalculateStructurePosition(SurfacePoint surfacePoint, StructureAsset structure)
    {
        Vector3 upDir = GetUpDirection(surfacePoint.position);
        return surfacePoint.position + upDir * structure.heightOffset;
    }

    private Vector3 GetUpDirection(Vector3 position)
    {
        return densityProvider != null
            ? PlanetMath.GetUpDirection(position, densityProvider.Center)
            : Vector3.up;
    }

    private Quaternion CalculateStructureRotation(SurfacePoint surfacePoint, StructureAsset structure)
    {
        Vector3 upDir = GetUpDirection(surfacePoint.position);
        Quaternion rotation;

        if (structure.alignToSurface)
        {
            rotation = CalculateSurfaceAlignedRotation(surfacePoint.normal, upDir, structure.maxTiltAngle);
        }
        else
        {
            rotation = Quaternion.FromToRotation(Vector3.up, upDir);
        }

        if (structure.randomYRotation)
        {
            rotation *= Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        return rotation;
    }

    private Quaternion CalculateSurfaceAlignedRotation(Vector3 surfaceNormal, Vector3 upDir, float maxTiltAngle)
    {
        Vector3 adjustedUp = surfaceNormal;
        float angle = Vector3.Angle(surfaceNormal, upDir);

        if (angle > maxTiltAngle)
        {
            adjustedUp = Vector3.Slerp(upDir, surfaceNormal, maxTiltAngle / angle);
        }

        return Quaternion.FromToRotation(Vector3.up, adjustedUp);
    }

    private PlacedStructure InstantiateStructure(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        StructureAsset structure,
        SurfacePoint surfacePoint)
    {
        GameObject instance = Instantiate(prefab, position, rotation, structureParent);
        instance.name = $"{structure.structureName}_{allPlacedStructures.Count}";
        instance.transform.localScale = Vector3.one * structure.GetRandomScale();

        PlacedStructure placed = new PlacedStructure
        {
            gameObject = instance,
            asset = structure,
            position = position,
            surfacePoint = surfacePoint
        };

        allPlacedStructures.Add(placed);

        LogPlacement(structure, position, surfacePoint);

        return placed;
    }

    private void LogPlacement(StructureAsset structure, Vector3 position, SurfacePoint surfacePoint)
    {
        if (!logPlacements) return;

        Debug.Log($"[StructurePlacer] Placed {structure.structureName} at {position} " +
                  $"(slope: {surfacePoint.slope:F1}°, height: {surfacePoint.height:F1})");
    }

    // =========================================================================
    // CLEANUP
    // =========================================================================

    public void ClearAllStructures()
    {
        foreach (var structure in allPlacedStructures)
        {
            if (structure.gameObject != null)
            {
                Destroy(structure.gameObject);
            }
        }

        allPlacedStructures.Clear();
        structuresPerChunkMap.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showPlacementGizmos) return;

        Gizmos.color = Color.yellow;
        foreach (var structure in allPlacedStructures)
        {
            if (structure.gameObject != null)
            {
                Gizmos.DrawWireSphere(structure.position, 2f);
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(structure.position, structure.asset.minDistanceFromOthers);
            }
        }
    }

    public List<PlacedStructure> AllPlacedStructures => allPlacedStructures;
    public int PlacedCount => allPlacedStructures.Count;
}