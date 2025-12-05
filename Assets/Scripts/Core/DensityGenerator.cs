using UnityEngine;

public class DensityGenerator
{
    private ShapeSettings terrainSettings;
    private INoiseFilter[] terrainNoiseFilters;

    private CaveSettings caveSettings;
    private CaveNoiseFilter[] caveNoiseFilters;

    public DensityGenerator(ShapeSettings settings, CaveSettings caveSettings)
    {
        this.terrainSettings = settings;
        this.caveSettings = caveSettings;
        InitializeNoiseFilters();
    }

    private void InitializeNoiseFilters()
    {
        // Initialize terrain noise filters
        terrainNoiseFilters = new INoiseFilter[terrainSettings.NoiseLayers.Length];
        for (int i = 0; i < terrainNoiseFilters.Length; i++)
        {
            terrainNoiseFilters[i] = NoiseFilterFactory.CreateNoiseFilter(
                terrainSettings.NoiseLayers[i].Settings
            );
        }

        // Initialize cave noise filters
        if (caveSettings != null && caveSettings.NoiseLayers != null)
        {
            caveNoiseFilters = new CaveNoiseFilter[caveSettings.NoiseLayers.Length];
            for (int i = 0; i < caveNoiseFilters.Length; i++)
            {
                caveNoiseFilters[i] = new CaveNoiseFilter(caveSettings.NoiseLayers[i].Settings);
            }
        }
        else
        {
            caveNoiseFilters = new CaveNoiseFilter[0];
        }
    }

    public float SampleDensity(Vector3 worldPoint)
    {
        float distanceFromCenter = worldPoint.magnitude;
        float baseDensity = distanceFromCenter - terrainSettings.PlanetRadius;

        // Terrain
        Vector3 direction = worldPoint.normalized;
        float terrainHeight = CalculateTerrainHeight(direction);
        float density = baseDensity - terrainHeight;

        // Caves (only underground and if cave system is configured)
        if (density < 0 && caveSettings != null && caveNoiseFilters.Length > 0)
        {
            float depth = -density;

            if (depth > caveSettings.MinDepth && depth < caveSettings.MaxDepth)
            {
                float caveValue = CalculateCaveValue(worldPoint);

                // Apply depth falloff
                float depthT = (depth - caveSettings.MinDepth) /
                              (caveSettings.MaxDepth - caveSettings.MinDepth);
                float depthMultiplier = caveSettings.DepthFalloff.Evaluate(depthT);

                density += caveValue * depthMultiplier;
            }
        }

        return density;
    }

    private float CalculateTerrainHeight(Vector3 pointOnUnitSphere)
    {
        float firstLayerValue = 0;
        float elevation = 0;

        if (terrainNoiseFilters.Length > 0)
        {
            firstLayerValue = terrainNoiseFilters[0].Evaluate(pointOnUnitSphere);
            if (terrainSettings.NoiseLayers[0].Enabled)
            {
                elevation = firstLayerValue;
            }
        }

        for (int i = 1; i < terrainNoiseFilters.Length; i++)
        {
            if (terrainSettings.NoiseLayers[i].Enabled)
            {
                float mask = terrainSettings.NoiseLayers[i].UseFirstLayerAsMask
                    ? firstLayerValue : 1;
                elevation += terrainNoiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
            }
        }

        return elevation;
    }

    private float CalculateCaveValue(Vector3 worldPoint)
    {
        if (caveNoiseFilters.Length == 0) return 0f;

        float firstLayerValue = 0f;
        float totalCaveValue = 0f;
        float totalWeight = 0f;

        // Evaluate first layer
        if (caveSettings.NoiseLayers[0].Enabled)
        {
            firstLayerValue = caveNoiseFilters[0].Evaluate(worldPoint);
            totalCaveValue = firstLayerValue * caveSettings.NoiseLayers[0].BlendWeight;
            totalWeight = caveSettings.NoiseLayers[0].BlendWeight;
        }

        // Evaluate additional layers
        for (int i = 1; i < caveNoiseFilters.Length; i++)
        {
            var layer = caveSettings.NoiseLayers[i];
            if (!layer.Enabled) continue;

            float layerValue = caveNoiseFilters[i].Evaluate(worldPoint);

            // Apply mask if enabled
            if (layer.UseFirstLayerAsMask)
            {
                layerValue *= Mathf.Clamp01(firstLayerValue);
            }

            // Blend based on weight and blend mode
            totalCaveValue += layerValue * layer.BlendWeight;
            totalWeight += layer.BlendWeight;
        }

        // Normalize by total weight
        if (totalWeight > 0f)
        {
            totalCaveValue /= totalWeight;
        }

        return totalCaveValue;
    }

    public Vector3 CalculateNormal(Vector3 point, float epsilon = 0.01f)
    {
        float dx = SampleDensity(point + Vector3.right * epsilon) -
                   SampleDensity(point - Vector3.right * epsilon);
        float dy = SampleDensity(point + Vector3.up * epsilon) -
                   SampleDensity(point - Vector3.up * epsilon);
        float dz = SampleDensity(point + Vector3.forward * epsilon) -
                   SampleDensity(point - Vector3.forward * epsilon);

        return -new Vector3(dx, dy, dz).normalized;
    }
}