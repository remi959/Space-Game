using UnityEngine;

[CreateAssetMenu(menuName = "Planet/Cave Shape Settings")]
public class CaveSettings : ScriptableObject
{
    [Header("Depth Control")]
    [Tooltip("Minimum depth below surface for caves to appear")]
    public float MinDepth = 10f;

    [Tooltip("Maximum depth below surface for caves")]
    public float MaxDepth = 100f;

    [Tooltip("How cave density varies with depth (X=0 is minDepth, X=1 is maxDepth)")]
    public AnimationCurve DepthFalloff = new(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Cave Noise")]
    [Tooltip("Noise layers that define cave shape")]
    public CaveNoiseLayer[] NoiseLayers;

    void OnValidate()
    {
        MinDepth = Mathf.Max(0f, MinDepth);
        MaxDepth = Mathf.Max(MinDepth + 1f, MaxDepth);
    }

    void Reset()
    {
        // Called when the asset is first created - set up good defaults
        MinDepth = 1f;
        MaxDepth = 50f;
        DepthFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);  // Full strength at min depth, fades out
    }


}