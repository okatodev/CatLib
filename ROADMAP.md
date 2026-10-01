# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## Mods

- Parcel list (`ClientOnly`): a list of all parcels in the scene with their types and counts.
- Stacking (`RequiredOnAll`): better compatibility of parcel sizes when stacking, which parcels can stand on which.
  Needs patches of the game's placement code and a safe patching wrapper in CatLib
  (the method must exist, errors are isolated and logged).
- Boat Tweaks: improvements and bug fixes.
- Late join: joining a game in progress. Builds on patches, mod messages and entity synchronization.

## Crash reports

- Readable reports: at start CatLib writes a map of game method addresses from IL2CPP, so offsets in `GameAssembly.dll`
  become method names; `UnityPlayer.dll` has public symbols. The report then names the top frames of every thread. Medium.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

A handler inside the crashing process itself is not planned: .NET, Unity and the game compete for that place,
and code running in a process with damaged memory is not reliable.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
