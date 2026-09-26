# Roadmap

What is planned, in rough order. Finished work moves to [CHANGELOG.md](CHANGELOG.md).

## Now: extra shelf labels

A mod that adds 1 to 3 extra labels next to or below each shelf label, so one shelf can say both "Port Windy" and "Fragile".

1. Done: save events in the bridge and `GameInfo` save properties, observed in single player. Client side comes with test 7.
2. Done: `CatLib.Saves`, checked with a real save: writing, reading after a restart, recovery from a damaged file, a new save.
3. Done: messages between mods, tested outside the game. Next: F7 in single player and with a friend.
4. The mod in `mods/ShelfLabels` works in single player: extra labels, clicks, placement and stands per shelf, saves. Next: test 10.5 with a friend.
   The plan it follows: copies of the game's label without its network and save identifiers, the game's own highlight and clicks,
   side and count per setting, pictures stored per original label and copy number so hiding copies never loses them,
   the original label never touched.

## Before tagging 0.5.0

- Test 7 of the session journal: Boat Tweaks with two players.

## Developer menu

One key opens an in-game panel (IMGUI) with the developer commands instead of a function key per tool:
entity dump, label experiments, deck saving and whatever mods register. Commands keep writing their results to files.
UnityExplorer stays the tool for live inspection; the menu is for repeatable dumps and experiments.

## Later mods

- Workshop: the amount of paper and cardboard used to repair parcels. Needs research of what the repair consumes.
- Stacking rules: which parcels can stand on which. Needs patches of the game's placement code
  and a safe patching wrapper in CatLib (the method must exist, errors are isolated and logged).
- Late join: joining a game in progress. Builds on patches, mod messages and entity synchronization.
- A client-only mod, still to be chosen.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
