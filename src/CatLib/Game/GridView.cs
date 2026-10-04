using System;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace CatLib.Game;

public readonly unsafe struct GridView
{
    private readonly Il2CppObjectBase _cells;
    private readonly Il2CppObjectBase _free;
    private readonly IntPtr _cellData;
    private readonly IntPtr _freeData;

    internal GridView(EntityInteractableStore store, Il2CppObjectBase cells, Il2CppObjectBase free, int rows, int columns, IntPtr cellData, IntPtr freeData)
    {
        Store = store;
        _cells = cells;
        _free = free;
        Rows = rows;
        Columns = columns;
        _cellData = cellData;
        _freeData = freeData;
    }

    public EntityInteractableStore Store { get; }

    public int Rows { get; }

    public int Columns { get; }

    public bool IsValid => Store != null && _cellData != IntPtr.Zero && _cells != null;

    public bool HasFreeCells => _freeData != IntPtr.Zero && _free != null;

    public bool Contains(Vector2Int cell) => IsValid && cell.x >= 0 && cell.y >= 0 && cell.x < Rows && cell.y < Columns;

    public bool TryGetCellLocal(Vector2Int cell, out Vector3 local)
    {
        local = Vector3.zero;
        if (!Contains(cell))
        {
            return false;
        }

        local = ((Vector3*)_cellData)[cell.x * Columns + cell.y];
        return true;
    }

    public bool TryGetCellWorld(Vector2Int cell, out Vector3 world)
    {
        world = Vector3.zero;
        if (!TryGetCellLocal(cell, out var local) || !StoreGrid.IsAlive(Store))
        {
            return false;
        }

        world = Store.transform.TransformPoint(local);
        return true;
    }

    public bool IsFree(Vector2Int cell) => Contains(cell) && HasFreeCells && ((byte*)_freeData)[cell.x * Columns + cell.y] != 0;
}
