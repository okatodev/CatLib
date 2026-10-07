# CatLib

Foundation library for Cat Mail Co. mods.

CatLib does nothing to the game by itself. Mods are built on top of it, so when a mod needs it, your mod manager installs it for you.

## For players

- **Mods tab.** Open Settings and you'll find a Mods tab with every installed mod, its icon, author, version and what it does.
  Change settings right there and most of them apply straight away. Point at a setting to see what it does and what the default is.
  Each mod has a reset button.
- **Your language.** Mods use the game's language, all 13 of them.
  If a translation reads wrong, you can fix it yourself with a small file, no need to wait for an update.
- **Playing with friends.** In the lobby, every player's mods are listed next to the host's, so you can see who has what.
  If someone is missing a mod, that mod is paused for the game instead of breaking it, and comes back once everyone has it.
- **Your saves are safe.** Mods keep their data in their own files next to your save. The game's save itself is never touched,
  so removing a mod leaves it exactly as it was.
- **When the game crashes.** A small window tells you what happened and keeps a report you can send to the mod's author.
  It also notices when the game freezes while quitting. This part is the CatLib Crash Watcher package, installed together with CatLib.
- **After a game update.** A small paper label in the corner of the main menu shows the CatLib version; point at it to see your mods.
  If the game has been updated and CatLib hasn't caught up yet, or your game is older than CatLib expects, it says so there.

CatLib's own settings are on the Mods tab too: the crash window, and whether a player with other mods can stay in your lobby.

## For mod developers

Everything below is documented in the repository.

- **Settings.** `CatSettings.For(this)` declares settings that apply live while the game runs and appear on the Mods tab
  with translated names, sliders, dropdowns and restart marks. Session settings come from the host.
- **Translations.** Put `Lang/en.json`, `Lang/ru.json` and so on next to the project. Plural forms, the game language
  and translation files from players are handled, and a check reports missing or broken texts.
- **Multiplayer.** `CatNetwork.Declare` puts your mod into the compatibility check over Steam. You get the session roster,
  paused mods when someone lacks yours, and `CatNetwork.Channel` for messages between your mod's copies.
- **Saves.** `CatSaves.For(this)` stores data per game save, written with the game's save, atomically and with a backup.
  Only the host writes. The game's save file is never opened.
- **Game events.** Loading, saving, the network and the player as plain .NET events, with their real order documented.
- **UI.** `Notifications.Show` for messages in the game's style, `FoldoutList` for lists built from the game's own UI,
  `HudLayer` and `CountTable` for small tables on the screen during a level.
- **Parcels and storages.** `CatParcels` lists every parcel of the level with its marks, size, damage and place, for every player.
  `StoreGrid` gives the grids of shelves and parcels, computed like the game does.
- **Patching.** `CatPatches` installs Harmony patches all or nothing and turns them off after repeated errors;
  `CodePatch` changes a few bytes of the game's code, found by a pattern and checked before every change.
- **Game build.** `GameCompatibility.Status` tells whether the running game is the build CatLib was made for.
- **Developer menu.** `DevMenu.Command` adds your tools to one in-game panel on the `` ` `` key.
- **Crash reports.** Crashes in your mod come with a report, both logs and a memory dump.

Depend on `CatLib-CatLib-<version>` in your manifest. Start with [Writing a mod](https://github.com/okatodev/CatLib/blob/main/docs/WritingMods.md).

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues). Tell us what you did, which mods you had, and whether you were playing alone, hosting or joining.

- **The game crashed:** press **Open the report folder** in the crash window, zip that folder and attach it.
  Older reports are in `BepInEx/CatLib/Crashes`. Nothing is sent anywhere by itself.
- **Something else went wrong:** attach `BepInEx/LogOutput.log` before you start the game again, because the next start overwrites it.
