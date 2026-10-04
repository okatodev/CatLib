using System;
using System.Collections.Generic;
using CatLib.Game;
using StackIt.Logic;
using UnityEngine;

namespace StackIt.Scene;

public sealed class SupportFinder
{
    public const float BoundsMargin = 0.2f;

    private readonly GridCache _grids;
    private readonly BridgeRegistry _registry;
    private readonly Dictionary<IntPtr, List<Candidate>> _byRoot = new();
    private readonly List<EntityInteractableStore> _scratch = new();
    private int _frame = -1;

    public SupportFinder(GridCache grids, BridgeRegistry registry)
    {
        _grids = grids;
        _registry = registry;
    }

    public void Invalidate()
    {
        _byRoot.Clear();
        _frame = -1;
    }

    public bool TryFind(EntityInteractableStore root, Vector3 point, EntityInteractableStore main, EntityInteractableStore excluded,
        out EntityInteractableStore support, out Vector2Int cell)
    {
        support = null;
        cell = Vector2Int.zero;
        if (!StoreGrid.IsAlive(root))
        {
            return false;
        }

        foreach (var candidate in Candidates(root))
        {
            if (!candidate.Covers(point) || !StoreGrid.IsAlive(candidate.Store)
                || candidate.Store.Pointer == main.Pointer || (excluded != null && StoreGrid.IsInside(candidate.Store, excluded))
                || !_grids.TryView(candidate.Store, out var view))
            {
                continue;
            }

            var index = candidate.Store.GetGridPosition(point, false);
            if (!view.TryGetCellWorld(index, out var center) || !BridgeRules.IsLevel(center.y, point.y)
                || !BridgeRules.IsAligned(center.x - point.x, center.z - point.z))
            {
                continue;
            }

            if (!view.IsFree(index) || _registry.IsReserved(candidate.Store, index))
            {
                continue;
            }

            support = candidate.Store;
            cell = index;
            return true;
        }

        return false;
    }

    private List<Candidate> Candidates(EntityInteractableStore root)
    {
        if (Time.frameCount != _frame)
        {
            _byRoot.Clear();
            _frame = Time.frameCount;
        }

        if (_byRoot.TryGetValue(root.Pointer, out var cached))
        {
            return cached;
        }

        _scratch.Clear();
        StoreGrid.CollectChildStores(root, _scratch);
        var result = new List<Candidate>();
        foreach (var store in _scratch)
        {
            if (!StoreGrid.IsParcel(store) || !_grids.TryView(store, out var view) || view.Rows == 0 || view.Columns == 0)
            {
                continue;
            }

            var candidate = new Candidate(store);
            foreach (var corner in new[] { new Vector2Int(0, 0), new Vector2Int(view.Rows - 1, 0), new Vector2Int(0, view.Columns - 1), new Vector2Int(view.Rows - 1, view.Columns - 1) })
            {
                if (view.TryGetCellWorld(corner, out var world))
                {
                    candidate.Include(world);
                }
            }

            if (candidate.HasBounds)
            {
                result.Add(candidate);
            }
        }

        _byRoot[root.Pointer] = result;
        return result;
    }

    private sealed class Candidate
    {
        private float _minX;
        private float _maxX;
        private float _minZ;
        private float _maxZ;

        public Candidate(EntityInteractableStore store)
        {
            Store = store;
        }

        public EntityInteractableStore Store { get; }

        public bool HasBounds { get; private set; }

        public void Include(Vector3 world)
        {
            if (!HasBounds)
            {
                HasBounds = true;
                _minX = _maxX = world.x;
                _minZ = _maxZ = world.z;
                return;
            }

            _minX = Math.Min(_minX, world.x);
            _maxX = Math.Max(_maxX, world.x);
            _minZ = Math.Min(_minZ, world.z);
            _maxZ = Math.Max(_maxZ, world.z);
        }

        public bool Covers(Vector3 point) =>
            point.x >= _minX - BoundsMargin && point.x <= _maxX + BoundsMargin && point.z >= _minZ - BoundsMargin && point.z <= _maxZ + BoundsMargin;
    }
}
