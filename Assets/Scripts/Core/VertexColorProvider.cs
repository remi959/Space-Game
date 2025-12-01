using UnityEngine;

/// <summary>
/// Single source of truth for vertex coloring logic.
/// Handles biome colors and cave color blending.
/// </summary>
public static class VertexColorProvider
{
    public static void ApplyBiomeColors(
        MeshData meshData,
        Transform chunkTransform,
        Vector3 planetCenter,
        float planetRadius,
        BiomeManager biomeManager,
        CaveGenerator caveGenerator,
        int seed)
    {
        if (biomeManager == null) return;

        foreach (Vector3 localVertex in meshData.vertices)
        {
            Vector3 worldPos = chunkTransform.position + localVertex;
            Vector3 normalizedPos = (worldPos - planetCenter).normalized;

            Color vertexColor = CalculateBiomeColor(biomeManager, normalizedPos, seed);
            vertexColor = ApplyCaveColor(vertexColor, worldPos, planetCenter, planetRadius, caveGenerator);

            vertexColor.a = 1f;
            meshData.colors.Add(vertexColor);
        }
    }

    private static Color CalculateBiomeColor(BiomeManager biomeManager, Vector3 normalizedPos, int seed)
    {
        BiomeWeight[] biomeWeights = biomeManager.GetBiomesAt(normalizedPos, seed);

        if (biomeWeights.Length == 0) return Color.gray;

        Color vertexColor = Color.black;
        float totalWeight = 0f;

        foreach (var bw in biomeWeights)
        {
            if (bw.biome != null)
            {
                vertexColor += bw.biome.debugColor * bw.weight;
                totalWeight += bw.weight;
            }
        }

        return totalWeight > 0f ? vertexColor / totalWeight : Color.gray;
    }

    private static Color ApplyCaveColor(Color surfaceColor, Vector3 worldPos, Vector3 planetCenter, float planetRadius, CaveGenerator caveGenerator)
    {
        if (caveGenerator == null || !caveGenerator.enableCaves) return surfaceColor;

        CaveGenerator.CaveColorInfo caveInfo = caveGenerator.GetCaveColorInfo(worldPos, planetCenter, planetRadius);

        if (caveInfo.isInCave)
        {
            return Color.Lerp(surfaceColor, caveInfo.color, caveInfo.blendWeight);
        }

        return surfaceColor;
    }
}