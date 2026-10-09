# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## Towards 0.8.0

The way to 0.8.0 goes in small steps like the way to 0.7.0, in other directions: assets, documentation, stability,
the mods that are out already and a first try at talking to each other in the game.

### Asset API

CatLib has no way to load assets yet: models, textures, sounds and other files from a mod's folder.
Assets come up in many mods, so the API is for every mod author, not only for the mods below.

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
- Custom stamps: an API for packs of stamp images that players put on parcels, and a mod that loads such packs.
  A pack is a folder of images with a small description; the images are added to the game's stamps.
  Stamps stay on parcels in saves and are seen by every player, so the API decides what others see when they do not have the pack.
- More mods on the same API later.

### Documentation

- Fill the gaps: every public API of CatLib described with an example, in the same style as the existing pages.
- One page that leads a new mod author from an empty folder to a published Thunderstore package.
- Check the pages against the code and remove what is out of date.

### Stability

- Exact crash checks: every crash and freeze while quitting with a known cause, a test that reproduces it where possible,
  and a line in the report that names the cause.
- The host could crash in `steamclient64.dll` when it quit right after a player left, inside the Steam thread that keeps
  peer to peer connections. CatLib waits for its closed sessions; whether the game itself crashes the same way is still to check
  without mods.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

#### Crash watcher: method names in reports

Reports name a crash as `GameAssembly.dll + 0x42e586`, which says nothing without a disassembler.
At start CatLib writes a map of the game's method addresses from IL2CPP (every method of `GameAssembly.dll` with its address),
and the watcher turns offsets in `GameAssembly.dll` into method names with it; `UnityPlayer.dll` has public symbols from Unity.
The report then names the top frames of every thread, for example `EntityInteractableStore.StoreEntityLocal + 0x3a`.

A handler inside the crashing process itself is not planned: .NET, Unity and the game compete for that place,
and code running in a process with damaged memory is not reliable.

### The mods that are out

- More content for Boat Tweaks, Shelf Labels, Better Repair, Parcel Board, Stack it! and Too Late, each released as a minor version of its own.
- Every README with the Discord server, screenshots and short GIFs of what the mod does, on GitHub and on Thunderstore.

### Chat between players

The game has neither a voice chat nor a text chat.

- A new mod that tries this direction, with voice chat as the goal.
- Voice needs sound in CatLib: capturing the microphone, encoding, sending over the CatLib channel and playing back
  at the player's position. Either an audio API in CatLib or a separate library that mods depend on; to decide when it starts.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
