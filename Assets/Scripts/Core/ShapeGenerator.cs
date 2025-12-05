using UnityEngine;

public class ShapeGenerator
{
    private ShapeSettings settings;
    private INoiseFilter[] noiseFilters;

    public ShapeGenerator(ShapeSettings settings)
    {
        this.settings = settings;
        noiseFilters = new INoiseFilter[settings.NoiseLayers.Length]; 
        for (int i = 0; i < noiseFilters.Length; i++)
        {
            noiseFilters[i] = NoiseFilterFactory.CreateNoiseFilter(settings.NoiseLayers[i].Settings);
        }
    }

    public Vector3 CalculatePointOnPlanet(Vector3 pointOnUnitSphere)
    {
        float firstLayerValue = 0;
        float elevation = 0;

        if (noiseFilters.Length > 0)
        {
            firstLayerValue = noiseFilters[0].Evaluate(pointOnUnitSphere);
            if (settings.NoiseLayers[0].Enabled)
            {
                elevation = firstLayerValue;
            }
        }

        // Start at 1 to skip the first layer since it's already applied
        for (int i = 1; i < noiseFilters.Length; i++)
        {
            if (settings.NoiseLayers[i].Enabled)
            {
                float mask = settings.NoiseLayers[i].UseFirstLayerAsMask ? firstLayerValue : 1;
                elevation += noiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
            }
        }

        return (1 + elevation) * settings.PlanetRadius * pointOnUnitSphere;
    }
}