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
Cells where arriving parcels stand are always left free: the host prefers boat variants whose parcels miss the layout, and otherwise frees those cells.

## Multiplayer

The host decides the deck of the next boat and sends it to everyone as a hidden session setting.
Own layouts travel whole, so only the host needs the files; generated decks are rebuilt from the same seed on every player.

## How it works

- Blocked cells come from objects on the `StorageBlocker` layer. The crates, baskets, bottles, lamps and paddles (`Visuals/prop_*`) decorate them.
- Game layouts replace the variant list of each level with the chosen variant, so the game itself spawns it.
- Empty, own layouts and generated decks hide the game's blockers and decoration in every boat prefab.
  For own layouts and generated decks, decoration is added to the boat that arrives: large crates on 2x2 pieces,
  small crates on pairs, bottles and lamps on single cells.
  The game reads blockers only when a boat is created, so the mod holds its cells taken in the storage grid every frame
  while that boat is at the dock; the game recounts the grid whenever a parcel is placed or taken, and the mod keeps its cells taken.
- For an own layout the host only lets the boat variants come whose arriving parcels do not stand on the layout.
  If no variant fits, the cells under arriving parcels are left free.
- Heights are scaled from the remembered original values of every prefab, and also applied to the boat at the dock.
