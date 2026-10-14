using UnityEngine;

namespace StackIt.Scene;

public sealed class HostWait
{
    public HostWait(Entity entity, Rigidbody body, float since)
    {
        Entity = entity;
        Body = body;
        Since = since;
    }

    public Entity Entity { get; }

    public Rigidbody Body { get; }

    public float Since { get; }
}
