# Roadmap

What is planned, in rough order. Finished work is removed from here and described in [CHANGELOG.md](CHANGELOG.md) or in the mod's CHANGELOG.md.

## After 0.7.2

The next steps go in small steps like the way to 0.7.0, in other directions: assets, stability,
the mods that are out already and a first try at talking to each other in the game.

### Asset API

CatLib reads PNG and JPG images, pictures of the game and content packs of players, see [Assets](docs/Assets.md). Models, sounds and other files are next.
Assets come up in many mods, so the API is for every mod author, not only for the mods below.

- Portable Magnifying Glass, harder than Too Late: the mod works with 3D models.
- Content packs: sounds and models next to images.
- More mods on the same API later.

### Stability

- Exact crash checks: every crash and freeze while quitting with a known cause, a test that reproduces it where possible,
  and a line in the report that names the cause.
- Crashes in .NET code named in the report. The crash when Steam shut down soon after a player left (fixed in 0.7.1) showed only
  "JIT-compiled .NET code" in the report and was found by hand: the dump had the text `"Before Steam shuts down: "` on the stack.
  - The crash watcher reads the .NET strings on the stack of the crashing thread and writes the first few into the report
    and the window, with a translated label in all 13 languages, and a developer menu command that crashes inside .NET code to check it.
  - CatLib writes what it is doing into the session file before risky calls into Steam and the game, like `Net.NativeCall` does now,
    so the report names the operation even without a dump.
  - A game closed through its console window keeps its report but does not take one of the three places for memory dumps.
- The last BepInEx log lines before a native crash on the game thread may not reach the log file.

### The mods that are out

- Parcel Board 0.2.0:
  - New icons for the cursed parcels (`BehaviorConstraint.Corrupted`, "Broken" in the mod's texts),
    the icon of their list and the mod's own `Icons/corrupted.png` used when the game's icon is not loaded. The other icons stay as they are.
  - More fields, the list comes later. Each of them can be turned off on its own, apart from the main table.
- Too Late 0.2.0: what was planned for joining a game in progress done in full, see "Not covered yet" in its README,
  with tests for all of it.
- Stack it! 0.2.0: the backtrack fixed, and the tests that were not done yet.
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
