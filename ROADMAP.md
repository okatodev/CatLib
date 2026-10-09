# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## Towards 0.8.0

The way to 0.8.0 goes in small steps like the way to 0.7.0, in other directions: assets, stability,
the mods that are out already and a first try at talking to each other in the game.

### Asset API

CatLib has no way to load assets yet: models, textures, sounds and other files from a mod's folder.
Assets come up in many mods, so the API is for every mod author, not only for the mods below.

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
- Custom stamps: an API for packs of stamp images that players put on parcels, and a mod that loads such packs.
  A pack is a folder of images with a small description; the images are added to the game's stamps.
  Stamps stay on parcels in saves and are seen by every player, so the API decides what others see when they do not have the pack.
- More mods on the same API later.

### Stability

- Exact crash checks: every crash and freeze while quitting with a known cause, a test that reproduces it where possible,
  and a line in the report that names the cause.
- The host could crash in `steamclient64.dll` when it quit right after a player left, inside the Steam thread that keeps
  peer to peer connections. CatLib waits for its closed sessions; whether the game itself crashes the same way is still to check
  without mods.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.
- More names in crash stacks: frames of `UnityPlayer.dll` from the public symbols on Unity's symbol server,
  and generic methods of IL2CPP from `global-metadata.dat`, which the method map of BepInEx does not list.

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
