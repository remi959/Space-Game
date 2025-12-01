using UnityEngine;

/// <summary>
/// Deterministic hash functions for procedural generation.
/// Used by CaveGenerator, NoiseGenerator, and any seeded random placement.
/// </summary>
public static class HashUtility
{
    /// <summary>
    /// Hashes 3D integer coordinates to a float [0, 1].
    /// </summary>
    public static float Hash3DToFloat(int x, int y, int z, int seed)
    {
        int hash = x * 374761393 + y * 668265263 + z * 1274126177 + seed;
        hash = (hash ^ (hash >> 13)) * 1274126177;
        hash = hash ^ (hash >> 16);
        return (hash & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    /// <summary>
    /// Hashes 3D integer coordinates to an int.
    /// </summary>
    public static int Hash3DToInt(int x, int y, int z, int seed)
    {
        return x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed;
    }

    /// <summary>
    /// Hashes a Vector3Int to an int.
    /// </summary>
    public static int HashVector3Int(Vector3Int v, int seed = 0)
    {
        return Hash3DToInt(v.x, v.y, v.z, seed);
    }

    /// <summary>
    /// Creates a bounded seed offset for noise sampling (prevents float precision issues).
    /// </summary>
    public static Vector3 GetSeedOffset(int seed)
    {
        int hash = (int)((seed * 747796405 + 2891336453) & 0x7FFFFFFF);
        float x = hash % 10000 * 0.1f;
        float y = hash / 10000 % 10000 * 0.1f;
        float z = hash / 100000000 % 10000 * 0.1f;
        return new Vector3(x, y, z);
    }
}