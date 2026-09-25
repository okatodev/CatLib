# Boat Tweaks

Changes the boat that takes the parcels away: the pre-placed parcels on its deck and the height limits of stacks.
Every setting is a session setting: in multiplayer the host's values apply to everyone, and every player needs the mod.

## Settings

| Section | Setting | Meaning |
|---|---|---|
| Deck layout | Mode | As in the game, Empty, Game layouts, One game layout |
| Game layouts | Allowed layouts | Numbers of the game boat variants that may come, for example `1,2,4`. Empty means all |
| Game layouts | No repeats in a row | The same variant never comes twice in a row |
| One game layout | Layout number | The variant that comes every time; the last one if a level has fewer |
| Height | Approved height | Multiplier for the height above which a stack turns red |
| Height | Maximum height | Multiplier for the absolute limit; the approved height never exceeds it |

Settings of a section only work in its own mode, so they never conflict.
Parcels that arrive with the boat are gameplay and are never changed by any mode.

## How it works

The game picks the boat for each delivery from lists of variants per player count and progression level.
A variant is a storage prefab with its own deck decoration, its own blockers and its own arriving parcels.

- Blocked cells come from objects on the `StorageBlocker` layer under the storage's `Colliders`.
  The crates, baskets, bottles, lamps and paddles under `Visuals` (`prop_*`) only decorate them.
- Empty hides the blockers and the decoration of every boat prefab, so the whole deck is free. Arriving parcels stay.
- Game layouts and One game layout replace the variant list of each level with the chosen variant before the boat comes,
  so the game itself spawns that variant with everything it carries. After each boat the next variant is chosen.
- Heights are scaled from the remembered original values of every prefab, and also applied to the boat at the dock.

Every change is made to prefabs, so it applies from the next boat; the original values are restored when a mode is turned off.

Planned: own layouts from JSON files with saving the current deck from the game, generated layouts, and a mix of sources.
