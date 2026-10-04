using System;
using System.Collections.Generic;
using CatLib.Game;
using UnityEngine;

namespace StackIt.Scene;

public sealed class GridCache
{
    private readonly Dictionary<IntPtr, GridView> _views = new();
    private readonly Dictionary<IntPtr, List<Vector3>> _offsets = new();
    private int _frame = -1;

    public void Invalidate()
    {
        _views.Clear();
        _offsets.Clear();
        _frame = -1;
    }

    public bool TryView(EntityInteractableStore store, out GridView view)
    {
        view = default;
        if (!StoreGrid.IsAlive(store))
        {
            return false;
        }

        Refresh();
        if (_views.TryGetValue(store.Pointer, out view))
        {
            return view.IsValid;
        }

        StoreGrid.TryView(store, out view);
        _views[store.Pointer] = view;
        return view.IsValid;
    }

    public IReadOnlyList<Vector3> Offsets(Entity entity)
    {
        if (entity == null || entity.WasCollected)
        {
            return null;
        }

        Refresh();
        if (_offsets.TryGetValue(entity.Pointer, out var cached))
        {
            return cached;
        }

        var offsets = new List<Vector3>();
        var result = StoreGrid.TryGetCellOffsets(entity, offsets) ? offsets : null;
        _offsets[entity.Pointer] = result;
        return result;
    }

    private void Refresh()
    {
        if (Time.frameCount == _frame)
        {
            return;
        }

        _views.Clear();
        _offsets.Clear();
        _frame = Time.frameCount;
    }
}
