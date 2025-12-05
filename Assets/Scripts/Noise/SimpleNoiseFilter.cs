using UnityEngine;

public class SimpleNoiseFilter : INoiseFilter
{
    private NoiseSettings.SimpleNoiseSettings settings;
    private Noise noise = new();

    public SimpleNoiseFilter(NoiseSettings.SimpleNoiseSettings settings)
    {
        this.settings = settings;
    }

    public float Evaluate(Vector3 point)
    {
        // Add one to make the value range from 0 to 1 instead of -1 to 1
        float noiseValue = 0;
        float frequency = settings.BaseRoughness;
        float amplitude = 1;

        for (int i = 0; i < settings.NumLayers; i++)
        {
            float v = noise.Evaluate(point * frequency + settings.Centre);
            noiseValue += (v + 1) * 0.5f * amplitude;

            // Roughness controls how quickly the frequency increases/decreases for each layer
            frequency *= settings.Roughness;

            // Persistence controls how quickly the amplitude increases/decreases for each layer
            amplitude *= settings.Persistence;
        }

        noiseValue = Mathf.Max(0, noiseValue - settings.MinValue);

        return noiseValue * settings.Strength;
    }

}