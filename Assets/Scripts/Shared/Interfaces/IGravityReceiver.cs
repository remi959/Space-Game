using UnityEngine;

public interface IGravityReceiver
{
    IGravityEmitter PrimaryEmitter { get; }
    Transform GetTransform { get; }
    Rigidbody GetRigidbody { get; }
    void SetEmitter(IGravityEmitter emitter);
}