# Roadmap

What is planned, in rough order. Finished work moves to [CHANGELOG.md](CHANGELOG.md).

## Now: next mod

0.5.0 is out with Boat Tweaks 0.2.0 and Shelf Labels 0.1.0, checked in multiplayer with two game copies on one computer.
0.6.0 is in progress: crash reports are done and checked in the game.
Seen in its checks: the last BepInEx log lines before a native crash on the game thread may not reach the log file.
Next is the Workshop mod below; it waits for research of what a repair consumes.
Open question from the tests: whether the boat leaves with a stack above the game's approved height when Boat Tweaks raises it.

## CatLib crash handler

Native crashes left almost nothing: BepInEx runs .NET inside the game, and .NET takes unhandled crashes before
the game's own crash handler, so Unity writes no report and `Player.log` just stops.

1. Done in 0.6.0: a watcher process. `CatLib.CrashWatcher.exe` waits for the game to close; on a crash it collects the logs,
   the session record and the crash records from the Windows event log into a report and shows a small window.
   See [Crash reports](docs/CrashReports.md).
2. Memory dumps. The watcher writes a minidump itself at the moment of the crash, also without Windows Error Reporting settings.
   Needs the watcher to attach to the game as a debugger. Medium.
3. Readable reports. At start CatLib writes a map of game method addresses from IL2CPP, so offsets in `GameAssembly.dll`
   become method names; `UnityPlayer.dll` has public symbols. The report then names the top frames of every thread. Medium.

A handler inside the crashing process itself is not planned: .NET, Unity and the game compete for that place,
and code running in a process with damaged memory is not reliable.

## Later mods

- Workshop: the amount of paper and cardboard used to repair parcels. Needs research of what the repair consumes.
- Stacking rules: which parcels can stand on which. Needs patches of the game's placement code
  and a safe patching wrapper in CatLib (the method must exist, errors are isolated and logged).
- Late join: joining a game in progress. Builds on patches, mod messages and entity synchronization.
- A client-only mod, still to be chosen.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
