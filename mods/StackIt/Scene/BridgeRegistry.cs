using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using StackIt.Logic;
using UnityEngine;

namespace StackIt.Scene;

public sealed class BridgeRegistry
{
    private readonly List<Bridge> _bridges = new();
    private readonly Dictionary<IntPtr, HashSet<long>> _reserved = new();

    public event Action Rebuilt;

    public int Count => _bridges.Count;

    public IReadOnlyList<Bridge> All => _bridges;

    public int ReservedCells => _reserved.Values.Sum(cells => cells.Count);

    public void Add(Bridge bridge)
    {
        Remove(bridge.Entity);
        _bridges.Add(bridge);
        Rebuild();
    }

    public Bridge Find(Entity entity)
    {
        if (entity == null)
        {
            return null;
        }

        var pointer = entity.Pointer;
        foreach (var bridge in _bridges)
        {
            if (bridge.Pointer == pointer)
            {
                return bridge;
            }
        }

        return null;
    }

    public bool Remove(Entity entity)
    {
        var bridge = Find(entity);
        return bridge != null && Remove(bridge);
    }

    public bool Remove(Bridge bridge)
    {
        if (!_bridges.Remove(bridge))
        {
            return false;
        }

        Rebuild();
        return true;
    }

    public void Changed() => Rebuild();

    public void Clear()
    {
        _bridges.Clear();
        _reserved.Clear();
        Rebuilt?.Invoke();
    }

    public bool HasReservations(EntityInteractableStore store) => store != null && _reserved.ContainsKey(store.Pointer);

    public bool IsReserved(EntityInteractableStore store, Vector2Int cell) =>
        store != null && _reserved.TryGetValue(store.Pointer, out var cells) && cells.Contains(CellKey.Of(cell.x, cell.y));

    public bool AnyUnder(EntityInteractableStore root)
    {
        foreach (var bridge in _bridges)
        {
            if (StoreGrid.IsAlive(bridge.Main) && StoreGrid.IsInside(bridge.Main, root))
            {
                return true;
            }
        }

        return false;
    }

    private void Rebuild()
    {
        _reserved.Clear();
        foreach (var bridge in _bridges)
        {
            foreach (var held in bridge.Held)
            {
                if (!StoreGrid.IsAlive(held.Store))
                {
                    continue;
                }

                if (!_reserved.TryGetValue(held.Store.Pointer, out var cells))
                {
                    _reserved[held.Store.Pointer] = cells = new HashSet<long>();
                }

                cells.Add(CellKey.Of(held.Cell.x, held.Cell.y));
            }
        }

        Rebuilt?.Invoke();
    }
}
