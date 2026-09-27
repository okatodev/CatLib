# Roadmap

What is planned, in rough order. Finished work moves to [CHANGELOG.md](CHANGELOG.md).

## Now: next mod

0.5.0 is out with Boat Tweaks 0.2.0 and Shelf Labels 0.1.0, checked in multiplayer with two game copies on one computer.
Next is the Workshop mod below; it waits for research of what a repair consumes.
Open question from the tests: whether the boat leaves with a stack above the game's approved height when Boat Tweaks raises it.

## Later mods

- Workshop: the amount of paper and cardboard used to repair parcels. Needs research of what the repair consumes.
- Stacking rules: which parcels can stand on which. Needs patches of the game's placement code
  and a safe patching wrapper in CatLib (the method must exist, errors are isolated and logged).
- Late join: joining a game in progress. Builds on patches, mod messages and entity synchronization.
- A client-only mod, still to be chosen.

## Deferred

- macOS support.
- Reloading mod code without restarting the game. (Questioned.)
