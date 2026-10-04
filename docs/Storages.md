# Storages

How the game stacks parcels, found in its code, and `StoreGrid` in `CatLib.Game`, which reads it.

## Grids

Every place a parcel can be put, `EntityInteractableStore`, is a grid of 0.25 m cells:

- A storage: the floor of a boat, a counter, a piece of a shelf. A shelf is built from pieces like `p_Entity_Storage_2x2`,
  `2x6`, `4x8`, `4x12`; each piece is a grid of its own and the root of its stacks.
- Every parcel: its top is a grid as big as its footprint, `EntityInteractablePickable.Size`. A Standard is 2×2, a GiantPlus 5×4.

A parcel stored on a grid belongs to exactly one store, `ParentStore`. The stores form a tree from the storage up.
`StoreEntityLocal` marks the cells of the parcel as taken, makes the parcel a child of the store in the scene and remembers
its anchor cell, its turn in quarter turns and its cells (`StoredEntityInfo`). Picking a parcel up takes everything on it along.
When a store tips over by more than `TipOverAngle`, the host takes every parcel off it and they fall.

## Placing

`CanStoreEntity` on the store under the pointer decides: `Success`, `NoFit`, `TooHigh`, `TooHeavy` or `Forbidden`.
The anchor is the cell under the pointer; if the parcel does not fit there, the game nudges it up to 5 cells and tries the next turn.
`IsEntityPositionValid` places the parcel's cells around the anchor and needs every one of them inside the grid and free.
So a parcel never hangs over the edge of the parcel under it and never stands on two parcels at once. The host checks
placements of other players with the same method.

The anchor cell of a parcel side of length `n` is `n * 0.5` rounded to even: 1 → 0, 2 → 1, 3 → 2, 4 → 2, 5 → 2.

## Marks

At every change of the time of day the host checks every parcel (`CheckForParcelDamage`):

- a storage mark is damaged when the root storage does not keep that climate;
- Fragile breaks when anything stands on it;
- ContactForbidden breaks when its root storage holds more than one ContactForbidden parcel;
- any parcel is damaged when a Heavy parcel stands anywhere above it in its stack.

## StoreGrid

| Member | Gives |
|---|---|
| `TryGetSize(store, out size)` | rows and columns of the grid |
| `TryGetCellLocal`, `TryGetCellWorld` | the centre of a cell on the top surface |
| `IsFree(store, cell)` | whether the game sees a cell as free |
| `Contains(store, cell)` | whether a cell is inside the grid |
| `IsParcel(store)`, `StoreOf(entity)` | a parcel's store, the store of an entity |
| `Root(store)`, `IsInside(store, ancestor)` | the storage at the bottom of a stack |
| `CollectChildStores(store, list)` | every store standing on a store, all the way up |
| `TryGetFootprint(store, entity, anchor, yaw, list)` | the world centres of the cells a parcel takes when stored at an anchor, the same way the game computes them |
| `TryGetCellOffsets(entity, list)`, `TryGetFootprint(view, offsets, anchor, yaw, list)` | the same in two steps, for code that tries many anchors |
| `TryView(store, out view)` | a `GridView`: the size, cell centres and free cells of one grid, read straight from the arrays |
| `AnchorIndex(size)` | the anchor cell of a side |

The grids are two-dimensional IL2CPP arrays, which interop cannot type; `StoreGrid` reads them with `Il2CppArrays`.
A `GridView` checks the arrays once and then reads cells without further checks, which matters in code that runs
many times a frame; keep it for one frame at most, the game makes new arrays when a grid is built again.
It only reads. Writing cells by hand bypasses the game's bookkeeping and is best left to the game's own methods.
