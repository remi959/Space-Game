using System;
using UnityEngine;

public class RigidNoiseFilter : INoiseFilter
{
    private NoiseSettings.RigidNoiseSettings settings;
    private Noise noise = new();

    public RigidNoiseFilter(NoiseSettings.RigidNoiseSettings settings)
    {
        this.settings = settings;
    }

    public float Evaluate(Vector3 point)
    {
        // Add one to make the value range from 0 to 1 instead of -1 to 1
        float noiseValue = 0;
        float frequency = settings.BaseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < settings.NumLayers; i++)
        {
            // Invert the noise by taking the absolute value and subtracting from 1
            float v = 1 - Mathf.Abs(noise.Evaluate(point * frequency + settings.Centre));

            // Square the value to create sharper ridges
            v *= v;

            // Apply weight from previous layer to create more pronounced ridges
            v *= weight;

            // Safe current value to use as weight for the next layer
            weight = Mathf.Clamp01(v * settings.WeightMultiplier);

            // No normalization factor needed since inverted value is used
            noiseValue += v * amplitude;

            // Roughness controls how quickly the frequency increases/decreases for each layer
            frequency *= settings.Roughness;

            // Persistence controls how quickly the amplitude increases/decreases for each layer
            amplitude *= settings.Persistence;
        }

        noiseValue = Mathf.Max(0, noiseValue - settings.MinValue);

        return noiseValue * settings.Strength;
    }

}