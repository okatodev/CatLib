using UnityEngine;

namespace StackIt.Scene;

public sealed class Slide
{
    public Slide(Entity entity, Rigidbody body, Vector3 from, Vector3 to, Vector3 direction, float start)
    {
        Entity = entity;
        Body = body;
        From = from;
        To = to;
        Direction = direction;
        Start = start;
    }

    public Entity Entity { get; }

    public Rigidbody Body { get; }

    public Vector3 From { get; }

    public Vector3 To { get; }

    public Vector3 Direction { get; }

    public float Start { get; }
}
