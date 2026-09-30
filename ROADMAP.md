# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's README.

## Mods

- Parcel list (`ClientOnly`): a list of all parcels in the scene with their types and counts.
- Stacking (`RequiredOnAll`): better compatibility of parcel sizes when stacking, which parcels can stand on which.
  Needs patches of the game's placement code and a safe patching wrapper in CatLib
  (the method must exist, errors are isolated and logged).
- Boat Tweaks: improvements and bug fixes.
- Late join: joining a game in progress. Builds on patches, mod messages and entity synchronization.

## Mod compatibility in the lobby

Next.

- Every player sees in the lobby, before the level starts, which mods each player has, themselves included:
  a short list with name, version and a mark (fine, missing, other version, only on this player).
  Today only the host sees "other mods" on a player's card, and a client only knows its own verdict.
  Needs the host to send the list of every player's mods to everyone (a new message, protocol 5).
  Players without CatLib are listed as such.
- Clear advice for every problem: which mod to install or remove and in which version, who has to do it,
  what a different game version means.
- Part of CatLib or a separate client-side mod: to decide. The data comes from the CatLib handshake, and the lobby
  list and advice are only useful if every player sees them, which speaks for CatLib with a setting to hide the list.
- Revisit `OnIncompatiblePlayer = Warn`. A player who stays after a failed check gets mod messages for the mods
  both sides share, but no session settings at all, so even shared mods run with the player's own values.
  Options: send session settings of shared mods to such players too; make `Disconnect` the default;
  let the host decide per player in the lobby (keep or remove).

## Mods tab

- Polish of the small details.
- Mod logos next to the mod names.

## Localization

- More languages than English and Russian.
- Conveniences in the localization API where mods need them.

## Crash reports

- Memory dumps: the crash watcher writes a minidump itself at the moment of the crash, also without Windows Error Reporting settings.
  Needs the watcher to attach to the game as a debugger. Medium.
- Readable reports: at start CatLib writes a map of game method addresses from IL2CPP, so offsets in `GameAssembly.dll`
  become method names; `UnityPlayer.dll` has public symbols. The report then names the top frames of every thread. Medium.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

A handler inside the crashing process itself is not planned: .NET, Unity and the game compete for that place,
and code running in a process with damaged memory is not reliable.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
