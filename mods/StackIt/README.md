# Stack it!

Parcels that stand across the joint of two or more parcels below them, like they would on the floor of a boat.

The mod changes how parcels are stacked, so it is `RequiredOnAll`: the host and every player need it.

## Why

In the game every parcel is a small shelf of its own: its top is a grid of 0.25 m cells, as big as its footprint.
A parcel put on top belongs to exactly one parcel below, and every cell of it must lie on that one grid.
So two 3×2 parcels side by side make a flat 6×2 top, but a 2×2 parcel cannot stand in its middle, across the joint,
and a 5×2 parcel cannot stand on it at all. A storage, the floor of a boat or a piece of a shelf, is one big grid, where all of this works.
A shelf is built from several storages (2×2, 2×6, 4×8 and others), and each of them is a grid of its own.

## The rules

- A parcel may stand on several parcels at once when their tops are level, within 2 cm, and it lies fully on them,
  without hanging over empty space. Standard parcels are 9 mm lower than the other parcels of their height, and two of them
  are 18 mm lower than a Tall one; all of that counts as level.
- All the parcels under it stand in the same storage: the same piece of a shelf, the same boat, the same counter.
- In the game's tree the parcel belongs to the parcel under its centre, the one you point at when you put it down.
  The cells it covers on the other parcels are held by the mod, so nothing else can be put there.
- When any parcel under it is taken away, the host lets it fall, the way the game drops parcels from a stack that tips over.
  It slides towards the side that is gone until its centre passes the edge of what is left under it, and tips over;
  a parcel resting mostly on one parcel falls too. A fragile parcel can break when it lands.
  The host does this at the moment the parcel under it is picked up, so it never rides along in the player's hands.
- With **Keep balance** on, it stays while its centre is above a parcel, and goes along with the parcel under its centre.
  The mod remembers every part of it that hangs over nothing: when Keep balance is turned off, such parcels fall.
- Nothing can be put into the space under a part that hangs over nothing. A parcel whose top is level with it fits there
  and holds it up again.
- A stack that is carried as a whole, with the parcel under the centre and the side parcels on one parcel below them, keeps its bridges.
  A parcel under it counts as taken only when it leaves the storage, so a boat that sways or turns,
  or parcels that settle into place after loading, move nothing.
- After loading, cells over a side whose parcel is not found within 5 s of the start of the level hang over nothing,
  so without Keep balance the parcel falls then.
- The marks treat the parcel as lying on all of them: a fragile parcel breaks when a parcel stands across it,
  and every parcel under a heavy one is damaged, across joints too, like in the game. The height and weight limits are the game's own.

## Saves

The game saves a parcel across a joint as a parcel on the one under its centre, with only the cells on that parcel.
A save made with the mod loads without it: such a parcel then simply stands on the parcel under its centre and sticks out over the others.
With the mod, the parcels under it are found again after loading.

## Settings

Both settings come from the host.

| Setting | Default | What it does |
|---|---|---|
| Stand across joints | on | Parcels may stand across the joint of level parcels. Off: no new ones; those standing keep their rules |
| Keep balance | off | Off: a parcel across a joint falls when any parcel under it is taken. On: it stays while its centre is supported |

## For developers

The mod patches four methods of the game with `CatPatches` from CatLib, see [Patching](../../docs/Patching.md):

| Method | Patch |
|---|---|
| `EntityInteractableStore.IsEntityPositionValid` | a placement the game refuses is allowed across a joint; a placement on cells held by a bridge is refused |
| `EntityInteractableStore.StoreEntityLocal` | a parcel stored partly outside the grid under it becomes a bridge, the parcels under the other cells are found |
| `EntityInteractableStore.RemoveEntityLocal` | a bridge taken from its parcel is forgotten |
| `EntityProperties.CheckBehaviorConstraint` | fragile and heavy marks see bridges, on the host, where the game checks them |

Every frame the mod checks the parcels under every bridge. Only the host lets bridges fall, with the game's own `RemoveEntity`,
which tells every player. How the game stacks parcels is described in [Storages](../../docs/Storages.md).

The developer menu has the group **Stack it!**:

| Command | Writes to the log |
|---|---|
| Bridges in the log | every parcel that stands across a joint, the parcels under it and the cells it holds on them |
| Parcel grids in the log | the grid on top of one parcel of every size next to its footprint, and the height of its top |
| Storages in the log | every storage with its grid, limits and the tree of parcels on it |
| Level tops in the log | for every storage, the parcels on it grouped by the height of their tops |
