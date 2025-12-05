using UnityEngine;

[System.Serializable]
public class NoiseSettings
{
    public enum FilterType
    {
        Simple,
        Rigid,
        Cave
    }

    public FilterType filterType;

    [ConditionalHide("filterType", (int)FilterType.Simple)]
    public SimpleNoiseSettings simpleNoiseSettings;

    [ConditionalHide("filterType", (int)FilterType.Rigid)]
    public RigidNoiseSettings rigidNoiseSettings;

    [ConditionalHide("filterType", (int)FilterType.Cave)]
    public CaveNoiseSettings caveNoiseSettings;

    [System.Serializable]
    public class SimpleNoiseSettings
    {
        [Tooltip("Strength of the noise applied to the shape.\nMakes features more or less pronounced.")]
        public float Strength = 1f;

        public float BaseRoughness = 1f;

        [Tooltip("Roughness of the noise.\nHigher values create more frequent variations.")]
        public float Roughness = 2f;

        [Tooltip("Number of layers of noise to be combined.\nMore layers add more detail.")]
        [Range(1, 8)]
        public int NumLayers = 1;

        [Tooltip("Persistence of the noise.\nControls the decrease in amplitude of each successive layer.")]
        public float Persistence = 0.5f;

        public float MinValue = 0f;

        public Vector3 Centre;
    }

    [System.Serializable]
    public class RigidNoiseSettings : SimpleNoiseSettings
    {
        public float WeightMultiplier = 0.8f;
    }

    [System.Serializable]
    public class CaveNoiseSettings
    {
        [Header("General")]
        public int seed = 0;
        public CaveType caveType = CaveType.Worm;

        [Header("Noise Parameters")]
        [Tooltip("Base frequency of cave noise. Lower = larger caves.")]
        public float baseFrequency = 0.05f;
        
        [Tooltip("Number of noise octaves. More = more detail.")]
        [Range(1, 6)]
        public int octaves = 3;
        
        [Tooltip("Frequency multiplier between octaves.")]
        public float lacunarity = 2f;
        
        [Tooltip("Amplitude multiplier between octaves.")]
        [Range(0f, 1f)]
        public float persistence = 0.5f;

        [Header("Cave Shape")]
        [Tooltip("Noise threshold for cave creation. Higher = smaller/fewer caves.")]
        [Range(-1f, 1f)]
        public float threshold = 0.3f;
        
        [Tooltip("How strongly caves carve into terrain.")]
        public float strength = 5f;

        [Header("Domain Warping")]
        [Tooltip("Apply domain warping for more organic shapes.")]
        public bool useWarping = true;
        
        [Tooltip("Strength of domain warping distortion.")]
        public float warpStrength = 0.5f;

        [Header("Fracture Settings")]
        [Tooltip("Vertical stretch for fracture caves. Lower = taller cracks.")]
        [Range(0.1f, 1f)]
        public float fractureVerticalScale = 0.3f;
        
        [Tooltip("Horizontal scale for fracture caves. Higher = thinner cracks.")]
        [Range(1f, 5f)]
        public float fractureHorizontalScale = 2f;
        
        [Tooltip("Edge sharpness for fracture caves. Higher = sharper edges.")]
        [Range(1f, 4f)]
        public float fractureSharpness = 2f;

        [Header("Stratified Settings")]
        [Tooltip("Scale of horizontal layers for stratified caves.")]
        [Range(0.5f, 5f)]
        public float stratifiedLayerScale = 2f;

        [Header("Sponge Settings")]
        [Tooltip("How connected sponge caves are. Higher = more tunnels between pockets.")]
        [Range(0f, 1f)]
        public float spongeConnectivity = 0.3f;

        /// <summary>
        /// Creates default settings for a specific cave type.
        /// </summary>
        public static CaveNoiseSettings CreateDefault(CaveType type)
        {
            var settings = new CaveNoiseSettings { caveType = type };

            switch (type)
            {
                case CaveType.Worm:
                    settings.baseFrequency = 0.05f;
                    settings.octaves = 3;
                    settings.threshold = 0.3f;
                    settings.strength = 5f;
                    settings.useWarping = true;
                    settings.warpStrength = 0.5f;
                    break;

                case CaveType.Cavern:
                    settings.baseFrequency = 0.02f;
                    settings.octaves = 2;
                    settings.threshold = 0.4f;
                    settings.strength = 8f;
                    settings.useWarping = true;
                    settings.warpStrength = 1f;
                    break;

                case CaveType.Fracture:
                    settings.baseFrequency = 0.08f;
                    settings.octaves = 2;
                    settings.threshold = 0.5f;
                    settings.strength = 4f;
                    settings.useWarping = false;
                    settings.fractureVerticalScale = 0.2f;
                    settings.fractureHorizontalScale = 3f;
                    settings.fractureSharpness = 2.5f;
                    break;

                case CaveType.Stratified:
                    settings.baseFrequency = 0.04f;
                    settings.octaves = 2;
                    settings.threshold = 0.35f;
                    settings.strength = 6f;
                    settings.useWarping = true;
                    settings.warpStrength = 0.3f;
                    settings.stratifiedLayerScale = 3f;
                    break;

                case CaveType.Sponge:
                    settings.baseFrequency = 0.1f;
                    settings.octaves = 2;
                    settings.threshold = 0.25f;
                    settings.strength = 4f;
                    settings.useWarping = true;
                    settings.warpStrength = 0.4f;
                    settings.spongeConnectivity = 0.4f;
                    break;

                case CaveType.Hybrid:
                    settings.baseFrequency = 0.05f;
                    settings.octaves = 3;
                    settings.threshold = 0.3f;
                    settings.strength = 5f;
                    settings.useWarping = true;
                    settings.warpStrength = 0.5f;
                    break;
            }

            return settings;
        }
    }
}