using System;
using System.Collections.Generic;
using UnityEngine;

namespace StackIt.Scene;

public sealed class Bridge
{
    public Bridge(Entity entity, EntityInteractableStore main, Vector2Int anchor, int yaw, IntPtr root, float since)
    {
        Entity = entity;
        Main = main;
        Anchor = anchor;
        Yaw = yaw;
        Root = root;
        Since = since;
    }

    public Entity Entity { get; }

    public EntityInteractableStore Main { get; }

    public Vector2Int Anchor { get; }

    public int Yaw { get; }

    public IntPtr Root { get; set; }

    public float Since { get; }

    public List<HeldCell> Held { get; } = new();

    public List<HangingCell> Hanging { get; } = new();

    public float Height { get; set; }

    public bool Waiting { get; set; }

    public IntPtr Pointer => Entity == null ? IntPtr.Zero : Entity.Pointer;
}
