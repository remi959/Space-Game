using UnityEngine;

public interface IGravityEmitter
{
    Transform GetTransform { get; }
    public float GetMass { get; }
    public float GetSOI { get; }
}