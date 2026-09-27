# Crash reports

When the game closes unexpectedly, CatLib shows a small window with what happened and keeps a report with the logs.
This works for every kind of crash, native ones included, because the window comes from a separate program
that waits for the game to close instead of code inside the crashing game.

## What the player sees

A small window titled "Cat Mail Co: crash report". The heading is a random cozy phrase, different from crash to crash,
in Russian when the game is in Russian and in English otherwise. Below it:

- what happened in one line: the reason from the exit code, the module and offset of the crash if Windows recorded one,
  how long the game ran, and whether it happened while the game was quitting;
- **Details**: time, exit code, module, the .NET exception, game and CatLib versions, the mods, the last game events;
- **Open the report folder** and **Copy the report**; the window stays open after both;
- the path of the report folder at the bottom.

A normal exit shows nothing. Closing the game from the Task Manager shows the window with "closed from outside".

## The report folder

`BepInEx/CatLib/Crashes/<date>_<time>_<process id>/`:

| File | Contents |
|---|---|
| `report.txt` | The full report: summary, exit code, crash record from the Windows event log, .NET message, versions, mods, the last 15 game events, the last 40 lines of the BepInEx log, the memory dump path if Windows wrote one |
| `LogOutput.log` | The BepInEx log of the crashed session |
| `Player.log` | The Unity log of the crashed session, which the game replaces on the next start |
| `session.txt` | What CatLib recorded while the game ran |

The last 10 reports are kept. `BepInEx/CatLib/Crashes/watcher.log` has the watcher's own messages if something went wrong with it.

## How it works

1. At start CatLib writes `session_<process id>.txt` in `BepInEx/CatLib/Crashes` with the versions and log paths,
   and starts `CatLib.CrashWatcher.exe` from its own folder.
2. While the game runs, CatLib adds the language, the mod list and the game events to that file
   (network ticks are left out, the file keeps the last 200 events).
3. On a normal quit CatLib marks the file. The watcher waits for the game process to end and reads its exit code.
4. Exit code 0: the watcher removes the file and closes. Anything else: it reads the crash records of this process
   from the Windows event log (Application Error 1000 and .NET Runtime 1026), writes the report folder and shows the window.

The watcher does not attach to the game as a debugger and does not change anything in it.
It is a .NET Framework 4.8 program, so it runs on every Windows 10 and 11 without installing anything.

## Settings

`CrashWindow` in section `[Diagnostics]` of CatLib's config turns the watcher off. It takes effect on the next start.

## Checking it

The developer menu of `CatLib.Tests` has the group **Crash**, and every command needs a second press within 3 s:

| Command | What happens |
|---|---|
| Native crash, game thread | Unity's own forced crash, an access violation on the game thread |
| Native crash, worker thread | An access violation on a new native thread, like the Steam networking crash found in 0.5.0 |
| Managed crash | An unhandled .NET exception on a new thread |

To look at the window without crashing, run
`CatLib.CrashWatcher.exe --preview --session <any file>` from `BepInEx/plugins/CatLib`.

## Memory dumps

The watcher does not write memory dumps itself. When Windows Error Reporting is set up to keep dumps
(`LocalDumps` in the registry), the report names the dump of the crashed process from `%LOCALAPPDATA%\CrashDumps`.
