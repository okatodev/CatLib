# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## Mods

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
  Needs an asset API in CatLib, which has none yet: loading models, textures and other assets from a mod's folder.
  Assets come up in many mods, so the API is for every mod author, not only for this one.
- Custom stamps: an API for packs of stamp images that players put on parcels, and a mod that loads such packs.
  A pack is a folder of images with a small description; the images are added to the game's stamps.
  Builds on the asset API. Stamps stay on parcels in saves and are seen by every player,
  so the API decides what others see when they do not have the pack.

## Crash reports

- Readable reports: at start CatLib writes a map of game method addresses from IL2CPP, so offsets in `GameAssembly.dll`
  become method names; `UnityPlayer.dll` has public symbols. The report then names the top frames of every thread. Medium.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

A handler inside the crashing process itself is not planned: .NET, Unity and the game compete for that place,
and code running in a process with damaged memory is not reliable.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
