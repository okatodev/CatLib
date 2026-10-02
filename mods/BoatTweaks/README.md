# Boat Tweaks

Changes the boat that takes the parcels away: the fixed crates, baskets and other blockers on its deck, and the height limits of stacks.
Parcels that arrive with the boat are gameplay and are never changed.
Every setting except the save hotkey is a session setting: in multiplayer the host's values apply to everyone, and every player needs the mod.

## Modes

| Mode | Deck |
|---|---|
| As in the game | unchanged |
| Empty | no blockers and no decoration |
| Game layouts | the game's boat variants, only the allowed numbers, optionally without repeats |
| One game layout | always the same game variant |
| Own layouts | layouts from files, at random, one after another or always one file |
| Generated | random blockers with a set density, near the rails or anywhere |
| Mixed | each boat picks one of the above by weight: game deck, empty, own layouts, generated |

Settings of a section only work in its own mode, so they never conflict.

## Own layouts

Files are `BepInEx/config/BoatTweaks/layouts/*.txt`. A layout is a grid of rows where `#` is a taken cell and `.` is free:

```
Any line with other characters is a note
..######
..######
........
```

The boat deck is 8 by 8 cells on most levels; a layout of another size is skipped for that boat.
The easiest way to make one: switch to Empty, put parcels where the blockers should be, then press the save hotkey (Ctrl+B by default).
Every taken cell of the boat at the dock is saved except the cells of the parcels that arrived with it, so take off other parcels that should not be part of the layout first.
Cells where arriving parcels stand are always left free: the host prefers boat variants whose parcels miss the layout.
A layout is cut into pieces, the biggest first: 4x5, 4x4, 2x5, 3x3, 2x2, 1x2 and single cells. A piece that an arriving parcel touches is left out whole,
so a square never turns into a smaller odd shape.
If every variant has parcels on the layout, which is common for boats of two or more players, only the variants that free the fewest cells,
and the same cells, come, so the deck looks the same every time, and a notification tells how many cells stay free.

## Multiplayer

The host decides the deck of the next boat and sends it to everyone as a hidden session setting.
Only a player with `CatNetwork.IsAuthority` decides, which is the host or a single player; a client builds exactly what the host planned
and, until the host has accepted it, keeps the game deck.
Own layouts travel whole, so only the host needs the files; generated decks are rebuilt from the same seed on every player.
Decoration is a personal setting: with `Decoration/Enabled` off the blocked cells stay taken but bare, for that player only.

## How it works

- Blocked cells come from objects on the `StorageBlocker` layer. The crates, baskets, bottles, lamps and paddles (`Visuals/prop_*`) decorate them.
- Own and generated decks get plain box colliders on the `StorageBlocker` layer, one per cell, not copies of the game's blocker objects.
- Game layouts replace the variant list of each level with the chosen variant, so the game itself spawns it.
- Empty, own layouts and generated decks hide the game's blockers and decoration in every boat prefab.
  For own layouts and generated decks, decoration is added to the boat that arrives: every crate, bottle and lamp of the game's boats
  decorates pieces of its size, measured from its model: 4x5, 4x4, 2x5, 3x3, 2x2, 1x2 and single cells. Rope coils and paddles are not used.
  A piece whose size has no prop gets smaller props that cover it. The log lists which props are used.
  Generated decks are made of whole pieces, mostly squares and pairs, like the game's own decks; big 3x3 and long 2x5 crates come where they fit.
  The game reads blockers only when a boat is created, so the mod holds its cells taken in the storage grid every frame
  while that boat is at the dock; the game recounts the grid whenever a parcel is placed or taken, and the mod keeps its cells taken.
- For an own layout the host only lets the boat variants come whose arriving parcels do not stand on the layout.
  If no variant fits, the closest variants come and the pieces under their arriving parcels are left free.
- Objects of the boat prefabs that carry game entities are never hidden, so the game's network identifiers are not affected.
- From the start of a game restart until the next level loads the mod changes nothing, so the level is never touched while it is destroyed.
- Heights are scaled from the remembered original values of every prefab and applied to every boat in the scene, after which the game recomputes whether the stack is approved.
  The red mark of the approved height moves with it, also on a boat that is already at the dock.
- The developer menu has **Boat Tweaks → Boat heights**, which writes the limits of every boat in the scene to the log.

## Changes

See [CHANGELOG.md](CHANGELOG.md).
