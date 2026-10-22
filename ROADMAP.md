# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## After 0.7.3

The next steps go in small steps like the way to 0.7.0, in other directions: assets,
the mods that are out already and a first try at talking to each other in the game.

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

The game has neither a voice chat nor a text chat.

- A new mod that tries this direction, with voice chat as the goal.
- Voice needs sound in CatLib: capturing the microphone, encoding, sending over the CatLib channel and playing back
  at the player's position. Either an audio API in CatLib or a separate library that mods depend on; to decide when it starts.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
