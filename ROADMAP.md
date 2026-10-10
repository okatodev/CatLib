# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## 0.7.1

Everything under "Not released yet" in the [changelog](CHANGELOG.md): the crash watcher with names of methods, the game without the Steam network,
the first part of the asset API and Custom Stamps 0.1.0. Before it goes out: the checks in [Deferred tests](tests/DeferredTests.md).

## After 0.7.1

The next steps go in small steps like the way to 0.7.0, in other directions: assets, stability,
the mods that are out already and a first try at talking to each other in the game.

### Asset API

CatLib reads PNG and JPG images, pictures of the game and content packs of players, see [Assets](docs/Assets.md). Models, sounds and other files are next.
Assets come up in many mods, so the API is for every mod author, not only for the mods below.

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
- Custom Stamps: stamps that open with the progress of the game, like some of the game's own, as a setting of the pack.
- Content packs: sounds and models next to images.
- More mods on the same API later.

### Stability

- Exact crash checks: every crash and freeze while quitting with a known cause, a test that reproduces it where possible,
  and a line in the report that names the cause.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

### The mods that are out

- More content for Boat Tweaks, Shelf Labels, Better Repair, Parcel Board, Stack it!, Too Late and Custom Stamps, each released as a minor version of its own.
- Every README with the Discord server, screenshots and short GIFs of what the mod does, on GitHub and on Thunderstore.

### Chat between players

The game has neither a voice chat nor a text chat.

- A new mod that tries this direction, with voice chat as the goal.
- Voice needs sound in CatLib: capturing the microphone, encoding, sending over the CatLib channel and playing back
  at the player's position. Either an audio API in CatLib or a separate library that mods depend on; to decide when it starts.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
