# Procedural Planet Generation: Learning Roadmap

## A Step-by-Step Guide to Mastering Voxel World Generation

This guide is designed to teach you the **concepts** behind procedural generation, not just give you code to copy. Each section includes:

- **Concept**: What you're learning and why it matters
- **Theory**: How it works mathematically/algorithmically
- **Implementation**: Concrete steps to add the feature
- **Exercises**: Challenges to deepen your understanding
- **Debug Tips**: How to visualize and troubleshoot

---

# Table of Contents

1. [Foundation: Understanding What You Have](#phase-1-foundation)
2. [Phase 2: Terrain Variation with Multiple Noise Layers](#phase-2-terrain-variation)
3. [Phase 3: Biome System](#phase-3-biome-system)
4. [Phase 4: Cave Generation](#phase-4-cave-generation)
5. [Phase 5: Surface Detection](#phase-5-surface-detection)
6. [Phase 6: Vegetation Placement](#phase-6-vegetation-placement)
7. [Phase 7: Structure Generation](#phase-7-structure-generation)
8. [Phase 8: Level of Detail (LOD)](#phase-8-level-of-detail)
9. [Phase 9: Saving and Loading](#phase-9-saving-and-loading)
10. [Phase 10: Performance Optimization](#phase-10-performance-optimization)

---

# Phase 1: Foundation

## Understanding What You Already Have

Before adding features, you must deeply understand the current system.

### Concept: The Density Field

Your planet is defined by a **density function** — a mathematical function that takes any point in 3D space and returns a number:

```
density(point) → float

If result > 0: This point is INSIDE terrain (solid)
If result < 0: This point is OUTSIDE terrain (air)  
If result = 0: This point is ON the surface
```

**This is the most important concept in voxel terrain.** Every feature you add will modify this density function.

### Exercise 1.1: Visualize the Density Field

Create a debug script that draws the density values:

```csharp
// Add to ProceduralPlanet.cs
private void OnDrawGizmosSelected()
{
    // Draw density samples in a grid
    float step = 5f;
    float range = planetRadius * 1.5f;
    
    for (float x = -range; x <= range; x += step)
    {
        for (float y = -range; y <= range; y += step)
        {
            for (float z = -range; z <= range; z += step)
            {
                Vector3 point = transform.position + new Vector3(x, y, z);
                float density = GetDensityAt(point);
                
                // Color based on density
                if (Mathf.Abs(density) < 2f) // Near surface
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, 0.5f);
                }
            }
        }
    }
}
```

**What you should see:** Yellow spheres forming a shell around your planet where the surface is.

### Exercise 1.2: Modify the Density Function

Try these modifications to `GetDensityAt()` and observe the results:

1. **Remove all noise** — You should get a perfect sphere
2. **Double the noise strength** — More extreme terrain
3. **Change `planetRadius - distanceFromCenter` to `distanceFromCenter - planetRadius`** — Inverted! Hollow sphere with terrain on inside

### Concept: The Marching Cubes Pipeline

```
Density Array → Marching Cubes → Vertices + Triangles → Mesh → Collider
     ↑                                                           ↓
  You control                                              Player interacts
  this part                                                with this
```

Every time you modify the density field, the mesh must be regenerated.

### Exercise 1.3: Trace the Code Path

Follow these function calls and write comments explaining each step:

1. `ProceduralPlanet.Start()` → Where does chunk generation begin?
2. `Chunk.GenerateDensities()` → How does it fill the density array?
3. `Chunk.GenerateMesh()` → How does MarchingCubes convert density to triangles?
4. `MarchingCubes.MarchCube()` → How does a single cube become triangles?

---

# Phase 2: Terrain Variation

## Adding Multiple Noise Layers

### Concept: Layered Noise

Real terrain has features at multiple scales:
- **Continental** scale: Major landmasses
- **Mountain** scale: Mountain ranges
- **Hill** scale: Rolling hills
- **Detail** scale: Rocks) and bumps

Each scale is a separate noise layer that gets combined.

### Theory: Noise Combination Methods

```
ADDITIVE (current):
    density = base + noise1 + noise2 + noise3
    Result: Each layer adds height
    
MULTIPLICATIVE:
    density = base + noise1 * noise2
    Result: One layer modulates another
    
MASKED:
    density = base + noise1 * mask(position)
    Result: Noise only appears in certain areas
```

### Implementation Steps

#### Step 2.1: Create a Noise Layer System

```csharp
// Create new file: NoiseLayer.cs

using UnityEngine;

[System.Serializable]
public class NoiseLayer
{
    public enum BlendMode { Add, Multiply, Max, Min }
    
    public bool enabled = true;
    public BlendMode blendMode = BlendMode.Add;
    
    [Header("Noise Settings")]
    public NoiseGenerator.NoiseSettings settings = new NoiseGenerator.NoiseSettings();
    
    [Header("Filters")]
    [Tooltip("Only apply this noise above this base density")]
    public bool useFloor = false;
    public float floorValue = 0f;
    
    [Tooltip("Use first layer as mask for this layer")]
    public bool useFirstLayerAsMask = false;
    
    /// <summary>
    /// Evaluates this noise layer at a point.
    /// </summary>
    public float Evaluate(Vector3 point, int seed, float firstLayerValue = 0f)
    {
        if (!enabled) return 0f;
        
        float noiseValue = NoiseGenerator.Sample3D(point, settings, seed);
        
        // Apply floor filter
        if (useFloor)
        {
            noiseValue = Mathf.Max(0, noiseValue - floorValue);
        }
        
        // Apply mask from first layer
        if (useFirstLayerAsMask && firstLayerValue > 0)
        {
            noiseValue *= firstLayerValue;
        }
        
        return noiseValue;
    }
}
```

#### Step 2.2: Update ProceduralPlanet to Use Layers

```csharp
// In ProceduralPlanet.cs, replace the noise settings with:

[Header("=== TERRAIN LAYERS ===")]
[SerializeField] private NoiseLayer[] noiseLayers = new NoiseLayer[]
{
    new NoiseLayer { /* Continental */ },
    new NoiseLayer { /* Mountains */ },
    new NoiseLayer { /* Detail */ }
};

// Update GetDensityAt():
public float GetDensityAt(Vector3 worldPos)
{
    Vector3 toPoint = worldPos - PlanetCenter;
    float distanceFromCenter = toPoint.magnitude;
    float baseDensity = planetRadius - distanceFromCenter;
    
    // Only apply noise near surface
    float surfaceBlend = Mathf.Clamp01(1f - Mathf.Abs(baseDensity) / 20f);
    if (surfaceBlend < 0.01f) return baseDensity;
    
    Vector3 samplePoint = toPoint.normalized * planetRadius;
    float totalNoise = 0f;
    float firstLayerValue = 0f;
    
    for (int i = 0; i < noiseLayers.Length; i++)
    {
        NoiseLayer layer = noiseLayers[i];
        float layerValue = layer.Evaluate(samplePoint, seed + i, firstLayerValue);
        
        if (i == 0) firstLayerValue = layerValue;
        
        switch (layer.blendMode)
        {
            case NoiseLayer.BlendMode.Add:
                totalNoise += layerValue;
                break;
            case NoiseLayer.BlendMode.Multiply:
                totalNoise *= (1 + layerValue);
                break;
            case NoiseLayer.BlendMode.Max:
                totalNoise = Mathf.Max(totalNoise, layerValue);
                break;
            case NoiseLayer.BlendMode.Min:
                totalNoise = Mathf.Min(totalNoise, layerValue);
                break;
        }
    }
    
    return baseDensity + totalNoise * surfaceBlend;
}
```

### Exercise 2.1: Design Your Own Terrain

Create three layers:
1. **Continents**: Low frequency, high strength — creates major land masses
2. **Mountains**: Medium frequency, masked by continents — only appear on land
3. **Detail**: High frequency, low strength — small bumps everywhere

### Exercise 2.2: Create Specific Features

Try to create these terrain types by adjusting noise layers:
- **Flat plains** with occasional mountains
- **Jagged, spiky terrain**
- **Smooth rolling hills**
- **Crater-like formations** (hint: use inverted noise)

---

# Phase 3: Biome System

## Different Terrain Types Based on Location

### Concept: What is a Biome?

A biome is a region with distinct characteristics:
- Different terrain shape (mountains vs. plains)
- Different colors/textures
- Different vegetation
- Different structures

### Theory: Biome Selection

Biomes are selected using **another noise function** separate from terrain:

```
biomeNoise(position) → value 0.0 to 1.0

0.0 - 0.3 → Desert biome
0.3 - 0.6 → Forest biome  
0.6 - 1.0 → Mountain biome
```

### Implementation Steps

#### Step 3.1: Create Biome Definition

```csharp
// Create new file: Biome.cs

using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Procedural Planet/Biome")]
public class Biome : ScriptableObject
{
    [Header("Identification")]
    public string biomeName = "Default";
    public Color debugColor = Color.green;
    
    [Header("Terrain Shape")]
    [Tooltip("How much this biome affects base terrain height")]
    public float heightMultiplier = 1f;
    
    [Tooltip("Additional height offset")]
    public float heightOffset = 0f;
    
    [Tooltip("Noise layers specific to this biome")]
    public NoiseLayer[] terrainLayers;
    
    [Header("Visuals")]
    public Color groundColor = Color.green;
    public Color cliffColor = Color.gray;
    
    [Header("Vegetation")]
    [Range(0f, 1f)]
    public float vegetationDensity = 0.5f;
    
    // Add more biome-specific settings as needed
}
```

#### Step 3.2: Create Biome Manager

```csharp
// Create new file: BiomeManager.cs

using UnityEngine;

[System.Serializable]
public class BiomeManager
{
    [Header("Biome Selection Noise")]
    public NoiseGenerator.NoiseSettings biomeNoise = new NoiseGenerator.NoiseSettings
    {
        octaves = 2,
        scale = 100f,  // Large scale for big biome regions
        strength = 1f
    };
    
    [Header("Available Biomes")]
    public Biome[] biomes;
    
    [Header("Blending")]
    [Tooltip("How much biomes blend at borders")]
    [Range(0f, 0.2f)]
    public float blendAmount = 0.1f;
    
    /// <summary>
    /// Gets the biome(s) at a position with blend weights.
    /// </summary>
    public BiomeWeight[] GetBiomesAt(Vector3 normalizedPosition, int seed)
    {
        if (biomes == null || biomes.Length == 0)
            return new BiomeWeight[0];
        
        // Sample biome noise
        float noiseValue = NoiseGenerator.Sample3D(
            normalizedPosition * biomeNoise.scale, 
            biomeNoise, 
            seed
        );
        
        // Normalize to 0-1
        noiseValue = (noiseValue + 1f) * 0.5f;
        
        // Determine primary biome
        float biomeStep = 1f / biomes.Length;
        int primaryIndex = Mathf.Clamp(
            Mathf.FloorToInt(noiseValue / biomeStep), 
            0, 
            biomes.Length - 1
        );
        
        // Calculate blend with neighboring biomes
        float positionInBiome = (noiseValue % biomeStep) / biomeStep;
        
        // Simple case: return just primary biome
        if (blendAmount <= 0f)
        {
            return new BiomeWeight[] 
            { 
                new BiomeWeight { biome = biomes[primaryIndex], weight = 1f } 
            };
        }
        
        // Calculate blend weights
        // (Advanced: implement smooth blending between biomes)
        return new BiomeWeight[] 
        { 
            new BiomeWeight { biome = biomes[primaryIndex], weight = 1f } 
        };
    }
}

[System.Serializable]
public struct BiomeWeight
{
    public Biome biome;
    public float weight;
}
```

#### Step 3.3: Integrate Biomes into Density Function

```csharp
// In ProceduralPlanet.cs

[Header("=== BIOMES ===")]
[SerializeField] private BiomeManager biomeManager;

public float GetDensityAt(Vector3 worldPos)
{
    Vector3 toPoint = worldPos - PlanetCenter;
    float distanceFromCenter = toPoint.magnitude;
    float baseDensity = planetRadius - distanceFromCenter;
    
    float surfaceBlend = Mathf.Clamp01(1f - Mathf.Abs(baseDensity) / 20f);
    if (surfaceBlend < 0.01f) return baseDensity;
    
    Vector3 normalizedPos = toPoint.normalized;
    
    // Get biome at this position
    BiomeWeight[] biomeWeights = biomeManager.GetBiomesAt(normalizedPos, seed);
    
    float totalNoise = 0f;
    float totalWeight = 0f;
    
    foreach (var bw in biomeWeights)
    {
        if (bw.biome == null) continue;
        
        // Calculate this biome's contribution
        float biomeNoise = 0f;
        foreach (var layer in bw.biome.terrainLayers)
        {
            biomeNoise += layer.Evaluate(normalizedPos * planetRadius, seed, 0f);
        }
        
        biomeNoise *= bw.biome.heightMultiplier;
        biomeNoise += bw.biome.heightOffset;
        
        totalNoise += biomeNoise * bw.weight;
        totalWeight += bw.weight;
    }
    
    if (totalWeight > 0f)
    {
        totalNoise /= totalWeight;
    }
    
    return baseDensity + totalNoise * surfaceBlend;
}
```

### Exercise 3.1: Create Three Biomes

1. **Plains Biome**
   - Low height multiplier
   - Gentle, low-frequency noise
   - High vegetation density

2. **Mountain Biome**
   - High height multiplier
   - Sharp, high-frequency noise
   - Low vegetation density

3. **Desert Biome**
   - Medium height multiplier
   - Dune-like noise patterns
   - Very low vegetation density

### Exercise 3.2: Visualize Biomes

Add a debug mode that colors terrain based on biome:

```csharp
// In Chunk.cs, modify mesh generation to add vertex colors
// based on the biome at each vertex position
```

### Exercise 3.3: Biome Blending

Implement smooth blending between biomes:
- When a point is near a biome boundary, blend between both biomes
- Use smooth interpolation (smoothstep) for natural transitions

---

# Phase 4: Cave Generation

## Creating Underground Spaces

### Concept: Caves as Negative Noise

Caves are created by **subtracting** density in specific areas:

```
normalDensity = planetRadius - distance + terrainNoise
caveNoise = wormNoise(position)  // Returns 0 or negative

finalDensity = normalDensity + caveNoise

Where caveNoise is negative, terrain is removed = cave!
```

### Theory: Worm Caves vs. Room Caves

**Worm Caves:**
- Long, twisting tunnels
- Created with 3D noise that forms connected paths
- Use noise threshold to create tube-like shapes

**Room Caves:**
- Large open chambers
- Created with spherical "blobs" of negative density
- Can connect with tunnels

### Implementation Steps

#### Step 4.1: Create Cave Noise Layer

```csharp
// Create new file: CaveGenerator.cs

using UnityEngine;

[System.Serializable]
public class CaveGenerator
{
    [Header("Cave Settings")]
    public bool enabled = true;
    
    [Tooltip("Overall cave density (0 = no caves, 1 = lots of caves)")]
    [Range(0f, 1f)]
    public float caveDensity = 0.3f;
    
    [Header("Worm Caves (Tunnels)")]
    public bool enableWormCaves = true;
    public NoiseGenerator.NoiseSettings wormNoise = new NoiseGenerator.NoiseSettings
    {
        octaves = 2,
        scale = 20f,
        strength = 1f
    };
    
    [Tooltip("Threshold for cave creation (higher = thinner caves)")]
    [Range(0f, 1f)]
    public float wormThreshold = 0.7f;
    
    [Tooltip("How wide the tunnels are")]
    public float wormWidth = 3f;
    
    [Header("Depth Control")]
    [Tooltip("Minimum depth below surface for caves")]
    public float minDepth = 5f;
    
    [Tooltip("Maximum depth below surface for caves")]
    public float maxDepth = 40f;
    
    /// <summary>
    /// Returns negative value where caves should be carved.
    /// </summary>
    public float GetCaveDensity(Vector3 worldPos, Vector3 planetCenter, float planetRadius, int seed)
    {
        if (!enabled) return 0f;
        
        // Calculate depth below surface
        float distanceFromCenter = Vector3.Distance(worldPos, planetCenter);
        float depthBelowSurface = planetRadius - distanceFromCenter;
        
        // Only generate caves within depth range
        if (depthBelowSurface < minDepth || depthBelowSurface > maxDepth)
        {
            return 0f;
        }
        
        // Fade caves near depth boundaries
        float depthFade = 1f;
        float fadeRange = 5f;
        if (depthBelowSurface < minDepth + fadeRange)
        {
            depthFade = (depthBelowSurface - minDepth) / fadeRange;
        }
        else if (depthBelowSurface > maxDepth - fadeRange)
        {
            depthFade = (maxDepth - depthBelowSurface) / fadeRange;
        }
        
        float caveDensityValue = 0f;
        
        // Worm caves
        if (enableWormCaves)
        {
            float wormValue = NoiseGenerator.Sample3D(worldPos, wormNoise, seed + 100);
            
            // Normalize to 0-1
            wormValue = (wormValue + 1f) * 0.5f;
            
            // Create cave where noise is above threshold
            if (wormValue > wormThreshold)
            {
                // Calculate how "deep" into the cave we are
                float caveStrength = (wormValue - wormThreshold) / (1f - wormThreshold);
                caveStrength *= caveDensity * depthFade;
                
                // Negative density = carve out terrain
                caveDensityValue -= caveStrength * wormWidth;
            }
        }
        
        return caveDensityValue;
    }
}
```

#### Step 4.2: Integrate Caves into Density Function

```csharp
// In ProceduralPlanet.cs

[Header("=== CAVES ===")]
[SerializeField] private CaveGenerator caveGenerator;

public float GetDensityAt(Vector3 worldPos)
{
    Vector3 toPoint = worldPos - PlanetCenter;
    float distanceFromCenter = toPoint.magnitude;
    float baseDensity = planetRadius - distanceFromCenter;
    
    // ... existing terrain noise code ...
    
    float terrainDensity = baseDensity + totalTerrainNoise;
    
    // Add cave carving
    float caveDensity = caveGenerator.GetCaveDensity(
        worldPos, 
        PlanetCenter, 
        planetRadius, 
        seed
    );
    
    return terrainDensity + caveDensity;
}
```

### Exercise 4.1: Tune Cave Parameters

Experiment with these settings and document what each does:
- `wormThreshold`: How does changing this affect cave frequency?
- `wormWidth`: How does this affect tunnel diameter?
- `scale` in wormNoise: How does this affect tunnel length/curvature?

### Exercise 4.2: Add Room Caves

Implement large chamber caves:

```csharp
// Hint: Use spherical regions of negative density
// at positions determined by another noise function

public float GetRoomCaveDensity(Vector3 worldPos, int seed)
{
    // 1. Use noise to determine room positions
    // 2. Check distance to nearest room center
    // 3. If within room radius, return negative density
}
```

### Exercise 4.3: Connect Rooms with Tunnels

Create a system where:
1. Room caves are generated at specific positions
2. Tunnel caves preferentially connect rooms
3. The underground feels like a connected network

### Debug Tip: Visualize Caves

```csharp
// Create a debug mode that only generates underground chunks
// and uses transparent material to see cave structure
```

---

# Phase 5: Surface Detection

## Finding Where to Place Objects

### Concept: Why Surface Detection Matters

Before placing vegetation or structures, you need to know:
1. **Where is the surface?** (position)
2. **What direction is "up"?** (normal)
3. **How steep is it?** (slope)
4. **What biome is it?** (context)

### Theory: Surface Detection Methods

**Method 1: Raycast from Above**
```
Cast ray from high above, downward
Where it hits terrain = surface position
Hit normal = surface normal
```

**Method 2: Density Gradient**
```
Sample density at point
Sample density at nearby points
Gradient points toward surface
Walk along gradient until density ≈ 0
```

**Method 3: During Mesh Generation**
```
When marching cubes creates a vertex,
that vertex IS on the surface
Store these positions for later use
```

### Implementation Steps

#### Step 5.1: Create Surface Point Data Structure

```csharp
// Create new file: SurfacePoint.cs

using UnityEngine;

/// <summary>
/// Represents a point on the terrain surface.
/// </summary>
[System.Serializable]
public struct SurfacePoint
{
    /// <summary>World position of the surface point</summary>
    public Vector3 position;
    
    /// <summary>Surface normal (up direction at this point)</summary>
    public Vector3 normal;
    
    /// <summary>Slope angle in degrees (0 = flat, 90 = vertical)</summary>
    public float slope;
    
    /// <summary>Biome at this location</summary>
    public Biome biome;
    
    /// <summary>Height above sea level (or planet minimum)</summary>
    public float height;
    
    /// <summary>Is this point valid?</summary>
    public bool isValid;
    
    /// <summary>
    /// Calculates if this point is suitable for placement.
    /// </summary>
    public bool IsSuitableForPlacement(float maxSlope)
    {
        return isValid && slope <= maxSlope;
    }
}
```

#### Step 5.2: Create Surface Sampler

```csharp
// Create new file: SurfaceSampler.cs

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Provides methods to find and sample surface points.
/// </summary>
public class SurfaceSampler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float raycastHeight = 100f;
    [SerializeField] private LayerMask terrainLayer;
    
    private ProceduralPlanet planet;
    
    private void Awake()
    {
        planet = GetComponent<ProceduralPlanet>();
    }
    
    /// <summary>
    /// Finds the surface point at a given position on the planet.
    /// </summary>
    public SurfacePoint GetSurfaceAt(Vector3 direction)
    {
        SurfacePoint result = new SurfacePoint();
        
        // Start ray from above the surface
        Vector3 rayStart = planet.PlanetCenter + direction.normalized * (planet.Radius + raycastHeight);
        Vector3 rayDir = -direction.normalized;
        
        if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, raycastHeight * 2f, terrainLayer))
        {
            result.position = hit.point;
            result.normal = hit.normal;
            result.slope = Vector3.Angle(hit.normal, direction.normalized);
            result.height = Vector3.Distance(hit.point, planet.PlanetCenter) - planet.Radius;
            result.isValid = true;
            
            // Get biome (you'll implement this based on your biome system)
            // result.biome = planet.GetBiomeAt(hit.point);
        }
        
        return result;
    }
    
    /// <summary>
    /// Samples multiple surface points in an area.
    /// </summary>
    public List<SurfacePoint> SampleArea(Vector3 center, float radius, int sampleCount)
    {
        List<SurfacePoint> points = new List<SurfacePoint>();
        
        for (int i = 0; i < sampleCount; i++)
        {
            // Random point within radius on sphere surface
            Vector3 randomOffset = Random.insideUnitSphere * radius;
            Vector3 direction = (center - planet.PlanetCenter).normalized;
            
            // Project offset onto tangent plane
            Vector3 tangentOffset = Vector3.ProjectOnPlane(randomOffset, direction);
            Vector3 sampleDirection = (center + tangentOffset - planet.PlanetCenter).normalized;
            
            SurfacePoint point = GetSurfaceAt(sampleDirection);
            if (point.isValid)
            {
                points.Add(point);
            }
        }
        
        return points;
    }
}
```

#### Step 5.3: Collect Surface Points During Mesh Generation

```csharp
// Modify Chunk.cs to optionally store surface vertices

private List<SurfacePoint> surfacePoints = new List<SurfacePoint>();

public List<SurfacePoint> SurfacePoints => surfacePoints;

// In GenerateMesh(), after mesh is created:
private void CollectSurfacePoints(MeshData meshData)
{
    surfacePoints.Clear();
    
    // Every vertex in marching cubes is on the surface
    Mesh mesh = meshFilter.mesh;
    Vector3[] vertices = mesh.vertices;
    Vector3[] normals = mesh.normals;
    
    for (int i = 0; i < vertices.Length; i++)
    {
        // Convert to world space
        Vector3 worldPos = transform.TransformPoint(vertices[i]);
        Vector3 worldNormal = transform.TransformDirection(normals[i]);
        
        SurfacePoint point = new SurfacePoint
        {
            position = worldPos,
            normal = worldNormal,
            slope = Vector3.Angle(worldNormal, (worldPos - planet.PlanetCenter).normalized),
            isValid = true
        };
        
        surfacePoints.Add(point);
    }
}
```

### Exercise 5.1: Visualize Surface Normals

Draw debug lines showing surface normals across the terrain:

```csharp
// In Chunk.cs
private void OnDrawGizmosSelected()
{
    if (surfacePoints == null) return;
    
    Gizmos.color = Color.blue;
    foreach (var point in surfacePoints)
    {
        Gizmos.DrawLine(point.position, point.position + point.normal * 2f);
    }
}
```

### Exercise 5.2: Slope Map

Color the terrain based on slope:
- Green: Flat (0-15°)
- Yellow: Moderate (15-45°)
- Red: Steep (45-90°)

---

# Phase 6: Vegetation Placement

## Procedurally Placing Plants and Trees

### Concept: Vegetation Layers

Like terrain, vegetation has multiple layers:
1. **Ground cover**: Grass, small flowers (very dense)
2. **Shrubs**: Bushes, small plants (medium density)
3. **Trees**: Large plants (sparse)
4. **Special**: Rare plants, biome-specific (very sparse)

### Theory: Placement Algorithms

**Method 1: Random Scatter**
```
For each chunk:
    For i in range(density):
        Pick random surface point
        If suitable: Place vegetation
```
Problem: Uneven distribution, clumping

**Method 2: Poisson Disk Sampling**
```
Ensures minimum distance between objects
More natural, even distribution
More complex to implement
```

**Method 3: Grid with Jitter**
```
Create grid across surface
Offset each grid point by random amount
Simple but effective
```

### Implementation Steps

#### Step 6.1: Create Vegetation Definition

```csharp
// Create new file: VegetationAsset.cs

using UnityEngine;

[CreateAssetMenu(fileName = "New Vegetation", menuName = "Procedural Planet/Vegetation")]
public class VegetationAsset : ScriptableObject
{
    [Header("Basic Info")]
    public string vegetationName = "Tree";
    public GameObject[] prefabVariants;
    
    [Header("Placement Rules")]
    [Range(0f, 90f)]
    public float maxSlope = 30f;
    
    [Range(0f, 1f)]
    public float density = 0.5f;
    
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    
    [Header("Spacing")]
    public float minDistance = 2f;  // Minimum distance between instances
    
    [Header("Biome Affinity")]
    [Tooltip("Which biomes this vegetation appears in")]
    public Biome[] allowedBiomes;
    
    [Header("Height Range")]
    public float minHeight = -10f;
    public float maxHeight = 100f;
    
    /// <summary>
    /// Checks if this vegetation can be placed at the given surface point.
    /// </summary>
    public bool CanPlaceAt(SurfacePoint point)
    {
        // Check slope
        if (point.slope > maxSlope) return false;
        
        // Check height
        if (point.height < minHeight || point.height > maxHeight) return false;
        
        // Check biome
        if (allowedBiomes != null && allowedBiomes.Length > 0)
        {
            bool biomeAllowed = false;
            foreach (var biome in allowedBiomes)
            {
                if (biome == point.biome)
                {
                    biomeAllowed = true;
                    break;
                }
            }
            if (!biomeAllowed) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Gets a random prefab variant.
    /// </summary>
    public GameObject GetRandomPrefab()
    {
        if (prefabVariants == null || prefabVariants.Length == 0)
            return null;
        return prefabVariants[Random.Range(0, prefabVariants.Length)];
    }
    
    /// <summary>
    /// Gets a random scale within the defined range.
    /// </summary>
    public float GetRandomScale()
    {
        return Random.Range(minScale, maxScale);
    }
}
```

#### Step 6.2: Create Vegetation Spawner

```csharp
// Create new file: VegetationSpawner.cs

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles vegetation placement for a chunk.
/// </summary>
public class VegetationSpawner : MonoBehaviour
{
    [Header("Vegetation Types")]
    [SerializeField] private VegetationAsset[] vegetationAssets;
    
    [Header("Performance")]
    [SerializeField] private int maxInstancesPerChunk = 100;
    [SerializeField] private float updateDistance = 50f;
    
    private Dictionary<Chunk, List<GameObject>> spawnedVegetation = 
        new Dictionary<Chunk, List<GameObject>>();
    
    private Transform vegetationParent;
    
    private void Start()
    {
        vegetationParent = new GameObject("Vegetation").transform;
        vegetationParent.SetParent(transform);
    }
    
    /// <summary>
    /// Spawns vegetation for a chunk.
    /// </summary>
    public void SpawnForChunk(Chunk chunk)
    {
        if (spawnedVegetation.ContainsKey(chunk)) return;
        
        List<GameObject> instances = new List<GameObject>();
        List<SurfacePoint> surfacePoints = chunk.SurfacePoints;
        
        if (surfacePoints == null || surfacePoints.Count == 0)
        {
            spawnedVegetation[chunk] = instances;
            return;
        }
        
        // Track placed positions for spacing
        List<Vector3> placedPositions = new List<Vector3>();
        
        foreach (var asset in vegetationAssets)
        {
            if (asset == null) continue;
            
            // Calculate how many to try placing
            int attempts = Mathf.CeilToInt(surfacePoints.Count * asset.density);
            attempts = Mathf.Min(attempts, maxInstancesPerChunk);
            
            for (int i = 0; i < attempts; i++)
            {
                // Pick random surface point
                SurfacePoint point = surfacePoints[Random.Range(0, surfacePoints.Count)];
                
                // Check if placement is valid
                if (!asset.CanPlaceAt(point)) continue;
                
                // Check minimum distance from other vegetation
                bool tooClose = false;
                foreach (var pos in placedPositions)
                {
                    if (Vector3.Distance(pos, point.position) < asset.minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                
                // Spawn vegetation
                GameObject prefab = asset.GetRandomPrefab();
                if (prefab == null) continue;
                
                GameObject instance = Instantiate(prefab, point.position, Quaternion.identity, vegetationParent);
                
                // Align to surface
                instance.transform.up = point.normal;
                
                // Random rotation around up axis
                instance.transform.Rotate(point.normal, Random.Range(0f, 360f), Space.World);
                
                // Random scale
                float scale = asset.GetRandomScale();
                instance.transform.localScale = Vector3.one * scale;
                
                instances.Add(instance);
                placedPositions.Add(point.position);
                
                if (instances.Count >= maxInstancesPerChunk) break;
            }
        }
        
        spawnedVegetation[chunk] = instances;
    }
    
    /// <summary>
    /// Removes vegetation for a chunk.
    /// </summary>
    public void RemoveForChunk(Chunk chunk)
    {
        if (!spawnedVegetation.TryGetValue(chunk, out List<GameObject> instances))
            return;
        
        foreach (var instance in instances)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        
        spawnedVegetation.Remove(chunk);
    }
}
```

#### Step 6.3: Integrate with Chunk Loading

```csharp
// In ProceduralPlanet.cs

[Header("=== VEGETATION ===")]
[SerializeField] private VegetationSpawner vegetationSpawner;

// Modify GenerateChunk():
private void GenerateChunk(Vector3Int chunkPos)
{
    // ... existing chunk creation code ...
    
    // Spawn vegetation after chunk is ready
    if (vegetationSpawner != null)
    {
        vegetationSpawner.SpawnForChunk(chunk);
    }
}

// Modify UnloadChunk():
private void UnloadChunk(Vector3Int chunkPos)
{
    if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
    {
        // Remove vegetation first
        if (vegetationSpawner != null)
        {
            vegetationSpawner.RemoveForChunk(chunk);
        }
        
        Destroy(chunk.gameObject);
        activeChunks.Remove(chunkPos);
    }
}
```

### Exercise 6.1: Create Vegetation Assets

Create at least 3 vegetation assets:
1. **Grass**: Very high density, tiny prefab, any slope
2. **Bush**: Medium density, small prefab, moderate slope
3. **Tree**: Low density, large prefab, low slope only

### Exercise 6.2: Biome-Specific Vegetation

Make vegetation biome-aware:
- Forest biome: Many trees, dense undergrowth
- Desert biome: Cacti, sparse shrubs
- Mountain biome: Only hardy shrubs at lower elevations

### Exercise 6.3: LOD for Vegetation

Implement Level of Detail:
- Far: Billboard (2D image facing camera)
- Medium: Low-poly mesh
- Near: Full detail mesh

---

# Phase 7: Structure Generation

## Placing Buildings and Landmarks

### Concept: Structures vs. Vegetation

Structures are more complex than vegetation:
- Need flat ground (may need to modify terrain)
- May have multiple parts
- May have interior spaces
- Often follow placement rules (near water, on hilltops, etc.)

### Theory: Structure Types

1. **Point Structures**: Single objects (monuments, towers)
2. **Compound Structures**: Multiple connected buildings (villages)
3. **Linear Structures**: Paths, walls, roads
4. **Embedded Structures**: Built into terrain (caves, mines)

### Implementation Steps

#### Step 7.1: Create Structure Definition

```csharp
// Create new file: StructureAsset.cs

using UnityEngine;

[CreateAssetMenu(fileName = "New Structure", menuName = "Procedural Planet/Structure")]
public class StructureAsset : ScriptableObject
{
    [Header("Basic Info")]
    public string structureName = "Building";
    public GameObject prefab;
    
    [Header("Placement Requirements")]
    public float requiredFlatRadius = 5f;  // Area that must be flat
    public float maxSlopeVariation = 5f;   // Max slope variation in area
    
    [Header("Terrain Modification")]
    public bool flattenTerrain = true;
    public float flattenRadius = 10f;
    public float flattenBlend = 2f;  // Blend distance at edges
    
    [Header("Rarity")]
    [Range(0f, 1f)]
    public float spawnChance = 0.1f;
    public float minDistanceFromOthers = 50f;
    
    [Header("Biome Restrictions")]
    public Biome[] allowedBiomes;
}
```

#### Step 7.2: Create Structure Placer

```csharp
// Create new file: StructurePlacer.cs

using UnityEngine;
using System.Collections.Generic;

public class StructurePlacer : MonoBehaviour
{
    [SerializeField] private StructureAsset[] structures;
    [SerializeField] private ProceduralPlanet planet;
    
    private List<Vector3> placedStructurePositions = new List<Vector3>();
    
    /// <summary>
    /// Attempts to place structures in a region.
    /// </summary>
    public void PlaceStructuresInRegion(Vector3 regionCenter, float regionRadius)
    {
        foreach (var structure in structures)
        {
            if (structure == null) continue;
            if (Random.value > structure.spawnChance) continue;
            
            // Find suitable location
            Vector3? location = FindSuitableLocation(regionCenter, regionRadius, structure);
            
            if (location.HasValue)
            {
                PlaceStructure(structure, location.Value);
            }
        }
    }
    
    private Vector3? FindSuitableLocation(Vector3 center, float radius, StructureAsset structure)
    {
        int maxAttempts = 20;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            // Random point in region
            Vector3 randomDir = Random.onUnitSphere;
            Vector3 testPoint = center + randomDir * Random.Range(0f, radius);
            
            // Get surface at this point
            // (Use your surface sampling method)
            
            // Check if location is suitable
            // - Flat enough?
            // - Far enough from other structures?
            // - Correct biome?
            
            // For now, simplified check:
            bool tooCloseToOther = false;
            foreach (var pos in placedStructurePositions)
            {
                if (Vector3.Distance(pos, testPoint) < structure.minDistanceFromOthers)
                {
                    tooCloseToOther = true;
                    break;
                }
            }
            
            if (!tooCloseToOther)
            {
                return testPoint;
            }
        }
        
        return null;
    }
    
    private void PlaceStructure(StructureAsset structure, Vector3 position)
    {
        // Flatten terrain if required
        if (structure.flattenTerrain)
        {
            FlattenTerrainAt(position, structure.flattenRadius, structure.flattenBlend);
        }
        
        // Get surface normal for alignment
        Vector3 up = (position - planet.PlanetCenter).normalized;
        
        // Instantiate structure
        GameObject instance = Instantiate(
            structure.prefab, 
            position, 
            Quaternion.FromToRotation(Vector3.up, up)
        );
        
        placedStructurePositions.Add(position);
    }
    
    private void FlattenTerrainAt(Vector3 center, float radius, float blend)
    {
        // Modify terrain density to create flat area
        // This is advanced - you'll need to:
        // 1. Store terrain modifications separately from base density
        // 2. Blend modifications with base terrain
        // 3. Regenerate affected chunks
        
        planet.ModifyTerrain(center, radius, 10f);  // Simplified: just fill in
    }
}
```

### Exercise 7.1: Create Simple Structures

Create prefabs for:
1. **Stone monument**: Simple pillar, no terrain modification
2. **Small hut**: Requires flat ground, slight terrain modification
3. **Tower**: Tall structure, embedded slightly in ground

### Exercise 7.2: Terrain Flattening

Implement proper terrain flattening:
```csharp
// Create a height target at structure position
// Modify density to match that height in a radius
// Blend smoothly at edges
```

### Exercise 7.3: Procedural Building Generation

Instead of prefabs, generate buildings procedurally:
1. Define building rules (width, height, roof style)
2. Generate mesh based on rules
3. Each building is unique but follows style

---

# Phase 8: Level of Detail (LOD)

## Showing More Detail Up Close

### Concept: Why LOD?

Distant terrain doesn't need full detail:
- Player can't see small features
- Rendering costs memory and GPU time
- Chunk generation costs CPU time

LOD solution:
- Far: Large voxels, simple mesh
- Near: Small voxels, detailed mesh

### Theory: LOD Approaches

**Approach 1: Discrete LOD Levels**
```
Distance 0-50:   LOD 0 (full detail, 1m voxels)
Distance 50-100: LOD 1 (half detail, 2m voxels)
Distance 100+:   LOD 2 (quarter detail, 4m voxels)
```

**Approach 2: Continuous LOD (CLOD)**
```
Detail = function(distance)
Smoothly transitions between levels
More complex, smoother results
```

### Implementation Steps

#### Step 8.1: Multi-Resolution Chunks

```csharp
// Modify Chunk.cs

public class Chunk : MonoBehaviour
{
    public int LODLevel { get; private set; } = 0;
    
    /// <summary>
    /// Sets the LOD level and regenerates if changed.
    /// </summary>
    public void SetLOD(int level)
    {
        if (level == LODLevel) return;
        
        LODLevel = level;
        float lodVoxelSize = VoxelSize * Mathf.Pow(2, level);
        
        // Regenerate at new resolution
        GenerateDensitiesAtLOD(lodVoxelSize);
        GenerateMesh();
    }
    
    private void GenerateDensitiesAtLOD(float lodVoxelSize)
    {
        int lodSize = Mathf.CeilToInt(SIZE * VoxelSize / lodVoxelSize) + 1;
        densities = new float[lodSize, lodSize, lodSize];
        
        for (int x = 0; x < lodSize; x++)
        {
            for (int y = 0; y < lodSize; y++)
            {
                for (int z = 0; z < lodSize; z++)
                {
                    Vector3 worldPos = transform.position + 
                        new Vector3(x, y, z) * lodVoxelSize;
                    densities[x, y, z] = planet.GetDensityAt(worldPos);
                }
            }
        }
    }
}
```

#### Step 8.2: LOD Manager

```csharp
// Create new file: LODManager.cs

using UnityEngine;

public class LODManager : MonoBehaviour
{
    [System.Serializable]
    public class LODLevel
    {
        public float maxDistance;
        public int lodLevel;
    }
    
    [SerializeField] private LODLevel[] lodLevels = new LODLevel[]
    {
        new LODLevel { maxDistance = 50f, lodLevel = 0 },
        new LODLevel { maxDistance = 100f, lodLevel = 1 },
        new LODLevel { maxDistance = 200f, lodLevel = 2 },
    };
    
    [SerializeField] private Transform player;
    [SerializeField] private ProceduralPlanet planet;
    
    private void Update()
    {
        if (player == null) return;
        
        // Update LOD for all chunks based on distance to player
        foreach (var chunk in planet.GetActiveChunks())
        {
            float distance = Vector3.Distance(
                player.position, 
                chunk.transform.position
            );
            
            int targetLOD = GetLODForDistance(distance);
            chunk.SetLOD(targetLOD);
        }
    }
    
    private int GetLODForDistance(float distance)
    {
        for (int i = 0; i < lodLevels.Length; i++)
        {
            if (distance <= lodLevels[i].maxDistance)
            {
                return lodLevels[i].lodLevel;
            }
        }
        return lodLevels[lodLevels.Length - 1].lodLevel;
    }
}
```

### Exercise 8.1: Implement Basic LOD

1. Add LOD support to chunks
2. Create 3 LOD levels
3. Verify distant chunks use lower detail

### Exercise 8.2: Seamless LOD Transitions

The hard part: Different LOD levels have different vertex positions at boundaries, causing cracks.

Solutions to research and implement:
1. **Skirts**: Extend chunk edges downward to hide gaps
2. **Stitching**: Special triangles at LOD boundaries
3. **Transvoxel Algorithm**: Designed specifically for this problem

---

# Phase 9: Saving and Loading

## Persisting Player Modifications

### Concept: What to Save

You don't save the entire planet — it's procedurally generated from the seed.

You only save **modifications**:
- Dug holes
- Built terrain
- Placed structures
- Discovered locations

### Theory: Delta Storage

```
Original density = GenerateDensity(position, seed)
Modified density = Original + StoredDelta

Save file contains only deltas, not full terrain
```

### Implementation Steps

#### Step 9.1: Modification Tracker

```csharp
// Create new file: TerrainModification.cs

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TerrainModification
{
    public Vector3Int chunkPosition;
    public Dictionary<Vector3Int, float> voxelDeltas = new Dictionary<Vector3Int, float>();
}

[System.Serializable]
public class PlanetSaveData
{
    public int seed;
    public List<TerrainModification> modifications = new List<TerrainModification>();
    public List<Vector3> structurePositions = new List<Vector3>();
}
```

#### Step 9.2: Save/Load System

```csharp
// Create new file: PlanetSaveSystem.cs

using UnityEngine;
using System.IO;

public class PlanetSaveSystem : MonoBehaviour
{
    private ProceduralPlanet planet;
    private string savePath;
    
    private void Awake()
    {
        planet = GetComponent<ProceduralPlanet>();
        savePath = Application.persistentDataPath + "/planet_save.json";
    }
    
    public void Save()
    {
        PlanetSaveData data = new PlanetSaveData();
        data.seed = planet.Seed;
        
        // Collect modifications from all modified chunks
        foreach (var chunk in planet.GetActiveChunks())
        {
            if (chunk.IsModified)
            {
                // Store chunk modifications
                // (You'll need to implement GetModifications in Chunk)
            }
        }
        
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);
        
        Debug.Log($"Saved to {savePath}");
    }
    
    public void Load()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("No save file found");
            return;
        }
        
        string json = File.ReadAllText(savePath);
        PlanetSaveData data = JsonUtility.FromJson<PlanetSaveData>(json);
        
        // Apply seed
        planet.SetSeed(data.seed);
        
        // Apply modifications
        foreach (var mod in data.modifications)
        {
            // Apply to chunks as they load
            planet.QueueModification(mod);
        }
        
        Debug.Log("Loaded save file");
    }
}
```

### Exercise 9.1: Track Modifications

Modify `Chunk.cs` to track changes:
1. Store original density values
2. Calculate delta when modified
3. Provide method to get all deltas

### Exercise 9.2: Efficient Storage

Implement compression:
1. Only store non-zero deltas
2. Use run-length encoding for similar values
3. Quantize floats to reduce precision where acceptable

---

# Phase 10: Performance Optimization

## Making It Run Smoothly

### Concept: Optimization Targets

1. **Chunk Generation Speed**: How fast new chunks appear
2. **Frame Rate**: Smooth gameplay
3. **Memory Usage**: Don't run out of RAM
4. **Load Times**: Initial world generation

### Theory: Optimization Techniques

**Threading/Jobs:**
```
Main Thread: Gameplay, rendering
Worker Threads: Chunk generation, mesh building

Never block main thread with generation!
```

**Object Pooling:**
```
Instead of: Create chunk → Use → Destroy
Do: Get from pool → Use → Return to pool

Avoids garbage collection hitches
```

**Spatial Optimization:**
```
Don't check every chunk every frame
Use spatial data structures (octree, grid)
Only update what's near the player
```

### Implementation Steps

#### Step 10.1: Threaded Chunk Generation

```csharp
// Using Unity's Job System

using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct GenerateDensitiesJob : IJob
{
    public NativeArray<float> densities;
    public Vector3 chunkWorldPos;
    public Vector3 planetCenter;
    public float planetRadius;
    public float voxelSize;
    public int size;
    public int seed;
    
    public void Execute()
    {
        for (int i = 0; i < densities.Length; i++)
        {
            // Convert flat index to 3D coordinates
            int x = i % (size + 1);
            int y = (i / (size + 1)) % (size + 1);
            int z = i / ((size + 1) * (size + 1));
            
            Vector3 worldPos = chunkWorldPos + new Vector3(x, y, z) * voxelSize;
            
            // Calculate density (simplified - full implementation would include noise)
            float distance = Vector3.Distance(worldPos, planetCenter);
            densities[i] = planetRadius - distance;
        }
    }
}
```

#### Step 10.2: Chunk Pool

```csharp
// Create new file: ChunkPool.cs

using UnityEngine;
using System.Collections.Generic;

public class ChunkPool : MonoBehaviour
{
    [SerializeField] private int initialPoolSize = 20;
    [SerializeField] private GameObject chunkPrefab;
    
    private Queue<Chunk> availableChunks = new Queue<Chunk>();
    
    private void Start()
    {
        // Pre-create chunks
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledChunk();
        }
    }
    
    private void CreatePooledChunk()
    {
        GameObject obj = Instantiate(chunkPrefab, transform);
        obj.SetActive(false);
        availableChunks.Enqueue(obj.GetComponent<Chunk>());
    }
    
    public Chunk GetChunk()
    {
        if (availableChunks.Count == 0)
        {
            CreatePooledChunk();
        }
        
        Chunk chunk = availableChunks.Dequeue();
        chunk.gameObject.SetActive(true);
        return chunk;
    }
    
    public void ReturnChunk(Chunk chunk)
    {
        chunk.gameObject.SetActive(false);
        chunk.transform.SetParent(transform);
        availableChunks.Enqueue(chunk);
    }
}
```

### Exercise 10.1: Profile Your Code

Use Unity Profiler to identify:
1. What takes the most time?
2. Where is memory allocated?
3. What causes frame rate drops?

### Exercise 10.2: Implement Async Generation

Move chunk generation off the main thread:
1. Request chunk generation
2. Generate densities on worker thread
3. Generate mesh on worker thread
4. Apply mesh on main thread (required by Unity)

---

# Summary: Your Learning Journey

## Phase Checklist

| Phase | Topic | Core Skill Learned |
|-------|-------|-------------------|
| 1 | Foundation | Understanding density fields |
| 2 | Terrain Variation | Combining noise layers |
| 3 | Biomes | Conditional generation |
| 4 | Caves | Subtractive density |
| 5 | Surface Detection | Querying generated terrain |
| 6 | Vegetation | Procedural placement |
| 7 | Structures | Complex placement rules |
| 8 | LOD | Multi-resolution systems |
| 9 | Save/Load | Delta-based persistence |
| 10 | Optimization | Threading and pooling |

## Recommended Order

1. **Complete Phase 1 exercises** — Understanding is crucial
2. **Phase 2** — More interesting terrain immediately
3. **Phase 4** — Caves are exciting and teach subtraction
4. **Phase 3** — Biomes make world diverse
5. **Phase 5-6** — Vegetation brings world to life
6. **Phase 8** — LOD needed for larger worlds
7. **Phase 7** — Structures add points of interest
8. **Phase 9** — Save/load for persistence
9. **Phase 10** — Optimize once everything works

## Key Principles to Remember

1. **Everything is density** — Every feature modifies the density function
2. **Deterministic = reproducible** — Same seed = same world
3. **Chunk locally, think globally** — Each chunk is independent but part of a whole
4. **Profile before optimizing** — Measure, don't guess
5. **Iterate** — Start simple, add complexity gradually

---

Good luck on your procedural generation journey! Take your time with each phase, experiment, break things, and learn from the results.