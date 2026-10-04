using UnityEngine;

namespace StackIt.Scene;

public sealed class HeldCell
{
    public HeldCell(EntityInteractableStore store, Vector2Int cell, Vector3 local)
    {
        Store = store;
        Cell = cell;
        Local = local;
    }

    public EntityInteractableStore Store { get; }

    public Vector2Int Cell { get; }

    public Vector3 Local { get; set; }
}
