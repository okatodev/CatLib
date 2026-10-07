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

    public bool HasPose { get; private set; }

    public Vector3 Position { get; private set; }

    public Quaternion Rotation { get; private set; }

    public void RememberPose(Transform transform)
    {
        Position = transform.position;
        Rotation = transform.rotation;
        HasPose = true;
    }

    public float CarriedSince { get; set; } = -1f;

    public bool HasFallPlan { get; set; }

    public StackIt.Logic.SlidePlan FallPlan { get; set; }

    public int PlannedAt { get; set; } = -1000;

    public void ForgetPose() => HasPose = false;

    public IntPtr Pointer => Entity == null ? IntPtr.Zero : Entity.Pointer;
}
