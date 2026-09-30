# Changelog

CatLib follows semantic versioning. Mods in `mods/` have their own versions and changes in their README files.

## 0.6.0

Not released yet.

### Added

- Crash reports: when the game closes unexpectedly, a small window names what happened with a different cozy phrase every time,
  and a report folder keeps the report, both logs and the recorded session in `BepInEx/CatLib/Crashes`.
  Works for native crashes too, since the window comes from `CatLib.CrashWatcher.exe`, a separate program next to `CatLib.dll`.
  Can be turned off with `CrashWindow` in section `[Diagnostics]`. See [Crash reports](docs/CrashReports.md).
- Developer menu of `CatLib.Tests`: the Crash group with a native crash on the game thread, a native crash on a worker thread
  and a managed crash.

- Session roster: the host keeps every player's versions, mods and status and sends it to everyone
  (`CatNetwork.Roster`, `CatNetwork.RosterChanged`). Protocol 5.
- Paused mods: a `RequiredOnAll` mod runs only while every player has it in a compatible version, otherwise it is paused for everyone
  and the game runs as without it (`CatNetwork.IsActive`, `CatNetwork.ActiveModsChanged`). Boat Tweaks, Shelf Labels and Better Repair pause.
- Players' mods in the lobby: a folding list at the top of the lobby for every player, the host included, with each player's mods,
  versions and what to do about differences. The host switches what happens to players with other mods right there.
- Mod icons in the Mods tab: `icon.png` next to the mod's DLL or in its Thunderstore package folder is shown left of the mod's name
  and at the top of its settings (`CatSettings.IconPath`), a question mark stands in for a missing one. Every plugin project copies its `icon.png` next to its DLL and into the game;
  without one the build warns (CATLIB001) and still succeeds. The log names the icon of every mod or where it was looked for.
- The Mods tab: the author under the mod's version (`CatSettings.Author`, from the Thunderstore package folder or the assembly company),
  a short mark under a mod's name when it is paused in this game, waits for a restart or has settings set by the host,
  CatLib first in the list, and keyboard and gamepad navigation between the list, the settings and the reset button.
  A text setting starts typing on submit instead of on selection, so a gamepad moves past it.
  The mod list and the settings scroll to follow the selection.
- `FoldoutList` in `CatLib.UI`: a folding list built from the game's UI pieces, for mods too. See [Folding lists](docs/Foldout.md).
- `CatNetwork.IncompatiblePlayers` reads and changes what the host does with incompatible players; a change applies to players already in the session.
- Developer menu of `CatLib.Tests`: Session roster and Lobby dump.
- Localization: texts in `Lang/*.json` are embedded by the build and loaded by `CatSettings.For` without code.
  Plural forms by the Unicode rules of each language (`Plural`, `PluralRules`), `LocalText` handles,
  `CatLanguage.Changed`, `CatLanguage.GameLanguages` and the game's own texts with `CatLanguage.Game(term)`.
  A translation that cannot be filled in shows English instead of failing.
- Translation files of players in `BepInEx/config/CatLib/Translations/<mod id>/<language>.json` win over built-in texts.
  The developer menu writes a translation report, exports texts for translators and reloads the files.
  `TranslationCheck` finds missing, unknown and broken texts; the log lists them at the main menu. See [Localization](docs/Localization.md).
- Memory dumps: the crash watcher follows the game like a debugger and writes `crash.dmp` into the report folder
  at the moment of the crash, without Windows Error Reporting settings. The module, offset and thread of the crash come
  from the watcher itself, the report says whether it was the game thread and what memory access failed.
  Crashes that Unity closes itself on the game thread are caught the moment the exception is raised.
  The text of an unhandled .NET exception comes from CatLib when Windows logs nothing. Setting `CrashDumps`, on by default.
- The crash window in every game language, with texts from CatLib's translations; `report.txt` is always in English for the mod authors.
- CatLib, Better Repair, Boat Tweaks and Shelf Labels in every language of the game: English, French, Italian, German, Spanish,
  Brazilian Portuguese, Polish, Simplified and Traditional Chinese, Japanese, Korean, Ukrainian and Russian.

### Changed

