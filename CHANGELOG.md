# Changelog

CatLib follows semantic versioning. The mods built on it, Boat Tweaks, Shelf Labels, Better Repair, Parcel Board and Stack it!, have their own versions and changelogs.

## Not released yet

### Added

- `CatParcels` in `CatLib.Game`: every parcel of the level as a `ParcelInfo` with its destination, marks, size, footprint on a shelf, weight, damage,
  missing stamps and place, the game's stamp icons and region names. See [HUD and parcels](docs/Hud.md).
- `HudLayer` in `CatLib.UI`: a layer on the game's HUD during a level that steps aside for the game's menus, and texts in the game's font.
- `CountTable` and `CountList` in `CatLib.UI`: a table and a list of icons and counts on a rounded, almost clear plate,
  with light outlined texts like the game's hints, rows with an icon and a faint hint, rows wrapped into columns and tables of the same height side by side; `UiSprites.RoundedPlate`, `UiSprites.FromPng` and `UiSprites.FromResource`.
- New mod: Parcel Board 0.1.0, counts of the parcels in the level by destination, per mark and size, only for the player who has it.
- `CatPatches` in `CatLib.Patching`: Harmony patches of a mod installed whole or not at all, each method and handler checked first;
  errors in handlers are written a few times, counted, and turn the patches off after 50. See [Patching the game](docs/Patching.md).
- `StoreGrid` and `GridView` in `CatLib.Game`: the grids of storages and parcels, free cells, roots of stacks and the cells a parcel takes,
  computed like the game does. See [Storages](docs/Storages.md), with how the game stacks parcels and checks their marks.
- `Il2CppArrays.TryGet` reads one cell of a two-dimensional IL2CPP array.
- New mod: Stack it! 0.1.0, parcels that stand across the joint of level parcels and fall when one is taken away. Every player needs it.
- The mod card on the Mods tab shows the mod's description under the author, across the whole card: `mod.description` from the catalog,
  else the description of the Thunderstore manifest. `CatSettings.Description` sets it from code. Every mod here has it in all 13 languages.

### Changed

- The mod list on the Mods tab has two lines per mod: the name, and under it the version and the number of settings,
  or a mark such as "needs a restart" in its place. Icons are bigger, their empty edges are cut off and dense icons are drawn
  a little smaller, so every icon looks about the same size.
- Tests started in a level open the game's pause menu and its settings to reach the Mods tab, and stop waiting for the tab
  when it did not appear earlier in the run, instead of waiting for it in every test.

### Fixed

- When the game cannot decode PNG images, CatLib says so once and decodes every icon itself, instead of trying the game first for each image.

## 0.6.2

The first release on Thunderstore, together with CatLib Crash Watcher 1.0.0, Boat Tweaks 0.3.0, Shelf Labels 0.2.0 and Better Repair 0.1.0.

### Changed

- The crash watcher is its own Thunderstore package, `CatLib-CrashWatcher`, with its own version and changelog, so a release of CatLib
  does not change the program and its review stays small. CatLib depends on it, so a mod manager installs both.
- CatLib finds `CatLib.CrashWatcher.exe` next to `CatLib.dll` or in any folder of `BepInEx/plugins`, and the log names its version and path.
- The session file starts with a format number. A watcher that reads an older format says in the report and in `watcher.log` that it should be updated,
  and the report names the version of the watcher.

## 0.6.1

Released together with Boat Tweaks 0.3.0, Shelf Labels 0.2.0 and Better Repair 0.1.0.
For players: works with the game update of October 2026, the game no longer hangs when quitting after single player,
the main menu shows the CatLib version and warns when the game and CatLib do not match, crash reports catch a hang while quitting.
For mod authors: Thunderstore packages for CatLib and every mod, `GameCompatibility`.

### Added

- The main menu shows the CatLib version in the bottom right corner. CatLib knows the game build it is made for
  (October 2026, Steam build 25651540) and compares it with the running game by the build date in the game version.
  When the game is newer, the corner asks to look for a CatLib update; when it is older, to update the game.
  Pointing at the corner shows both build dates, or the number of mods when everything matches.
  Mods can read the same check from `GameCompatibility`. The log and crash reports name the result.
- Crash reports catch a hang while quitting: when the game still runs 8 s after it began to quit, the crash watcher keeps a dump
  of every thread, and the report and the window say the game stopped responding while quitting. See [Crash reports](docs/CrashReports.md).
- Text settings on the Mods tab title the virtual keyboard of Steam Deck and Big Picture with the setting's name.
- Thunderstore packages: `dotnet build -p:CatLibThunderstore=true` packs CatLib, `CatLib.Tests` and every mod with `Thunderstore/manifest.json`
  into `Thunderstore-build/`, a folder and a zip per package with the manifest, icon, README, changelog and plugins.
  The build checks the manifest and the icon by Thunderstore's rules (CATLIB002, CATLIB003, CATLIB004),
  points relative links of the README and the changelog to the repository on GitHub, and warns when the changelog does not start with the package version.
  See [Thunderstore package](docs/WritingMods.md#thunderstore-package).
- Mods keep their changes in `CHANGELOG.md` next to `README.md`, and a README for players in `Thunderstore/README.md`.
- `CatLib.Tests` is its own package that depends on CatLib and the three mods.
- Developer menu of `CatLib.Tests`: Crash → Hang on quit, UI → Version badge preview.

### Changed

- Works with the game update of October 2026 (Unity 6000.3.23, single player without internet).

### Fixed

- The game could hang while quitting after a single-player game: since the October update the game's own UDP client of single player
  keeps waiting for a packet on a worker thread after the level is left, and the game waits for that thread forever when it quits.
  CatLib closes the socket of a game network client when the game replaces it and when the game quits, so the thread ends.

## 0.6.0

For players: the players' mods in the lobby with paused mods instead of disconnects, mod icons and authors on the Mods tab,
gamepad navigation, every text in all 13 languages of the game, memory dumps and a translated crash window.
For mod authors: the session roster, `FoldoutList`, texts loaded from `Lang/*.json` without code, plural forms,
translation files and checks. New mod: Better Repair. Checked with two game copies on one computer.

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
