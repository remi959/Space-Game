# Volumetric Planet Generation Implementation Plan

## Table of Contents
1. [Foundation Concepts](#1-foundation-concepts)
2. [Architecture Overview](#2-architecture-overview)
3. [Phase 1: Basic Marching Cubes](#3-phase-1-basic-marching-cubes)
4. [Phase 2: Spherical Density Function](#4-phase-2-spherical-density-function)
5. [Phase 3: Chunk Management](#5-phase-3-chunk-management)
6. [Phase 4: Cave Generation](#6-phase-4-cave-generation)
7. [Phase 5: LOD with Transvoxel](#7-phase-5-lod-with-transvoxel)
8. [Phase 6: Biomes](#8-phase-6-biomes)
9. [Phase 7: Player Modifications](#9-phase-7-player-modifications)
10. [Performance Considerations](#10-performance-considerations)

---

## 1. Foundation Concepts

Before diving into implementation, you need to understand several core concepts that underpin volumetric terrain generation.

### 1.1 Your Current Approach: Surface Displacement

Your existing code works by:
1. Creating a unit sphere (points on surface)
2. Displacing each vertex along its normal based on noise
3. The result is a 2D surface with varying height

```
Current: vertex_position = (1 + noise(direction)) * radius * direction
```

**Limitation:** The surface is always a function of direction - you can't have caves, overhangs, or holes because each direction maps to exactly one surface point.

### 1.2 Density Fields (Scalar Fields)

A density field assigns a scalar value to every point in 3D space:

```
f(x, y, z) → scalar value (density)
```

**Convention:**
- Negative values = inside solid material
- Positive values = outside (air/empty)
- Zero = exactly on the surface (the "isosurface")

Think of it like measuring "how far inside or outside the terrain am I?"

```
Example for a sphere of radius 10:
f(point) = length(point) - 10

At center (0,0,0):     f = 0 - 10 = -10  (deep inside)
At surface (10,0,0):   f = 10 - 10 = 0   (on surface)
At (15,0,0):           f = 15 - 10 = 5   (outside)
```

### 1.3 Signed Distance Fields (SDFs)

An SDF is a special density field where the absolute value represents the exact distance to the nearest surface:

```
|f(point)| = distance to nearest surface
sign(f(point)) = inside (-) or outside (+)
```

**Why SDFs are useful:**
- Combining shapes is intuitive (union, intersection, subtraction)
- Gradients give you surface normals
- Good for smooth blending

**Common SDF Operations:**
```csharp
// Union (combine two shapes): take minimum
float Union(float d1, float d2) => Mathf.Min(d1, d2);

// Intersection (overlap only): take maximum  
float Intersection(float d1, float d2) => Mathf.Max(d1, d2);

// Subtraction (cut d2 from d1): 
float Subtraction(float d1, float d2) => Mathf.Max(d1, -d2);

// Smooth union (blend shapes together)
float SmoothUnion(float d1, float d2, float k) {
    float h = Mathf.Clamp01(0.5f + 0.5f * (d2 - d1) / k);
    return Mathf.Lerp(d2, d1, h) - k * h * (1f - h);
}
```

### 1.4 Voxels and Grids

A **voxel** (volumetric pixel) is a point sample in 3D space. We store density values in a 3D grid:

```
Grid: float[,,] density = new float[width, height, depth];

Each cell: density[x, y, z] = f(worldPosition(x, y, z))
```

**Important:** We sample at grid vertices (corners), not cell centers. This means:
- A 32×32×32 voxel grid needs 33×33×33 sample points
- Each "cube" for marching cubes uses 8 corner samples

```
    v4 -------- v5
   /|          /|
  / |         / |
v7 -------- v6  |      Grid cell with 8 corner vertices
 |  v0 ------|- v1     Each corner has a density value
 | /         | /
 |/          |/
v3 -------- v2
```

### 1.5 Isosurface Extraction

The **isosurface** is where f(x,y,z) = 0 (or any chosen threshold). Marching cubes extracts a triangle mesh approximating this surface.

```
Density field:          Extracted mesh:
                        
  + + + + +               ___________
  + + + + +              |           |
  - - - + +      →       |   Solid   |
  - - - - +              |___________|
  - - - - -              
  
(- = solid, + = air)    (triangles along boundary)
```

### 1.6 Why This Enables Caves and Overhangs

With density fields, any point can be solid or empty regardless of its neighbors:

```
Surface displacement (your current approach):
- Point A at direction D: can only have ONE height
- No caves possible

Density field:
- Any point can be solid or air
- Cave: a tunnel of positive values through negative values
- Overhang: solid above air above solid
```

---

## 2. Architecture Overview

### 2.1 High-Level Structure

```
VolumetricPlanet
├── PlanetSettings (ScriptableObject)
│   ├── Radius
│   ├── ChunkResolution (voxels per chunk)
│   ├── ChunkSize (world units)
│   └── NoiseSettings[]
│
├── DensityGenerator
│   ├── BaseDensity(point) → sphere SDF
│   ├── TerrainNoise(point) → surface features
│   ├── CaveNoise(point) → cave carving
│   └── SampleDensity(point) → combined result
│
├── ChunkManager
│   ├── ActiveChunks Dictionary
│   ├── LoadChunk(coord)
│   ├── UnloadChunk(coord)
│   └── UpdateLoadedChunks(playerPos)
│
├── MeshGenerator
│   ├── MarchingCubes algorithm
│   ├── GenerateMesh(densityField) → Mesh
│   └── (Later: Transvoxel for LOD)
│
└── PlanetChunk
    ├── ChunkCoordinate
    ├── DensityField[,,]
    ├── Mesh
    ├── GameObject
    └── IsDirty flag (for regeneration)
```

### 2.2 Coordinate Systems

You'll work with multiple coordinate systems:

```
1. World Space
   - Unity's global coordinates
   - Planet center at (0,0,0) or wherever placed
   
2. Planet Space  
   - Relative to planet center
   - Used for density calculations
   
3. Chunk Coordinates
   - Integer (x, y, z) identifying which chunk
   - Chunk (0,0,0) is at planet center
   
4. Local Voxel Coordinates
   - (x, y, z) within a chunk's density grid
   - Range: 0 to ChunkResolution
```

**Conversion Functions:**
```csharp
// World position to chunk coordinate
Vector3Int WorldToChunkCoord(Vector3 worldPos) {
    return new Vector3Int(
        Mathf.FloorToInt(worldPos.x / chunkSize),
        Mathf.FloorToInt(worldPos.y / chunkSize),
        Mathf.FloorToInt(worldPos.z / chunkSize)
    );
}

// Chunk coordinate + local position to world position
Vector3 ChunkLocalToWorld(Vector3Int chunkCoord, Vector3 localPos) {
    return new Vector3(
        chunkCoord.x * chunkSize + localPos.x,
        chunkCoord.y * chunkSize + localPos.y,
        chunkCoord.z * chunkSize + localPos.z
    );
}

// Voxel index to local position within chunk
Vector3 VoxelToLocal(int x, int y, int z) {
    float step = chunkSize / chunkResolution;
    return new Vector3(x * step, y * step, z * step);
}
```

### 2.3 Data Flow

```
1. Chunk needs generation
       ↓
2. For each voxel corner in chunk:
   - Calculate world position
   - Sample density function
   - Store in density[x,y,z]
       ↓
3. Pass density field to mesh generator
       ↓
4. Marching cubes produces vertices + triangles
       ↓
5. Create/update Unity Mesh
       ↓
6. Assign to chunk's MeshFilter
```

---

## 3. Phase 1: Basic Marching Cubes

### 3.1 Algorithm Overview

Marching cubes processes the density field one cube at a time:

```
For each cube (8 corners):
1. Sample density at all 8 corners
2. Determine which corners are inside (negative) vs outside (positive)
3. This creates an 8-bit index (0-255) - each bit = one corner
4. Look up which edges are crossed by the surface
5. Interpolate vertex positions along crossed edges
6. Look up triangle configuration
7. Output triangles
```

### 3.2 The 256 Cases

Each corner is either inside or outside, giving 2^8 = 256 configurations:

```
Corner numbering:
    4 -------- 5
   /|         /|
  / |        / |
 7 -------- 6  |
 |  0 ------|-- 1
 | /        | /
 |/         |/
 3 -------- 2

Index calculation:
int cubeIndex = 0;
if (density[0] < 0) cubeIndex |= 1;    // bit 0
if (density[1] < 0) cubeIndex |= 2;    // bit 1
if (density[2] < 0) cubeIndex |= 4;    // bit 2
if (density[3] < 0) cubeIndex |= 8;    // bit 3
if (density[4] < 0) cubeIndex |= 16;   // bit 4
if (density[5] < 0) cubeIndex |= 32;   // bit 5
if (density[6] < 0) cubeIndex |= 64;   // bit 6
if (density[7] < 0) cubeIndex |= 128;  // bit 7
```

### 3.3 Edge Table

The edge table tells us which edges have surface crossings for each configuration:

```csharp
// Each entry is a 12-bit mask indicating which edges are crossed
// Edge 0 = corner 0-1, Edge 1 = corner 1-2, etc.
static readonly int[] EdgeTable = new int[256] {
    0x000, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
    0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
    // ... 256 entries total (see full table in implementation)
};

// Edge definitions: which two corners each edge connects
static readonly int[,] EdgeConnections = new int[12, 2] {
    {0, 1}, {1, 2}, {2, 3}, {3, 0},  // Bottom face edges
    {4, 5}, {5, 6}, {6, 7}, {7, 4},  // Top face edges  
    {0, 4}, {1, 5}, {2, 6}, {3, 7}   // Vertical edges
};
```

### 3.4 Triangle Table

The triangle table defines which triangles to create for each configuration:

```csharp
// Each entry lists vertex indices (referring to edge intersection points)
// -1 marks end of list
static readonly int[,] TriangleTable = new int[256, 16] {
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, // Case 0: all outside
    {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},    // Case 1: corner 0 inside
    {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},    // Case 2: corner 1 inside
    // ... 256 entries total
};
```

### 3.5 Edge Interpolation

When the surface crosses an edge, we interpolate to find where:

```csharp
Vector3 InterpolateEdge(Vector3 p1, Vector3 p2, float v1, float v2) {
    // v1 and v2 are density values at p1 and p2
    // We want to find where density = 0
    
    if (Mathf.Abs(v1) < 0.00001f) return p1;
    if (Mathf.Abs(v2) < 0.00001f) return p2;
    if (Mathf.Abs(v1 - v2) < 0.00001f) return p1;
    
    // Linear interpolation factor
    float t = -v1 / (v2 - v1);  // Solving: v1 + t*(v2-v1) = 0
    
    return new Vector3(
        p1.x + t * (p2.x - p1.x),
        p1.y + t * (p2.y - p1.y),
        p1.z + t * (p2.z - p1.z)
    );
}
```

### 3.6 Complete Marching Cubes Implementation Structure

```csharp
public class MarchingCubes {
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    
    public Mesh GenerateMesh(float[,,] densityField, float cellSize) {
        vertices.Clear();
        triangles.Clear();
        
        int sizeX = densityField.GetLength(0) - 1;
        int sizeY = densityField.GetLength(1) - 1;
        int sizeZ = densityField.GetLength(2) - 1;
        
        // March through all cells
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++) {
                for (int z = 0; z < sizeZ; z++) {
                    MarchCube(densityField, x, y, z, cellSize);
                }
            }
        }
        
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
    
    private void MarchCube(float[,,] field, int x, int y, int z, float cellSize) {
        // 1. Get density values at 8 corners
        float[] cornerDensities = new float[8];
        Vector3[] cornerPositions = new Vector3[8];
        
        for (int i = 0; i < 8; i++) {
            Vector3Int offset = CornerOffsets[i];
            cornerDensities[i] = field[x + offset.x, y + offset.y, z + offset.z];
            cornerPositions[i] = new Vector3(
                (x + offset.x) * cellSize,
                (y + offset.y) * cellSize,
                (z + offset.z) * cellSize
            );
        }
        
        // 2. Calculate cube index
        int cubeIndex = 0;
        for (int i = 0; i < 8; i++) {
            if (cornerDensities[i] < 0) cubeIndex |= (1 << i);
        }
        
        // 3. Skip if entirely inside or outside
        if (EdgeTable[cubeIndex] == 0) return;
        
        // 4. Find edge intersections
        Vector3[] edgeVertices = new Vector3[12];
        for (int i = 0; i < 12; i++) {
            if ((EdgeTable[cubeIndex] & (1 << i)) != 0) {
                int c1 = EdgeConnections[i, 0];
                int c2 = EdgeConnections[i, 1];
                edgeVertices[i] = InterpolateEdge(
                    cornerPositions[c1], cornerPositions[c2],
                    cornerDensities[c1], cornerDensities[c2]
                );
            }
        }
        
        // 5. Create triangles
        for (int i = 0; TriangleTable[cubeIndex, i] != -1; i += 3) {
            int baseIndex = vertices.Count;
            
            vertices.Add(edgeVertices[TriangleTable[cubeIndex, i]]);
            vertices.Add(edgeVertices[TriangleTable[cubeIndex, i + 1]]);
            vertices.Add(edgeVertices[TriangleTable[cubeIndex, i + 2]]);
            
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }
    }
    
    // Corner offsets for the 8 corners of a cube
    private static readonly Vector3Int[] CornerOffsets = {
        new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(1, 0, 1), new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
        new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1)
    };
}
```

### 3.7 Testing Phase 1

Create a simple test scene:

```csharp
public class MarchingCubesTest : MonoBehaviour {
    public int gridSize = 32;
    public float cellSize = 0.5f;
    
    void Start() {
        // Create a simple sphere density field
        float[,,] density = new float[gridSize + 1, gridSize + 1, gridSize + 1];
        Vector3 center = new Vector3(gridSize / 2f, gridSize / 2f, gridSize / 2f) * cellSize;
        float radius = gridSize * cellSize * 0.4f;
        
        for (int x = 0; x <= gridSize; x++) {
            for (int y = 0; y <= gridSize; y++) {
                for (int z = 0; z <= gridSize; z++) {
                    Vector3 pos = new Vector3(x, y, z) * cellSize;
                    density[x, y, z] = Vector3.Distance(pos, center) - radius;
                }
            }
        }
        
        // Generate mesh
        MarchingCubes mc = new MarchingCubes();
        Mesh mesh = mc.GenerateMesh(density, cellSize);
        
        // Display
        GetComponent<MeshFilter>().mesh = mesh;
    }
}
```

---

## 4. Phase 2: Spherical Density Function

### 4.1 Converting Your Existing System

Your current noise system modifies surface height. We'll convert it to modify density:

```
Current (surface displacement):
  height = radius + noise(direction)
  vertex = height * direction

New (density field):
  baseDensity = distanceFromCenter - radius
  terrainModification = -noise(direction) * noiseStrength
  finalDensity = baseDensity + terrainModification
```

**Key insight:** Subtracting from density makes terrain rise (more solid), adding makes it sink (more air).

### 4.2 Density Generator Class

```csharp
public class DensityGenerator {
    private PlanetSettings settings;
    private INoiseFilter[] terrainNoiseFilters;
    private INoiseFilter caveNoiseFilter;
    
    public DensityGenerator(PlanetSettings settings) {
        this.settings = settings;
        InitializeNoiseFilters();
    }
    
    private void InitializeNoiseFilters() {
        // Reuse your existing noise filter system
        terrainNoiseFilters = new INoiseFilter[settings.noiseLayers.Length];
        for (int i = 0; i < terrainNoiseFilters.Length; i++) {
            terrainNoiseFilters[i] = NoiseFilterFactory.CreateNoiseFilter(
                settings.noiseLayers[i].Settings
            );
        }
    }
    
    public float SampleDensity(Vector3 worldPoint) {
        // 1. Base sphere density
        float distanceFromCenter = worldPoint.magnitude;
        float baseDensity = distanceFromCenter - settings.planetRadius;
        
        // 2. Terrain modification (negative to make mountains rise)
        Vector3 pointOnUnitSphere = worldPoint.normalized;
        float terrainHeight = CalculateTerrainHeight(pointOnUnitSphere);
        
        // 3. Combine: terrain height subtracts from density (makes solid)
        float density = baseDensity - terrainHeight;
        
        // 4. (Later) Cave carving adds to density (makes air)
        // density += CalculateCaveDensity(worldPoint);
        
        return density;
    }
    
    private float CalculateTerrainHeight(Vector3 pointOnUnitSphere) {
        // This is essentially your existing ShapeGenerator logic
        float firstLayerValue = 0;
        float elevation = 0;
        
        if (terrainNoiseFilters.Length > 0) {
            firstLayerValue = terrainNoiseFilters[0].Evaluate(pointOnUnitSphere);
            if (settings.noiseLayers[0].Enabled) {
                elevation = firstLayerValue;
            }
        }
        
        for (int i = 1; i < terrainNoiseFilters.Length; i++) {
            if (settings.noiseLayers[i].Enabled) {
                float mask = settings.noiseLayers[i].UseFirstLayerAsMask 
                    ? firstLayerValue : 1;
                elevation += terrainNoiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
            }
        }
        
        return elevation;
    }
}
```

### 4.3 Understanding the Density Function Visually

```
Cross-section through planet:

    Outside (density > 0)           Density values:
         ↓                             +5  +3  +1  +2  +4
    ~~~~~~~~~~~~  ← Surface (density ≈ 0)     
    ############                       -1  -2  -3  -2  -1
    ############  ← Inside (density < 0)
    ############                       -5  -7  -8  -7  -5
    ############
    ~~~~~~~~~~~~  ← Surface              
         ↓
    Outside (density > 0)

With terrain noise:
    
         +++++++                    Noise adds negative values
        ++~~~~~~++                  to create mountains
       ++########++     →          (pushes surface outward)
      ++############++
     ++##############++
    ++################++
```

### 4.4 Sampling Strategy for Spherical Chunks

Since we're working with a sphere, we need to be smart about where we sample:

```csharp
public class SphericalChunkSampler {
    // Only sample within a shell around the planet surface
    // Don't waste samples deep inside or far outside
    
    public bool ShouldSampleChunk(Vector3Int chunkCoord, PlanetSettings settings) {
        Vector3 chunkCenter = ChunkCoordToWorldCenter(chunkCoord);
        float distanceFromPlanetCenter = chunkCenter.magnitude;
        
        float innerBound = settings.planetRadius - settings.maxTerrainDepth;
        float outerBound = settings.planetRadius + settings.maxTerrainHeight;
        float chunkDiagonal = settings.chunkSize * Mathf.Sqrt(3);
        
        // Only generate chunks that could contain surface
        return distanceFromPlanetCenter > innerBound - chunkDiagonal &&
               distanceFromPlanetCenter < outerBound + chunkDiagonal;
    }
}
```

### 4.5 Gradient Calculation for Normals

For better lighting, calculate normals from density gradients:

```csharp
public Vector3 CalculateNormal(Vector3 point, float epsilon = 0.01f) {
    // Central differences
    float dx = SampleDensity(point + Vector3.right * epsilon) - 
               SampleDensity(point - Vector3.right * epsilon);
    float dy = SampleDensity(point + Vector3.up * epsilon) - 
               SampleDensity(point - Vector3.up * epsilon);
    float dz = SampleDensity(point + Vector3.forward * epsilon) - 
               SampleDensity(point - Vector3.forward * epsilon);
    
    // Gradient points toward increasing density (outward from surface)
    // Negate for surface normal pointing outward from solid
    return -new Vector3(dx, dy, dz).normalized;
}
```

---

## 5. Phase 3: Chunk Management

### 5.1 Why Chunks?

A planet-sized density field is too large to store entirely. Chunks allow:
- Loading only nearby terrain
- Parallel generation
- Localized updates (player edits one chunk)
- Memory management

### 5.2 Chunk Data Structure

```csharp
public class PlanetChunk {
    public Vector3Int Coordinate { get; private set; }
    public float[,,] DensityField { get; private set; }
    public Mesh Mesh { get; private set; }
    public GameObject GameObject { get; private set; }
    
    public bool IsDirty { get; set; }  // Needs mesh regeneration
    public bool IsGenerated { get; private set; }
    
    private int resolution;
    private float size;
    
    public PlanetChunk(Vector3Int coord, int resolution, float size) {
        Coordinate = coord;
        this.resolution = resolution;
        this.size = size;
        
        // +1 because we sample at corners, not centers
        DensityField = new float[resolution + 1, resolution + 1, resolution + 1];
    }
    
    public Vector3 GetWorldOrigin() {
        return new Vector3(
            Coordinate.x * size,
            Coordinate.y * size,
            Coordinate.z * size
        );
    }
    
    public Vector3 LocalToWorld(int x, int y, int z) {
        float step = size / resolution;
        return GetWorldOrigin() + new Vector3(x * step, y * step, z * step);
    }
    
    public void GenerateDensityField(DensityGenerator generator) {
        for (int x = 0; x <= resolution; x++) {
            for (int y = 0; y <= resolution; y++) {
                for (int z = 0; z <= resolution; z++) {
                    Vector3 worldPos = LocalToWorld(x, y, z);
                    DensityField[x, y, z] = generator.SampleDensity(worldPos);
                }
            }
        }
        IsGenerated = true;
        IsDirty = true;
    }
    
    public void GenerateMesh(MarchingCubes meshGenerator) {
        if (!IsGenerated) return;
        
        Mesh = meshGenerator.GenerateMesh(DensityField, size / resolution);
        IsDirty = false;
    }
    
    public void CreateGameObject(Material material, Transform parent) {
        GameObject = new GameObject($"Chunk {Coordinate}");
        GameObject.transform.parent = parent;
        GameObject.transform.position = GetWorldOrigin();
        
        var meshFilter = GameObject.AddComponent<MeshFilter>();
        var meshRenderer = GameObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = Mesh;
        meshRenderer.material = material;
    }
    
    public void Destroy() {
        if (Mesh != null) Object.Destroy(Mesh);
        if (GameObject != null) Object.Destroy(GameObject);
    }
}
```

### 5.3 Chunk Manager

```csharp
public class ChunkManager : MonoBehaviour {
    public PlanetSettings settings;
    public Material terrainMaterial;
    public Transform player;
    
    private Dictionary<Vector3Int, PlanetChunk> activeChunks = new();
    private DensityGenerator densityGenerator;
    private MarchingCubes meshGenerator;
    
    private Queue<Vector3Int> chunksToGenerate = new();
    private Queue<PlanetChunk> chunksToMesh = new();
    
    public int loadRadius = 5;  // Chunks in each direction
    public int unloadRadius = 7;  // When to unload
    
    void Start() {
        densityGenerator = new DensityGenerator(settings);
        meshGenerator = new MarchingCubes();
    }
    
    void Update() {
        UpdateLoadedChunks();
        ProcessGenerationQueue();
        ProcessMeshQueue();
    }
    
    void UpdateLoadedChunks() {
        Vector3Int playerChunk = WorldToChunkCoord(player.position);
        
        // Find chunks that need loading
        for (int x = -loadRadius; x <= loadRadius; x++) {
            for (int y = -loadRadius; y <= loadRadius; y++) {
                for (int z = -loadRadius; z <= loadRadius; z++) {
                    Vector3Int coord = playerChunk + new Vector3Int(x, y, z);
                    
                    // Skip if too far from planet surface
                    if (!ShouldChunkExist(coord)) continue;
                    
                    // Queue if not loaded
                    if (!activeChunks.ContainsKey(coord) && 
                        !chunksToGenerate.Contains(coord)) {
                        chunksToGenerate.Enqueue(coord);
                    }
                }
            }
        }
        
        // Find chunks to unload
        List<Vector3Int> toUnload = new();
        foreach (var kvp in activeChunks) {
            Vector3Int coord = kvp.Key;
            float distance = Vector3Int.Distance(coord, playerChunk);
            if (distance > unloadRadius) {
                toUnload.Add(coord);
            }
        }
        
        foreach (var coord in toUnload) {
            activeChunks[coord].Destroy();
            activeChunks.Remove(coord);
        }
    }
    
    void ProcessGenerationQueue() {
        // Generate a few chunks per frame to avoid stutters
        int chunksPerFrame = 2;
        
        for (int i = 0; i < chunksPerFrame && chunksToGenerate.Count > 0; i++) {
            Vector3Int coord = chunksToGenerate.Dequeue();
            
            if (activeChunks.ContainsKey(coord)) continue;
            
            var chunk = new PlanetChunk(coord, settings.chunkResolution, settings.chunkSize);
            chunk.GenerateDensityField(densityGenerator);
            
            activeChunks[coord] = chunk;
            chunksToMesh.Enqueue(chunk);
        }
    }
    
    void ProcessMeshQueue() {
        int meshesPerFrame = 2;
        
        for (int i = 0; i < meshesPerFrame && chunksToMesh.Count > 0; i++) {
            var chunk = chunksToMesh.Dequeue();
            
            chunk.GenerateMesh(meshGenerator);
            chunk.CreateGameObject(terrainMaterial, transform);
        }
    }
    
    Vector3Int WorldToChunkCoord(Vector3 worldPos) {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / settings.chunkSize),
            Mathf.FloorToInt(worldPos.y / settings.chunkSize),
            Mathf.FloorToInt(worldPos.z / settings.chunkSize)
        );
    }
    
    bool ShouldChunkExist(Vector3Int coord) {
        Vector3 center = (coord + Vector3.one * 0.5f) * settings.chunkSize;
        float dist = center.magnitude;
        float margin = settings.chunkSize * 2;
        
        return dist > settings.planetRadius - settings.maxDepth - margin &&
               dist < settings.planetRadius + settings.maxHeight + margin;
    }
}
```

### 5.4 Handling Chunk Boundaries

Adjacent chunks share edge vertices. For seamless meshes:

**Option A: Overlap sampling (simpler)**
```csharp
// Each chunk samples one extra layer that overlaps with neighbors
// Density values are identical, so meshes align
// Slight memory overhead
```

**Option B: Shared edge references (complex)**
```csharp
// Chunks reference neighbor's edge data
// No duplicate samples
// Requires careful synchronization
```

**Recommendation:** Start with Option A. The memory overhead is minimal and implementation is straightforward.

### 5.5 Priority-Based Loading

Load chunks closer to the player first:

```csharp
// Instead of Queue, use a priority queue
class ChunkLoadRequest : IComparable<ChunkLoadRequest> {
    public Vector3Int Coord;
    public float Priority;  // Lower = higher priority
    
    public int CompareTo(ChunkLoadRequest other) {
        return Priority.CompareTo(other.Priority);
    }
}

// Priority = distance from player
float priority = Vector3.Distance(
    chunkCenter, 
    player.position
);
```

---

## 6. Phase 4: Cave Generation

### 6.1 Cave Generation Concepts

Caves are created by adding positive values to the density field (creating air pockets inside the planet). Several approaches:

**A. 3D Perlin Worms**
- Sample 3D noise
- Where noise exceeds threshold = cave
- Creates organic, connected tunnels

**B. Cellular Automata**
- Start with random caves
- Apply smoothing rules
- Creates more natural cavern shapes

**C. Agent-Based (Worm Algorithm)**
- Simulated "worms" carve paths
- More controlled but less organic
- Good for specific tunnel layouts

### 6.2 3D Noise Cave Implementation

```csharp
public class CaveNoiseFilter : INoiseFilter {
    private NoiseSettings.CaveNoiseSettings settings;
    private Noise noise = new();
    private Noise warpNoise = new();  // For domain warping
    
    public CaveNoiseFilter(NoiseSettings.CaveNoiseSettings settings) {
        this.settings = settings;
        noise = new Noise(settings.seed);
        warpNoise = new Noise(settings.seed + 1);
    }
    
    public float Evaluate(Vector3 point) {
        // Apply domain warping for more organic shapes
        Vector3 warpedPoint = point;
        if (settings.useWarping) {
            float warpStrength = settings.warpStrength;
            warpedPoint += new Vector3(
                warpNoise.Evaluate(point + Vector3.right * 100) * warpStrength,
                warpNoise.Evaluate(point + Vector3.up * 100) * warpStrength,
                warpNoise.Evaluate(point + Vector3.forward * 100) * warpStrength
            );
        }
        
        // Multi-octave noise
        float noiseValue = 0;
        float frequency = settings.baseFrequency;
        float amplitude = 1;
        
        for (int i = 0; i < settings.octaves; i++) {
            noiseValue += noise.Evaluate(warpedPoint * frequency) * amplitude;
            frequency *= settings.lacunarity;
            amplitude *= settings.persistence;
        }
        
        // Threshold to create discrete caves
        // Returns positive value (air) where caves exist, 0 elsewhere
        float caveValue = Mathf.Max(0, noiseValue - settings.threshold);
        
        return caveValue * settings.strength;
    }
}
```

### 6.3 Cave Settings

```csharp
[System.Serializable]
public class CaveNoiseSettings {
    public int seed = 0;
    
    [Header("Noise Parameters")]
    public float baseFrequency = 0.05f;  // Lower = larger caves
    public int octaves = 3;
    public float lacunarity = 2f;
    public float persistence = 0.5f;
    
    [Header("Cave Shape")]
    [Range(0f, 1f)]
    public float threshold = 0.3f;  // Higher = smaller caves
    public float strength = 5f;      // How strongly caves carve
    
    [Header("Domain Warping")]
    public bool useWarping = true;
    public float warpStrength = 0.5f;
    
    [Header("Depth Control")]
    public float minDepth = 10f;     // No caves above this depth
    public float maxDepth = 100f;    // No caves below this depth
    public AnimationCurve depthFalloff;  // How cave density varies with depth
}
```

### 6.4 Integrating Caves into Density Function

```csharp
public float SampleDensity(Vector3 worldPoint) {
    float distanceFromCenter = worldPoint.magnitude;
    float baseDensity = distanceFromCenter - settings.planetRadius;
    
    // Terrain
    Vector3 direction = worldPoint.normalized;
    float terrainHeight = CalculateTerrainHeight(direction);
    float density = baseDensity - terrainHeight;
    
    // Caves (only underground)
    if (density < 0) {  // Only carve where already solid
        float depth = -density;  // How far underground
        
        // Only create caves within depth range
        if (depth > settings.caveSettings.minDepth && 
            depth < settings.caveSettings.maxDepth) {
            
            // Evaluate cave noise
            float caveValue = caveNoiseFilter.Evaluate(worldPoint);
            
            // Apply depth falloff
            float depthT = (depth - settings.caveSettings.minDepth) / 
                          (settings.caveSettings.maxDepth - settings.caveSettings.minDepth);
            float depthMultiplier = settings.caveSettings.depthFalloff.Evaluate(depthT);
            
            // Add cave carving (positive values create air)
            density += caveValue * depthMultiplier;
        }
    }
    
    return density;
}
```

### 6.5 Swiss Cheese vs Worm Caves

**Swiss Cheese (3D Noise):**
```csharp
// Spherical caves scattered throughout
float caveNoise = noise.Evaluate(point * frequency);
return caveNoise > threshold ? 1 : 0;
```

**Worm Caves (Directional):**
```csharp
// Long tunnels in preferred directions
// Use 2D noise sampled in multiple directions
float horizontalNoise = noise.Evaluate(new Vector3(point.x, 0, point.z) * freq);
float verticalNoise = noise.Evaluate(new Vector3(0, point.y, point.z) * freq * 0.5f);

// Combine to create horizontal tunnel preference
float tunnelValue = horizontalNoise * 0.7f + verticalNoise * 0.3f;
return tunnelValue > threshold ? 1 : 0;
```

### 6.6 Cave Entrance Generation

Ensure caves connect to the surface:

```csharp
// Near-surface cave boost
float surfaceProximity = Mathf.Abs(density);  // How close to surface
if (surfaceProximity < settings.entranceRange) {
    // Boost cave probability near surface
    float entranceBoost = 1 - (surfaceProximity / settings.entranceRange);
    caveThreshold -= entranceBoost * settings.entranceStrength;
}
```

---

## 7. Phase 5: LOD with Transvoxel

### 7.1 Why LOD Matters

At planetary scale, you can't render everything at full resolution:
- Close chunks: Full detail (32³ voxels)
- Medium distance: Half detail (16³ voxels)  
- Far chunks: Quarter detail (8³ voxels)

### 7.2 The LOD Problem

Naive LOD creates cracks at chunk boundaries:

```
Full res │ Half res
         │
•──•──•──│•────•
│  │  │  ││    │
•──•──•──│•────•     ← Crack! Vertices don't align
│  │  │  ││    │
•──•──•──│•────•
```

### 7.3 Transvoxel Solution

Transvoxel creates special "transition cells" at LOD boundaries:

```
Full res │ Transition │ Half res
         │   cells    │
•──•──•──│•──•──•────│•────•
│  │  │  │ \  |  /   ││    │
•──•──•──│•──•──•────│•────•
│  │  │  │ /  |  \   ││    │
•──•──•──│•──•──•────│•────•
```

### 7.4 LOD Level Structure

```csharp
public enum LODLevel {
    LOD0 = 0,  // Highest detail: 32³
    LOD1 = 1,  // 16³
    LOD2 = 2,  // 8³
    LOD3 = 3   // 4³ (very far)
}

public class LODSettings {
    // Distance thresholds for each LOD level
    public float[] lodDistances = { 50f, 100f, 200f, 400f };
    
    public LODLevel GetLODForDistance(float distance) {
        for (int i = 0; i < lodDistances.Length; i++) {
            if (distance < lodDistances[i]) return (LODLevel)i;
        }
        return LODLevel.LOD3;
    }
    
    public int GetResolutionForLOD(LODLevel lod, int baseResolution) {
        return baseResolution >> (int)lod;  // Divide by 2^lod
    }
}
```

### 7.5 Transvoxel Transition Cells

For each face of a chunk, check if the neighbor has different LOD:

```csharp
public class TransvoxelChunk : PlanetChunk {
    // Track which faces need transition cells
    public bool[] needsTransition = new bool[6];  // +X, -X, +Y, -Y, +Z, -Z
    
    public void UpdateTransitionFlags(Dictionary<Vector3Int, PlanetChunk> neighbors) {
        Vector3Int[] faceDirections = {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };
        
        for (int i = 0; i < 6; i++) {
            Vector3Int neighborCoord = Coordinate + faceDirections[i];
            if (neighbors.TryGetValue(neighborCoord, out var neighbor)) {
                // Need transition if neighbor has lower detail (higher LOD number)
                needsTransition[i] = neighbor.LODLevel > this.LODLevel;
            }
        }
    }
}
```

### 7.6 Transvoxel Tables

Transvoxel requires additional lookup tables for transition cells:

```csharp
// 512 transition cell cases (9 vertices: 8 corners + 1 center)
// Each face has its own transition cell configuration
public static class TransvoxelTables {
    // Regular cell tables (same as marching cubes)
    public static readonly int[] RegularCellClass = { /* 256 entries */ };
    public static readonly int[,] RegularVertexData = { /* vertex data */ };
    
    // Transition cell tables
    public static readonly int[] TransitionCellClass = { /* 512 entries */ };
    public static readonly int[,] TransitionVertexData = { /* vertex data */ };
    
    // Transition cell uses 9 sample points:
    // 8 corners (4 on high-res side, 4 on low-res side)
    // Plus up to 4 edge midpoints on high-res side
}
```

### 7.7 Simplified LOD Alternative: Geomorphing

If Transvoxel is too complex, use geomorphing:

1. Generate meshes at multiple LOD levels
2. Store vertex correspondence between levels
3. When transitioning, interpolate vertex positions
4. No cracks because vertices morph smoothly

```csharp
public class GeomorphingChunk {
    public Mesh[] lodMeshes;  // One mesh per LOD level
    public float morphProgress;  // 0-1, how far through transition
    
    void UpdateMorph(float distanceToPlayer) {
        // Calculate which LOD we're transitioning between
        LODLevel currentLOD = GetCurrentLOD(distanceToPlayer);
        LODLevel nextLOD = currentLOD + 1;
        
        // Calculate morph factor
        float lodStart = lodDistances[(int)currentLOD];
        float lodEnd = lodDistances[(int)nextLOD];
        morphProgress = (distanceToPlayer - lodStart) / (lodEnd - lodStart);
        
        // Pass to shader for vertex interpolation
        meshRenderer.material.SetFloat("_MorphProgress", morphProgress);
    }
}
```

---

## 8. Phase 6: Biomes

### 8.1 Biome Determination

Biomes can be based on:
- Latitude (distance from poles)
- Altitude (height above sea level)
- Moisture (another noise layer)
- Temperature (derived from latitude + altitude)

```csharp
public enum BiomeType {
    Ocean,
    Beach,
    Plains,
    Forest,
    Desert,
    Mountains,
    Snow,
    Caves  // Underground biome
}

public class BiomeGenerator {
    public BiomeData GetBiome(Vector3 worldPoint, float surfaceHeight) {
        Vector3 direction = worldPoint.normalized;
        float altitude = worldPoint.magnitude - planetRadius;
        
        // Latitude: 0 at equator, 1 at poles
        float latitude = Mathf.Abs(direction.y);
        
        // Temperature: hot at equator, cold at poles and high altitude
        float temperature = 1 - latitude - (altitude * 0.01f);
        temperature = Mathf.Clamp01(temperature);
        
        // Moisture: separate noise layer
        float moisture = moistureNoise.Evaluate(direction);
        
        // Biome lookup
        return BiomeLookup(temperature, moisture, altitude);
    }
    
    private BiomeData BiomeLookup(float temp, float moisture, float altitude) {
        // Underwater
        if (altitude < 0) return biomes[BiomeType.Ocean];
        
        // Beach
        if (altitude < 5) return biomes[BiomeType.Beach];
        
        // Snow (cold)
        if (temp < 0.2f) return biomes[BiomeType.Snow];
        
        // Desert (hot + dry)
        if (temp > 0.7f && moisture < 0.3f) return biomes[BiomeType.Desert];
        
        // Forest (medium temp + wet)
        if (moisture > 0.5f) return biomes[BiomeType.Forest];
        
        // Mountains (high altitude)
        if (altitude > 50) return biomes[BiomeType.Mountains];
        
        // Default
        return biomes[BiomeType.Plains];
    }
}
```

### 8.2 Biome Data Structure

```csharp
[System.Serializable]
public class BiomeData {
    public BiomeType type;
    public Color surfaceColor;
    public Color subsurfaceColor;
    
    [Header("Terrain Modification")]
    public float heightMultiplier = 1f;      // Scale terrain features
    public float roughnessMultiplier = 1f;   // More/less detail
    
    [Header("Cave Settings")]
    public float caveDensity = 1f;           // More/fewer caves
    public float caveSize = 1f;              // Larger/smaller caves
    
    [Header("Features")]
    public bool hasVegetation;
    public bool hasWater;
    public bool hasSnow;
}
```

### 8.3 Biome-Modified Density Function

```csharp
public float SampleDensity(Vector3 worldPoint) {
    Vector3 direction = worldPoint.normalized;
    float distanceFromCenter = worldPoint.magnitude;
    
    // Get preliminary surface height (for biome determination)
    float baseSurfaceHeight = CalculateTerrainHeight(direction);
    float preliminaryAltitude = distanceFromCenter - planetRadius - baseSurfaceHeight;
    
    // Determine biome
    BiomeData biome = biomeGenerator.GetBiome(worldPoint, preliminaryAltitude);
    
    // Apply biome modifiers to terrain
    float modifiedTerrainHeight = baseSurfaceHeight * biome.heightMultiplier;
    
    // Base density with biome-modified terrain
    float density = distanceFromCenter - planetRadius - modifiedTerrainHeight;
    
    // Biome-modified caves
    if (density < 0) {
        float caveValue = caveNoiseFilter.Evaluate(worldPoint);
        caveValue *= biome.caveDensity;
        density += caveValue;
    }
    
    return density;
}
```

### 8.4 Biome Blending

Avoid harsh biome boundaries by blending:

```csharp
public BiomeBlendResult GetBlendedBiome(Vector3 worldPoint) {
    // Sample biome at point and nearby points
    BiomeData center = GetBiome(worldPoint);
    
    // Sample in a small radius
    float blendRadius = 10f;
    int samples = 8;
    
    Dictionary<BiomeType, float> weights = new();
    weights[center.type] = 1f;
    
    for (int i = 0; i < samples; i++) {
        float angle = i * Mathf.PI * 2 / samples;
        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * blendRadius,
            0,
            Mathf.Sin(angle) * blendRadius
        );
        
        BiomeData nearby = GetBiome(worldPoint + offset);
        if (!weights.ContainsKey(nearby.type)) weights[nearby.type] = 0;
        weights[nearby.type] += 0.5f;
    }
    
    // Normalize weights
    float total = weights.Values.Sum();
    foreach (var key in weights.Keys.ToList()) {
        weights[key] /= total;
    }
    
    return new BiomeBlendResult { weights = weights };
}
```

### 8.5 Per-Vertex Biome Data

Store biome information in mesh for shader use:

```csharp
public class BiomeMeshGenerator : MarchingCubes {
    protected override void AddVertex(Vector3 position, Vector3 worldPos) {
        base.AddVertex(position, worldPos);
        
        // Store biome data in UV channels or vertex colors
        BiomeData biome = biomeGenerator.GetBiome(worldPos);
        
        // UV2: biome parameters
        uv2s.Add(new Vector2(biome.type, biome.heightMultiplier));
        
        // Vertex color: biome color blend
        colors.Add(biome.surfaceColor);
    }
}
```

---

## 9. Phase 7: Player Modifications

### 9.1 Terrain Modification Concept

When the player digs or builds:
1. Identify affected chunks
2. Modify density values in those chunks
3. Regenerate affected meshes
4. Save modifications for persistence

### 9.2 Modification Data Structure

```csharp
[System.Serializable]
public class TerrainModification {
    public Vector3 worldPosition;
    public float radius;
    public float strength;  // Positive = add terrain, Negative = remove
    public ModificationType type;
    
    public enum ModificationType {
        Sphere,      // Spherical brush
        Cube,        // Box brush
        Smooth       // Smoothing brush
    }
}

public class ChunkModifications {
    public Vector3Int chunkCoord;
    public List<TerrainModification> modifications = new();
    
    // Or store as delta density field
    public float[,,] densityDeltas;  // Added to base density
}
```

### 9.3 Applying Modifications

```csharp
public class ModifiableDensityGenerator : DensityGenerator {
    private Dictionary<Vector3Int, ChunkModifications> modifications = new();
    
    public override float SampleDensity(Vector3 worldPoint) {
        // Base density from procedural generation
        float density = base.SampleDensity(worldPoint);
        
        // Apply stored modifications
        Vector3Int chunkCoord = WorldToChunkCoord(worldPoint);
        
        // Check this chunk and neighbors (modifications might overlap)
        for (int dx = -1; dx <= 1; dx++) {
            for (int dy = -1; dy <= 1; dy++) {
                for (int dz = -1; dz <= 1; dz++) {
                    Vector3Int checkCoord = chunkCoord + new Vector3Int(dx, dy, dz);
                    if (modifications.TryGetValue(checkCoord, out var mods)) {
                        density += EvaluateModifications(worldPoint, mods);
                    }
                }
            }
        }
        
        return density;
    }
    
    private float EvaluateModifications(Vector3 point, ChunkModifications mods) {
        float totalDelta = 0;
        
        foreach (var mod in mods.modifications) {
            float distance = Vector3.Distance(point, mod.worldPosition);
            
            if (distance < mod.radius) {
                // Smooth falloff
                float falloff = 1 - (distance / mod.radius);
                falloff = falloff * falloff;  // Quadratic falloff
                
                totalDelta += mod.strength * falloff;
            }
        }
        
        return totalDelta;
    }
    
    public void AddModification(Vector3 worldPos, float radius, float strength) {
        Vector3Int chunkCoord = WorldToChunkCoord(worldPos);
        
        if (!modifications.ContainsKey(chunkCoord)) {
            modifications[chunkCoord] = new ChunkModifications { chunkCoord = chunkCoord };
        }
        
        modifications[chunkCoord].modifications.Add(new TerrainModification {
            worldPosition = worldPos,
            radius = radius,
            strength = strength
        });
        
        // Mark affected chunks as dirty
        MarkChunksDirty(worldPos, radius);
    }
}
```

### 9.4 Efficient Modification Storage

For many modifications, store as a delta density field:

```csharp
public class DeltaDensityChunk {
    // Sparse storage - only store non-zero deltas
    private Dictionary<Vector3Int, float> deltas = new();
    
    public void SetDelta(int x, int y, int z, float value) {
        Vector3Int key = new Vector3Int(x, y, z);
        if (Mathf.Approximately(value, 0)) {
            deltas.Remove(key);
        } else {
            deltas[key] = value;
        }
    }
    
    public float GetDelta(int x, int y, int z) {
        return deltas.TryGetValue(new Vector3Int(x, y, z), out float val) ? val : 0;
    }
    
    // For serialization
    public byte[] Serialize() {
        // Compress and save delta data
    }
}
```

### 9.5 Persistence

Save modifications to disk:

```csharp
public class TerrainPersistence {
    private string savePath;
    
    public void SaveChunkModifications(Vector3Int coord, ChunkModifications mods) {
        string filename = $"chunk_{coord.x}_{coord.y}_{coord.z}.dat";
        string path = Path.Combine(savePath, filename);
        
        using (var writer = new BinaryWriter(File.Open(path, FileMode.Create))) {
            writer.Write(mods.modifications.Count);
            foreach (var mod in mods.modifications) {
                writer.Write(mod.worldPosition.x);
                writer.Write(mod.worldPosition.y);
                writer.Write(mod.worldPosition.z);
                writer.Write(mod.radius);
                writer.Write(mod.strength);
                writer.Write((int)mod.type);
            }
        }
    }
    
    public ChunkModifications LoadChunkModifications(Vector3Int coord) {
        string filename = $"chunk_{coord.x}_{coord.y}_{coord.z}.dat";
        string path = Path.Combine(savePath, filename);
        
        if (!File.Exists(path)) return null;
        
        var mods = new ChunkModifications { chunkCoord = coord };
        
        using (var reader = new BinaryReader(File.Open(path, FileMode.Open))) {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                mods.modifications.Add(new TerrainModification {
                    worldPosition = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle()
                    ),
                    radius = reader.ReadSingle(),
                    strength = reader.ReadSingle(),
                    type = (TerrainModification.ModificationType)reader.ReadInt32()
                });
            }
        }
        
        return mods;
    }
}
```

### 9.6 Real-Time Editing Interface

```csharp
public class TerrainEditor : MonoBehaviour {
    public float brushRadius = 2f;
    public float brushStrength = 1f;
    public bool isDigging = true;  // true = remove, false = add
    
    private ModifiableDensityGenerator densityGenerator;
    private ChunkManager chunkManager;
    
    void Update() {
        if (Input.GetMouseButton(0)) {
            // Raycast to find terrain hit point
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), 
                out RaycastHit hit)) {
                
                // Apply modification
                float strength = isDigging ? brushStrength : -brushStrength;
                densityGenerator.AddModification(hit.point, brushRadius, strength);
                
                // Regenerate affected chunks
                RegenerateNearbyChunks(hit.point, brushRadius);
            }
        }
    }
    
    void RegenerateNearbyChunks(Vector3 center, float radius) {
        // Find all chunks within radius
        Vector3Int centerChunk = WorldToChunkCoord(center);
        int chunkRadius = Mathf.CeilToInt(radius / chunkSize) + 1;
        
        for (int x = -chunkRadius; x <= chunkRadius; x++) {
            for (int y = -chunkRadius; y <= chunkRadius; y++) {
                for (int z = -chunkRadius; z <= chunkRadius; z++) {
                    Vector3Int coord = centerChunk + new Vector3Int(x, y, z);
                    chunkManager.RegenerateChunk(coord);
                }
            }
        }
    }
}
```

---

## 10. Performance Considerations

### 10.1 Threading Strategy

Density field generation and mesh generation are CPU-intensive. Use Unity's Job System:

```csharp
[BurstCompile]
public struct DensityGenerationJob : IJobParallelFor {
    [ReadOnly] public NativeArray<Vector3> samplePositions;
    [WriteOnly] public NativeArray<float> densities;
    
    public float planetRadius;
    // Noise parameters...
    
    public void Execute(int index) {
        Vector3 pos = samplePositions[index];
        densities[index] = CalculateDensity(pos);
    }
    
    private float CalculateDensity(Vector3 pos) {
        // Density calculation (must be Burst-compatible)
    }
}

// Usage
var job = new DensityGenerationJob {
    samplePositions = positions,
    densities = densities,
    planetRadius = settings.radius
};
var handle = job.Schedule(positions.Length, 64);
handle.Complete();
```

### 10.2 GPU Compute Shaders

For even better performance, use compute shaders:

```hlsl
// DensityCompute.compute
#pragma kernel GenerateDensity

RWStructuredBuffer<float> densities;
float3 chunkOrigin;
float cellSize;
int resolution;
float planetRadius;

[numthreads(8, 8, 8)]
void GenerateDensity(uint3 id : SV_DispatchThreadID) {
    if (id.x > resolution || id.y > resolution || id.z > resolution) return;
    
    float3 worldPos = chunkOrigin + float3(id) * cellSize;
    float dist = length(worldPos);
    float density = dist - planetRadius;
    
    // Add noise...
    
    int index = id.x + id.y * (resolution + 1) + id.z * (resolution + 1) * (resolution + 1);
    densities[index] = density;
}
```

### 10.3 Mesh Optimization

Reduce triangle count with mesh simplification:

```csharp
public class MeshOptimizer {
    public Mesh SimplifyMesh(Mesh source, float quality) {
        // Use Unity's mesh simplification or a library like UnityMeshSimplifier
        var simplifier = new UnityMeshSimplifier.MeshSimplifier();
        simplifier.Initialize(source);
        simplifier.SimplifyMesh(quality);
        return simplifier.ToMesh();
    }
}
```

### 10.4 Memory Management

```csharp
public class ChunkPool {
    private Stack<PlanetChunk> pool = new();
    
    public PlanetChunk GetChunk() {
        if (pool.Count > 0) {
            var chunk = pool.Pop();
            chunk.Reset();
            return chunk;
        }
        return new PlanetChunk();
    }
    
    public void ReturnChunk(PlanetChunk chunk) {
        chunk.ClearData();
        pool.Push(chunk);
    }
}
```

### 10.5 Recommended Settings by Platform

| Platform | Chunk Resolution | Load Radius | Max LOD | Chunks/Frame |
|----------|-----------------|-------------|---------|--------------|
| High-End PC | 32³ | 8-12 | LOD3 | 4-8 |
| Mid PC | 16³ | 6-8 | LOD2 | 2-4 |
| Mobile | 8³ | 4-6 | LOD1 | 1-2 |

---

## Implementation Checklist

### Phase 1: Basic Marching Cubes ⬜
- [ ] Implement marching cubes algorithm
- [ ] Create edge and triangle lookup tables
- [ ] Test with simple sphere SDF
- [ ] Verify mesh generation works correctly

### Phase 2: Spherical Density Function ⬜
- [ ] Create DensityGenerator class
- [ ] Implement sphere SDF base
- [ ] Integrate existing noise system
- [ ] Test planet generation with noise

### Phase 3: Chunk Management ⬜
- [ ] Create PlanetChunk class
- [ ] Implement ChunkManager
- [ ] Add chunk loading/unloading
- [ ] Handle chunk boundaries
- [ ] Add priority-based loading

### Phase 4: Cave Generation ⬜
- [ ] Create CaveNoiseFilter
- [ ] Implement 3D cave noise
- [ ] Add depth-based cave control
- [ ] Test cave generation

### Phase 5: LOD System ⬜
- [ ] Implement basic LOD levels
- [ ] Add Transvoxel transition cells (or geomorphing)
- [ ] Test seamless LOD transitions

### Phase 6: Biomes ⬜
- [ ] Create BiomeGenerator
- [ ] Implement temperature/moisture calculation
- [ ] Add biome blending
- [ ] Integrate biome-modified terrain

### Phase 7: Player Modifications ⬜
- [ ] Create terrain modification system
- [ ] Implement real-time editing
- [ ] Add modification persistence
- [ ] Test dig/build functionality

---

## Resources

- **Marching Cubes Tables**: http://paulbourke.net/geometry/polygonise/
- **Transvoxel Algorithm**: https://transvoxel.org/
- **GPU Gems 3 - Procedural Terrain**: https://developer.nvidia.com/gpugems/gpugems3/
- **Sebastian Lague's Tutorials**: https://www.youtube.com/c/SebastianLague
