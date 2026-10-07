using System;
using System.Collections.Generic;
using System.Globalization;
using CatLib.Logging;
using UnityEngine;

namespace StackIt.Research;

public sealed class FallTrace
{
    public const float Seconds = 1.5f;
    public const float Step = 0.1f;

    private readonly CatLogger _log;
    private readonly List<Entry> _entries = new();

    public FallTrace(CatLogger log)
    {
        _log = log;
    }

    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool value)
    {
        IsEnabled = value;
        if (!value)
        {
            _entries.Clear();
        }

        _log.Info(value ? "[Trace] Every fall is written to the log for 1.5 s: position, parent, body and carrying" : "[Trace] Falls are no longer traced");
    }

    public void Start(Entity entity, string what)
    {
        if (!IsEnabled || entity == null || entity.WasCollected)
        {
            return;
        }

        var now = Time.realtimeSinceStartup;
        var start = entity.transform.position;
        _entries.RemoveAll(entry => entry.Entity.Pointer == entity.Pointer);
        _entries.Add(new Entry(entity, start, now));
        _log.Info($"[Trace] {StackController.Name(entity)} {what}: {State(entity, start)}");
    }

    public void Update()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var now = Time.realtimeSinceStartup;
        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            try
            {
                if (entry.Entity == null || entry.Entity.WasCollected || now - entry.Since > Seconds)
                {
                    _entries.RemoveAt(index);
                    continue;
                }

                if (now < entry.Next)
                {
                    continue;
                }

                entry.Next = now + Step;
                _log.Info($"[Trace] {StackController.Name(entry.Entity)} +{(now - entry.Since).ToString("0.00", CultureInfo.InvariantCulture)} s: {State(entry.Entity, entry.Start)}");
            }
            catch (Exception exception)
            {
                _entries.RemoveAt(index);
                _log.Warning($"[Trace] Tracing a fall stopped: {exception.Message}");
            }
        }
    }

    private static string State(Entity entity, Vector3 start)
    {
        var transform = entity.transform;
        var position = transform.position;
        var parent = transform.parent;
        var physics = entity.Physics;
        var body = physics == null ? null : physics.GetComponent<Rigidbody>();
        var pickable = entity.Interactable == null ? null : entity.Interactable.Pickable;
        var carrier = pickable == null ? null : pickable.CarryingEntity;
        var store = CatLib.Game.StoreGrid.StoreOf(entity);
        var parentStore = store == null ? null : store.ParentStore;
        return string.Format(CultureInfo.InvariantCulture,
            "at ({0:0.00}, {1:0.00}, {2:0.00}), moved {3:0.00} m, parent {4}, body {5}, carried by {6}, stored in {7}",
            position.x, position.y, position.z, Vector3.Distance(position, start),
            parent == null ? "none" : parent.name,
            body == null ? "none" : (body.isKinematic ? "kinematic" : "physical"),
            carrier == null ? "nobody" : carrier.name,
            parentStore == null ? "nothing" : StackController.Name(parentStore.LinkedEntity));
    }

    private sealed class Entry
    {
        public Entry(Entity entity, Vector3 start, float since)
        {
            Entity = entity;
            Start = start;
            Since = since;
            Next = since + Step;
        }

        public Entity Entity { get; }

        public Vector3 Start { get; }

        public float Since { get; }

        public float Next { get; set; }
    }
}
