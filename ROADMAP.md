# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## After 0.7.5

The next steps go on in small steps: assets, the mods that are out already, and more ways for players to talk to each other.

### Asset API

CatLib reads PNG and JPG images, pictures of the game and content packs of players, see [Assets](docs/Assets.md). Models, sounds and other files are next.
Assets come up in many mods, so the API is for every mod author, not only for the mods below.

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
- Content packs: sounds and models next to images.
- More mods on the same API later.

### The mods that are out

- Too Late: players 5 to 8 who do not have the mod start at the spawn points of players 1 to 4, their own game picks the spot.
- Every README with the Discord server, screenshots and short GIFs of what the mod does, on GitHub and on Thunderstore.

### Chat between players

Purr! and the voice API of CatLib are the first step, see [Voice](docs/Voice.md).

- Purr!: a volume and mute per player in the Mods tab, push to talk on a gamepad.
- A text chat.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
