using System;
using System.Collections.Generic;
using CatLib.Il2Cpp;
using UnityEngine;

namespace CatLib.Game;

public static class StoreGrid
{
    public const float CellSize = 0.25f;
    public const int MaxDepth = 32;

    public static int AnchorIndex(int size) => (int)Math.Round((double)(size * 0.5f), MidpointRounding.ToEven);

    public static bool IsAlive(EntityInteractableStore store) => store != null && !store.WasCollected;

    public static bool TryGetSize(EntityInteractableStore store, out Vector2Int size)
    {
        size = Vector2Int.zero;
        if (!IsAlive(store) || !Il2CppArrays.TryGetBounds(store._grid, out var rows, out var columns))
        {
            return false;
        }

        size = new Vector2Int(rows, columns);
        return true;
    }

    public static bool TryView(EntityInteractableStore store, out GridView view)
    {
        view = default;
        if (!IsAlive(store))
        {
            return false;
        }

        var cells = store._grid;
        if (!Il2CppArrays.TryGetRank(cells, out var rank, out var cellSize) || rank != 2 || cellSize != 12
            || !Il2CppArrays.TryGetBounds(cells, out var rows, out var columns))
        {
            return false;
        }

        var free = store._availableCells;
        var freeData = IntPtr.Zero;
        if (Il2CppArrays.TryGetRank(free, out var freeRank, out var freeSize) && freeRank == 2 && freeSize == 1
            && Il2CppArrays.TryGetBounds(free, out var freeRows, out var freeColumns) && freeRows == rows && freeColumns == columns)
        {
            freeData = free.Pointer + Il2CppArrays.DataOffset;
        }
        else
        {
            free = null;
        }

        view = new GridView(store, cells, free, rows, columns, cells.Pointer + Il2CppArrays.DataOffset, freeData);
        return true;
    }

    public static bool Contains(EntityInteractableStore store, Vector2Int cell) =>
        TryGetSize(store, out var size) && cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;

    public static bool TryGetCellLocal(EntityInteractableStore store, Vector2Int cell, out Vector3 local)
    {
        local = Vector3.zero;
        return IsAlive(store) && Il2CppArrays.TryGet(store._grid, cell.x, cell.y, out local);
    }

    public static bool TryGetCellWorld(EntityInteractableStore store, Vector2Int cell, out Vector3 world)
    {
        world = Vector3.zero;
        if (!TryGetCellLocal(store, cell, out var local))
        {
            return false;
        }

        world = store.transform.TransformPoint(local);
        return true;
    }

    public static bool IsFree(EntityInteractableStore store, Vector2Int cell) =>
        IsAlive(store) && Il2CppArrays.TryGet(store._availableCells, cell.x, cell.y, out bool free) && free;

    public static bool IsParcel(EntityInteractableStore store)
    {
        if (!IsAlive(store))
        {
            return false;
        }

        var entity = store.LinkedEntity;
        return entity != null && !entity.WasCollected && !entity.IsDisposed && entity.Properties != null;
    }

    public static EntityInteractableStore StoreOf(Entity entity)
    {
        if (entity == null || entity.WasCollected || entity.IsDisposed)
        {
            return null;
        }

        var interactable = entity.Interactable;
        var store = interactable == null ? null : interactable.Store;
        return IsAlive(store) ? store : null;
    }

    public static EntityInteractableStore Root(EntityInteractableStore store)
    {
        var current = store;
        for (var depth = 0; depth < MaxDepth && IsAlive(current) && IsAlive(current.ParentStore); depth++)
        {
            current = current.ParentStore;
        }

        return IsAlive(current) ? current : null;
    }

    public static bool IsInside(EntityInteractableStore store, EntityInteractableStore ancestor)
    {
        if (!IsAlive(ancestor))
        {
            return false;
        }

        var current = store;
        for (var depth = 0; depth < MaxDepth && IsAlive(current); depth++)
        {
            if (current.Pointer == ancestor.Pointer)
            {
                return true;
            }

            current = current.ParentStore;
        }

        return false;
    }

    public static void CollectChildStores(EntityInteractableStore store, List<EntityInteractableStore> result) => Collect(store, result, 0);

    public static bool TryGetCellOffsets(Entity entity, List<Vector3> offsets)
    {
        offsets.Clear();
        var interactable = entity == null || entity.WasCollected ? null : entity.Interactable;
        var pickable = interactable == null ? null : interactable.Pickable;
        if (pickable == null)
        {
            return false;
        }

        var size = pickable.Size;
        if (size.x <= 0 || size.y <= 0)
        {
            return false;
        }

        var anchorCell = pickable.GetCell(AnchorIndex(size.x), AnchorIndex(size.y));
        for (var x = 0; x < size.x; x++)
        {
            for (var y = 0; y < size.y; y++)
            {
                offsets.Add(pickable.GetCell(x, y) - anchorCell);
            }
        }

        return true;
    }

    public static bool TryGetFootprint(GridView view, IReadOnlyList<Vector3> offsets, Vector2Int anchor, int yawSteps, List<Vector3> cells)
    {
        cells.Clear();
        if (!view.TryGetCellWorld(anchor, out var worldAnchor))
        {
            return false;
        }

        var rotation = view.Store.transform.rotation;
        foreach (var offset in offsets)
        {
            cells.Add(worldAnchor + rotation * EntityInteractableStore.RotateLocalOffset90Steps(offset, yawSteps));
        }

        return cells.Count > 0;
    }

    public static bool TryGetFootprint(EntityInteractableStore store, Entity entity, Vector2Int anchor, int yawSteps, List<Vector3> cells)
    {
        cells.Clear();
        var offsets = new List<Vector3>();
        return TryView(store, out var view) && TryGetCellOffsets(entity, offsets) && TryGetFootprint(view, offsets, anchor, yawSteps, cells);
    }

    private static void Collect(EntityInteractableStore store, List<EntityInteractableStore> result, int depth)
    {
        if (!IsAlive(store) || depth >= MaxDepth)
        {
            return;
        }

        var children = store.GetChildren();
        for (var index = 0; children != null && index < children.Count; index++)
        {
            var child = StoreOf(children[index]);
            if (child == null)
            {
                continue;
            }

            result.Add(child);
            Collect(child, result, depth + 1);
        }
    }
}
