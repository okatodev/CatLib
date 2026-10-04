using UnityEngine;

namespace StackIt.Scene;

public sealed class HangingCell
{
    public HangingCell(Vector3 local, float since, bool lost)
    {
        Local = local;
        Since = since;
        Lost = lost;
    }

    public Vector3 Local { get; }

    public float Since { get; }

    public bool Lost { get; }
}
