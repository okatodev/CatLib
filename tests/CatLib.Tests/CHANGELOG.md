# Changelog

## 0.7.3

- Developer menu group Spawn: a new parcel of any of the twelve sizes in front of you, its destination and marks chosen by the game.
  Works for the host and for the players in a level.
- Developer menu group Game, for the host: the next step of every pile of parcels, or every pile cleared step by step.
  The parcels still lying on a step are removed, the doors behind the piles open at their steps like in the game.
- Developer menu group UI: hide the game's interface for screenshots and videos, with the pause menu, settings, results and stamp choice still shown.
  While it is hidden, a second toggle decides whether the notifications are hidden too.

## 0.7.2
- Updated BepInEx dependency version.
## 0.7.1

- Tests of Custom Stamps and of the asset API: PNG and JPG images, pack files, content packs and their cards, Thunderstore packages.
- Tests of the crash watcher's method names, symbols and stacks, and of the game without the Steam network.
- Depends on Custom Stamps too.

## 0.7.0

- Tests of Parcel Board, Stack it! and Too Late, and of the new parts of CatLib: parcels, storages, patches, the HUD and the version label.
- Tests of a mod that is not installed are skipped and counted apart.
- Developer menu group Game, for the host in a level: serve the customers waiting at the counters, the next part of the day,
  and skip to the day's results.

## 0.6.2

- First package. Runs the tests of CatLib, Boat Tweaks, Shelf Labels and Better Repair when the main menu loads
  and from the developer menu, and writes reports to `BepInEx/CatLib.Tests/Reports`.
- Developer menu commands: entity, settings menu and lobby dumps, sprite and texture export, network probes,
  the session roster, sample notifications, the version badge preview, test crashes and a hang on quit.
