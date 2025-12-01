# Procedural Planet Generation System

## Complete Technical Documentation

This document provides a comprehensive explanation of the procedural planet generation system, covering Marching Cubes mesh generation, biome systems, cave generation, and why the mesh produces a spherical shape.

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Why Is the Mesh Spherical?](#why-is-the-mesh-spherical)
3. [Marching Cubes Algorithm](#marching-cubes-algorithm)
4. [Density Functions](#density-functions)
5. [Biome System](#biome-system)
6. [Cave Generation](#cave-generation)
7. [Performance Considerations](#performance-considerations)
8. [Common Issues and Solutions](#common-issues-and-solutions)

---

## System Overview

The procedural planet system creates a 3D terrain mesh using a technique called **implicit surface extraction**. Instead of directly defining vertices and triangles, we define a mathematical function (the "density function") that describes where solid matter exists in 3D space.

### Core Pipeline

```
1. Density Function → 2. Chunk System → 3. Marching Cubes → 4. Mesh Output
      ↓                     ↓                  ↓                  ↓
  Mathematical          16×16×16           Lookup tables      Triangles
  definition of       voxel grids          convert density    rendered by
  terrain shape       sample density       to triangles       Unity
```

### Key Components

| Component | File | Purpose |
|-----------|------|---------|
| ProceduralPlanet | ProceduralPlanet.cs | Main controller, density calculation |
| Chunk | Chunk.cs | 16×16×16 voxel region management |
| MarchingCubes | MarchingCubes.cs | Density-to-mesh conversion |
| BiomeManager | BiomeManager.cs | Biome blending and terrain variation |
| CaveGenerator | CaveGenerator.cs | Subtractive cave carving |
| NoiseGenerator | NoiseGenerator.cs | Simplex noise for natural variation |

---

## Why Is the Mesh Spherical?

This is one of the most common questions about the system. The answer lies in understanding the **density function**.

### The Density Function

In `ProceduralPlanet.GetDensityAt()`, the base density is calculated as:

```csharp
float baseDensity = planetRadius - distanceFromCenter;
```

This single line creates the sphere. Here's why:

**Density Convention:**
- **Positive density** = solid matter (inside the planet)
- **Negative density** = empty space (outside the planet)
- **Zero density** = the surface

**What the formula does:**

For a planet with radius 50:
- Point 30 units from center: `density = 50 - 30 = +20` (solid, inside)
- Point 50 units from center: `density = 50 - 50 = 0` (surface)
- Point 70 units from center: `density = 50 - 70 = -20` (air, outside)

This creates an **implicit sphere** - every point exactly `planetRadius` units from the center has density = 0, which defines a perfect sphere.

### Visual Representation

```
                    Outside (negative density)
                    ↓
        - - - - - - - - - - - - -
       /                         \
      /    + + + + + + + + + +    \
     |    + + + + + + + + + + +    |    ← Surface (density = 0)
     |   + + + + SOLID + + + + +   |
     |    + + + + + + + + + + +    |
      \    + + + + + + + + + +    /
       \                         /
        - - - - - - - - - - - - -
                    ↑
        Inside (positive density)
```

### Adding Terrain Features

Noise is added to create mountains and valleys:

```csharp
float terrainDensity = baseDensity + totalNoise * surfaceBlend;
```

When noise adds positive values, the surface moves outward (mountains).
When noise adds negative values (inverted layers), the surface moves inward (craters).

**The `surfaceBlend` factor** ensures noise only affects areas near the surface:

```csharp
float surfaceBlend = Mathf.Clamp01(1f - Mathf.Abs(baseDensity) / surfaceBlendDistance);
```

- At the surface (`baseDensity ≈ 0`): `surfaceBlend ≈ 1` (full noise effect)
- Deep inside (`baseDensity >> 0`): `surfaceBlend ≈ 0` (no noise effect)
- Far outside (`baseDensity << 0`): `surfaceBlend ≈ 0` (no noise effect)

This prevents noise from creating floating islands or hollow cores.

---

## Marching Cubes Algorithm

Marching Cubes is an algorithm that converts a 3D scalar field (density values) into a triangle mesh. It was developed in 1987 by Lorensen and Cline.

### How It Works

**Step 1: Divide Space into Cubes**

The algorithm processes the density field one cube at a time. Each cube has 8 corners (vertices).

```
      4 --------- 5
     /|          /|
    / |         / |
   7 --------- 6  |
   |  |        |  |
   |  0 -------|-- 1
   | /         | /
   |/          |/
   3 --------- 2
```

**Step 2: Sample Density at Corners**

For each cube, we check the density at all 8 corners.

```csharp
for (int i = 0; i < 8; i++)
{
    Vector3Int corner = position + CornerOffsets[i];
    cubeCorners[i] = densities[corner.x, corner.y, corner.z];
}
```

**Step 3: Build Cube Index**

Each corner is classified as "inside" (density > 0) or "outside" (density ≤ 0). This creates a binary pattern with 2⁸ = 256 possible combinations.

```csharp
int cubeIndex = 0;
for (int i = 0; i < 8; i++)
{
    if (cubeCorners[i] > SURFACE_LEVEL)
    {
        cubeIndex |= 1 << i;
    }
}
```

Example:
- Corners 0, 1, 2, 3 inside (bottom half): `cubeIndex = 0b00001111 = 15`
- All corners inside: `cubeIndex = 0b11111111 = 255`
- All corners outside: `cubeIndex = 0b00000000 = 0`

**Step 4: Edge Table Lookup**

The `EdgeTable` tells us which of the 12 edges have surface crossings (where density transitions from positive to negative).

```csharp
int edges = EdgeTable[cubeIndex];
```

The 12 edges of a cube:
- Edges 0-3: Bottom face
- Edges 4-7: Top face
- Edges 8-11: Vertical edges

**Step 5: Calculate Vertex Positions**

For each crossed edge, we interpolate to find where the surface crosses:

```csharp
float t = (SURFACE_LEVEL - density1) / (density2 - density1);
edgeVertices[i] = Vector3.Lerp(pos1, pos2, t);
```

This interpolation creates smooth surfaces instead of blocky voxels.

**Step 6: Triangle Table Lookup**

The `TriangleTable` tells us how to connect the edge vertices into triangles.

```csharp
for (int i = 0; TriangleTable[cubeIndex, i] != -1; i += 3)
{
    // Add triangle with vertices at edges:
    // TriangleTable[cubeIndex, i], TriangleTable[cubeIndex, i+1], TriangleTable[cubeIndex, i+2]
}
```

### Lookup Tables

The algorithm uses two precomputed tables:

**EdgeTable (256 entries):**
Each entry is a 12-bit bitmask indicating which edges are crossed.

**TriangleTable (256 × 16 entries):**
Each entry lists the edge indices that form triangles, terminated by -1.

Example for `cubeIndex = 1` (only corner 0 inside):
- EdgeTable[1] = 0b000100001001 = edges 0, 3, 8 crossed
- TriangleTable[1] = {0, 8, 3, -1, ...} = one triangle using edges 0, 8, 3

### Performance Optimization

The implementation includes several optimizations:

1. **Static arrays** reused across calls to avoid allocation
2. **Early exit** when cubeIndex is 0 or 255 (no surface)
3. **Chunk-level culling** skips entirely solid/empty chunks

---

## Density Functions

The density function is the mathematical heart of the system. It determines the shape of every surface.

### Base Density (Sphere)

```csharp
float baseDensity = planetRadius - distanceFromCenter;
```

### Adding Terrain Layers

Noise layers modify the base density:

```csharp
foreach (var layer in noiseLayers)
{
    float noise = NoiseGenerator.Sample3D(worldPos, layer.settings, seed);
    totalNoise += noise;
}
float terrainDensity = baseDensity + totalNoise * surfaceBlend;
```

### Biome Contribution

Biomes provide per-region terrain variation:

```csharp
foreach (var biomeWeight in biomeWeights)
{
    float biomeNoise = EvaluateBiomeLayers(biomeWeight.biome);
    biomeNoise *= biomeWeight.biome.heightMultiplier;
    biomeNoise += biomeWeight.biome.heightOffset;
    totalNoise += biomeNoise * biomeWeight.weight;
}
```

### Cave Subtraction

Caves subtract from density (make it more negative):

```csharp
float caveDensity = caveGenerator.GetCaveDensity(...);  // Returns negative values
return terrainDensity + caveDensity;
```

When cave density makes the total negative, that area becomes air (a cave).

---

## Biome System

The biome system divides the planet into regions with different terrain characteristics.

### Biome Selection

Biomes are selected using 3D noise:

```csharp
Vector3 samplePoint = normalizedPosition * sampleRadius;
float rawNoise = NoiseGenerator.Sample3D(samplePoint, biomeNoise, seed);
float noiseValue = (rawNoise + 1f) * 0.5f;  // Normalize to 0-1
```

The normalized noise value selects which biome(s) are active:
- 3 biomes: ranges 0-0.33, 0.33-0.66, 0.66-1.0

### Biome Blending

At biome boundaries, smooth blending prevents jarring transitions:

```csharp
// Near boundary: calculate blend weights
float t = distToBoundary / blendWidth;
float primaryWeight = 0.5f + 0.5f * Smoothstep(t);
```

The smoothstep function creates an S-curve: `t² × (3 - 2t)`

**Blend Weight Distribution:**
- At exact boundary: 50% / 50%
- At blend edge: 100% / 0%

### Biome Properties

Each biome defines:

| Property | Effect |
|----------|--------|
| terrainLayers | Noise layers specific to this biome |
| heightMultiplier | Scales all terrain features |
| heightOffset | Adds constant height (plateaus) |
| debugColor | Vertex color for visualization |

---

## Cave Generation

Caves are created by subtracting density from the terrain.

### Cave Types

**1. Worm Caves (Tunnels)**

Long, winding passages created by 3D noise:

```csharp
float wormValue = NoiseGenerator.Sample3D(worldPos, wormNoise, seed);
wormValue = (wormValue + 1f) * 0.5f;  // Normalize to 0-1

if (wormValue > wormThreshold)
{
    float caveStrength = (wormValue - wormThreshold) / (1f - wormThreshold);
    return -caveStrength * wormWidth;  // Negative = carve out
}
```

The threshold creates sparse caves; the width controls tunnel size.

**2. Room Caves (Chambers)**

Spherical chambers placed on a grid:

```csharp
// Deterministic room position from cell coordinates
Vector3 roomCenter = GetRoomCenterInCell(cellCoord, seed);

// Check if this cell has a room
float roomChance = NoiseGenerator.Sample3D(roomCenter, roomPositionNoise, seed);
if (roomChance > roomThreshold)
{
    // Calculate spherical carving
    float normalizedDist = distanceToRoom / roomRadius;
    float carveStrength = (1 - normalizedDist)²;  // Quadratic falloff
    return -carveStrength * roomRadius * caveDensity;
}
```

**3. Tunnel Connections**

Tunnels connecting nearby rooms:

```csharp
// Find closest point on line between two rooms
Vector3 closestPoint = roomA + tunnelDir * t;

// Add curvature for natural appearance
float curveStrength = sin(t/length × π) * tunnelCurvature;
closestPoint += perpendicular * curveStrength;

// Carve tunnel
if (distToTunnel < tunnelRadius)
{
    return -carveStrength * tunnelRadius * caveDensity;
}
```

### Depth Control

Caves only spawn within a depth range:

```csharp
float depthBelowSurface = (planetRadius + maxTerrainHeight) - distanceFromCenter;

if (depthBelowSurface < minDepth || depthBelowSurface > maxDepth)
{
    return 0f;  // No cave at this depth
}
```

**Important:** The depth calculation accounts for `maxTerrainHeight` because the actual surface can be higher than `planetRadius` due to terrain features.

---

## Performance Considerations

### Chunk System

The world is divided into 16×16×16 chunks:
- Each chunk stores density values at 17×17×17 points (corners of 16³ cubes)
- Chunks are loaded/unloaded based on player distance
- Modified chunks are queued for batched mesh updates

### Optimization Techniques

**1. Dictionary-Based Chunk Lookup (O(1))**
```csharp
private Dictionary<Vector3Int, Chunk> chunkLookup;
```

**2. Early Exit for Empty/Solid Chunks**
```csharp
if (minDensity > 0 || maxDensity < 0)
{
    // Entire chunk is solid or empty, skip marching cubes
    return;
}
```

**3. Bounded Voxel Iteration**
During terrain modification, only affected voxels are updated:
```csharp
int minX = Mathf.Max(0, Mathf.FloorToInt(localPos.x - radius / VoxelSize) - 1);
int maxX = Mathf.Min(SIZE, Mathf.CeilToInt(localPos.x + radius / VoxelSize) + 1);
```

**4. Deferred Collider Updates**
Collider updates are expensive; they're batched with a small delay.

**5. Cave Room Caching**
Room existence checks are cached to avoid redundant noise calculations.

---

## Common Issues and Solutions

### Issue: Caves Don't Go Deep Enough

**Cause:** Depth was calculated from `planetRadius`, not accounting for terrain height.

**Solution:** Use `effectiveSurfaceRadius = planetRadius + maxTerrainHeight`:
```csharp
float depthBelowSurface = (planetRadius + maxTerrainHeight) - distanceFromCenter;
```

### Issue: Player Falls Through Terrain During Editing

**Cause:** Collider update delay creates a window where mesh is updated but collider isn't.

**Solution:** Enable immediate collider updates during terrain modification:
```csharp
if (immediateCollider)
{
    requireImmediateColliderUpdate = true;
}
```

### Issue: Tunnel Connections Freeze Generation

**Cause:** 6-level nested loops (3×3×3 × 3×3×3 = 729 iterations) with expensive room checks.

**Solution:** Collect nearby rooms first, then iterate pairs:
```csharp
// Step 1: Collect all rooms in neighborhood (27 iterations)
for each cell in 3×3×3:
    if (cell has room): nearbyRooms.Add(room)

// Step 2: Check pairs (n² iterations, but n is small)
for i = 0 to nearbyRooms.Count:
    for j = i+1 to nearbyRooms.Count:
        checkTunnelContribution(rooms[i], rooms[j])
```

### Issue: Only One Biome Appears

**Cause:** `biomeNoise.minValue = 0` clamps negative noise, clustering values around 0.5.

**Solution:** Set `biomeNoise.minValue = -1` to allow full noise range.

### Issue: Biome Colors Not Visible

**Cause:** Standard shader ignores vertex colors.

**Solution:** Use a custom shader that outputs vertex colors or multiply them with albedo.

---

## Summary

The procedural planet system creates spherical terrain through:

1. **Density function**: `planetRadius - distanceFromCenter` defines a sphere
2. **Noise layers**: Add natural terrain variation
3. **Biomes**: Regional terrain characteristics with smooth blending
4. **Caves**: Subtractive carving using negative density
5. **Marching Cubes**: Converts density field to triangle mesh
6. **Chunk system**: Manages performance through spatial partitioning

The key insight is that **everything is density**. Positive density is solid, negative is air, and zero is the surface. By manipulating density with noise and cave functions, we create complex, natural-looking planetary terrain.