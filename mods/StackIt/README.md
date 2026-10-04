# Stack it!

Parcels that stand across the joint of two or more parcels below them, like they would on the floor of a boat.

Not released yet. For now the mod only has research commands in the developer menu; it changes nothing in the game.

## Why

In the game every parcel is a small shelf of its own: its top is a grid of 0.25 m cells, as big as its footprint.
A parcel put on top belongs to exactly one parcel below, and every cell of it must lie on that one grid.
So two 3×2 parcels side by side make a flat 6×2 top, but a 2×2 parcel cannot stand in its middle, across the joint,
and a 5×2 parcel cannot stand on it at all. The floor of a boat or a shelf level is one big grid, where all of this works.

## The rules

- A parcel may stand on several parcels at once when their tops are level, within a few millimetres, and it lies
  fully on them, without hanging over empty space.
- The supports stand on the same shelf level or on the same boat.
- In the game's tree the parcel belongs to the support under its centre; the cells it takes on the other supports
  are marked as taken.
- When any support is taken away, the parcel falls, the way the game drops parcels from a stack that tips over.
  A setting keeps it standing instead while its centre is still above a support; then it goes along with the support under its centre.
- Height, weight and the marks treat the parcel as lying on all its supports, so a heavy parcel across a fragile one
  still damages it.
- Every player needs the mod (`RequiredOnAll`): the host and the clients check placements with the same grids.

## Research commands

The developer menu has the group **Stack it!**:

| Command | Writes to the log |
|---|---|
| Parcel grids in the log | the grid on top of one parcel of every size next to its footprint, and the height of its top |
| Storages in the log | every shelf level, boat and counter with its grid, limits and the tree of parcels on it |
| Level tops in the log | for every shelf level and boat, the parcels on it grouped by the height of their tops |
