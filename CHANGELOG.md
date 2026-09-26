# Changelog

CatLib follows semantic versioning. Mods in `mods/` have their own versions and changes in their README files.

## Unreleased

### Added

- `CatLib.Saves`: mod data per game save in `CatLibSaves` next to the game's save folder (`CatSaves.For`, `ModSave`).
  Written only after the game saved successfully and only by the host, atomically with a backup;
  damaged files are set aside, newer data is never overwritten, a new save never inherits data left under its name.
  See [Mod data in game saves](docs/Saves.md).
- Messages between mods: `CatNetwork.Channel` with `SendToHost`, `Broadcast`, `SendTo`, `PeerJoined` and `PeerLeft`.
  Only between the host and players that share the mod, with local delivery in single player and on the host,
  and a limit of 60 messages per second per player on the host. See [Mod messages](docs/Network.md#mod-messages).

### Changed

- Notifications are wrapped by the width the game's notification font measures, not by a character count,
  so a line never runs past the screen edge when the notification settles. Lines are not broken inside quotes.
- Network protocol 4: the verdict lists the mods both sides share. Players with an older CatLib are reported as incompatible.
- The F7 developer key sends a mod message probe instead of the old game protocol probes.

## 0.5.0

For mod authors: localization, safe access to two-dimensional IL2CPP arrays, player notifications and the session role.

### Added

- `CatLib.Localization`: a translation catalog per mod (`CatLocalization.For`, `CatSettings.Texts`) loaded from JSON files or embedded resources,
  lookup with fallback from a regional language to its base language and to English, `CatLanguage.Current` following the game language.
- The Mods tab takes the mod name, section names, setting labels, descriptions and dropdown values from the mod's catalog,
  and rebuilds its texts when the game language changes.
- `Il2CppArrays` in `CatLib.Il2Cpp`: reads and writes two-dimensional IL2CPP arrays that interop only exposes as `Il2CppObjectBase`.
  Every call checks the array rank, the element size against the requested type and the bounds.
- `Notifications.Show` in `CatLib.UI` shows a mod's message as a game notification in a level and in the Mods tab status line.
- `CatNetwork.Role` (`Offline`, `Host`, `Client`) and `CatNetwork.IsAuthority` for mods whose decisions must match for every player.
- `Setting<T>.LocalValue` can be written from code: the file is saved and appliers run.
- `SaveEvents` in `CatLib.Game.Events` (`SaveFileSelected`, `GameSavingStarted`, `SuccessfullySaved`, `UnsuccessfullySaved`)
  and the current save in `GameInfo` (`SaveDirectory`, `SaveFileName`, `SaveFilePath`, `IsNewSave`, `IsLoadingSave`).
- Documentation: [Writing a mod](docs/WritingMods.md), [Localization](docs/Localization.md), session role in [Multiplayer compatibility](docs/Network.md).
- Developer tools in `CatLib.Tests`: entity dump on F4 with the object tree, collider sizes and renderer state; notification preview on F5.

### Changed

- The context line of the Mods tab holds three lines, so long descriptions keep their default value line.

## 0.4.0

Multiplayer compatibility: a handshake over its own Steam channel checks that players have compatible mods,
session settings take the host's values on clients, incompatible players are warned or disconnected with a clear message in the lobby and in a level.
Fixed a crash when leaving a level after opening the Mods tab from the pause menu.

## 0.3.0

The Mods tab in the game's settings menu, in the main menu and in the pause menu: a list of mods, a card per mod, rows for every setting
with toggles, sliders, dropdowns and text fields, player messages in English and Russian.

## 0.2.0

Live settings: settings declared in code, applied at once and whenever the file changes, value checks, settings that need a restart.

## 0.1.0

Game event bridge, main thread dispatcher, frame loop, logging and the in-game test runner.