- The scrollbars of the Mods tab are copies of the game's own.
- A player that stays after a failed check gets the session settings of the mods it shares with the host.
- Protocol 5: players with CatLib 0.5.0 are told that their CatLib differs.
- CatLib's own texts live in `src/CatLib/Lang/*.json` (catalog `catlib.core`, keys `ui.*`), so they can be translated and fixed by files like any mod's.
- Notifications wrap Chinese and Japanese between characters, keep Korean words whole and split at a full-width colon.
- Mods no longer load their texts themselves and have no `EmbeddedResource` line for `Lang`.
- Mods in `mods/` follow one folder layout (`Settings`, `Logic`, `Scene`, `Sync`, `Data`, `Patches`),
  with namespaces that follow the folders. See [Writing a mod](docs/WritingMods.md#folders).

## 0.5.0

For mod authors: localization, mod data in game saves, messages between mods, a developer menu,
safe access to two-dimensional IL2CPP arrays, player notifications and the session role.
Checked with two game copies on one computer, including Boat Tweaks and Shelf Labels in multiplayer.

### Added

- `CatLib.Localization`: a translation catalog per mod (`CatLocalization.For`, `CatSettings.Texts`) loaded from JSON files or embedded resources,
  lookup with fallback from a regional language to its base language and to English, `CatLanguage.Current` following the game language.
- The Mods tab takes the mod name, section names, setting labels, descriptions and dropdown values from the mod's catalog,
  and rebuilds its texts when the game language changes.
- `CatLib.Saves`: mod data per game save in `CatLibSaves` next to the game's save folder (`CatSaves.For`, `ModSave`).
  Written only after the game saved successfully and only by the host, atomically with a backup;
  damaged files are set aside, newer data is never overwritten, a new save never inherits data left under its name.
  See [Mod data in game saves](docs/Saves.md).
- Messages between mods: `CatNetwork.Channel` with `SendToHost`, `Broadcast`, `SendTo`, `PeerJoined` and `PeerLeft`.
  Only between the host and players that share the mod, with local delivery in single player and on the host,
  and a limit of 60 messages per second per player on the host. See [Mod messages](docs/Network.md#mod-messages).
- Developer menu in `CatLib.DevTools`: one key opens a keyboard driven panel with commands mods register with
  `DevMenu.Command` and `DevMenu.Toggle`. See [Developer menu](docs/DevTools.md).
- `Il2CppArrays` in `CatLib.Il2Cpp`: reads and writes two-dimensional IL2CPP arrays that interop only exposes as `Il2CppObjectBase`.
  Every call checks the array rank, the element size against the requested type and the bounds.
- `Notifications.Show` in `CatLib.UI` shows a mod's message as a game notification in a level and in the Mods tab status line.
- `CatNetwork.Role` (`Offline`, `Host`, `Client`) and `CatNetwork.IsAuthority` for mods whose decisions must match for every player.
- `Setting<T>.LocalValue` can be written from code: the file is saved and appliers run.
- `SaveEvents` in `CatLib.Game.Events` (`SaveFileSelected`, `GameSavingStarted`, `SuccessfullySaved`, `UnsuccessfullySaved`)
  and the current save in `GameInfo` (`SaveDirectory`, `SaveFileName`, `SaveFilePath`, `IsNewSave`, `IsLoadingSave`).
- The build copies plugins to a second game copy too when `SecondGameDir` is set in `GamePaths.props`.
- Developer tools in `CatLib.Tests`, all in the developer menu: entity dump, settings menu dump, notification preview,
  mod message probe, Steam self check, the list of visible Steam players and joining the first host.
- Documentation: [Writing a mod](docs/WritingMods.md), [Localization](docs/Localization.md), session role in [Multiplayer compatibility](docs/Network.md).

### Changed

- Network protocol 4: the verdict lists the mods both sides share. Players with an older CatLib are reported as incompatible.
- Notifications are wrapped by the width the game's notification font measures, not by a character count,
  so a line never runs past the screen edge when the notification settles. Lines are not broken inside quotes.
- When a client leaves through a game restart, the host's session settings stay until the main menu is loaded,
  so mods do not react to setting changes while the level is being destroyed.
- The context line of the Mods tab holds three lines, so long descriptions keep their default value line.

### Fixed

- A crash when leaving a level in multiplayer: the Steam channel sessions of CatLib stayed open while the game shut its networking down.
  They are now closed when a player leaves, when the session stops and after the Steam self check.
- The scrollbars of the Mods tab stretch along their pane like the game's own, instead of sitting as a small square in the corner.

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
