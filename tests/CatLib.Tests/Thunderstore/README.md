# CatLib Tests

Tests and developer tools for CatLib and its mods.

**If you just play, you don't need this.** It runs its tests every time the main menu loads, briefly opening the settings on its own,
and its demo mod takes part in the lobby check, so friends without it would show up as missing a mod.

## What's inside

- **Tests.** About 160 checks that run inside the real game: settings, the Mods tab, translations, saves, multiplayer and every mod.
  They run when the main menu first loads and again from the developer menu. Tests of a mod that is not installed are skipped.
  Results go to `BepInEx/CatLib.Tests/Reports`, a timeline of game events to `BepInEx/CatLib.Tests/Timelines`.
- **Developer menu** on the `` ` `` key, the one left of 1:
  - Tests: run them all, turn the automatic run on or off.
  - Game, for the host in a level: serve the waiting customers, the next part of the day, skip to the day's results.
  - Inspect: dump what the camera is looking at, dump the settings menu.
  - Network: send a test message, list the players Steam sees, join the first open game, check the Steam channel.
  - UI: preview a notification, the version badge in the main menu, a Mods tab full of mods.
  - Crash: crash or hang the game on purpose to check the crash window. Each needs a second press.
  - A group for each mod with its own tools, for example the falls of Stack it! or the joins of Too Late.
- **Demo settings.** A Demo section on the Mods tab to try live settings, plus a demo network mod
  whose version you can change on one side to see a mismatch in the lobby.

## Settings

`BepInEx/config/catlib.tests.cfg`, or the Mods tab. `OnMainMenu` in `[Run]` turns the automatic run off.

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues) with the test report from `BepInEx/CatLib.Tests/Reports` and `BepInEx/LogOutput.log`.
