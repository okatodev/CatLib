# Boat Tweaks

Choose what stands on the deck of the parcel boat and how high you can stack.

The boat that takes your parcels comes with crates, baskets and other clutter already on board.
Boat Tweaks lets you clear it away, keep only the game's decks you like, build your own, or get a new random one every time.
The parcels that arrive on the boat are never changed.

## What it does

Pick a mode in the settings:

- **As in the game.** Nothing changes.
- **Empty.** A clean deck every time.
- **Game layouts.** Only the game's decks you allow, by number, and never the same one twice in a row if you like.
- **One game layout.** Always the same game deck.
- **Own layouts.** Decks you made yourself, at random, one after another, or always the same one.
- **Generated.** A new random deck each boat. Set how full it is and whether the clutter stays near the rails.
- **Mixed.** Each boat rolls one of the above, with chances you set.

Besides the deck, you can raise or lower the **height limits**: the red line where a stack stops counting as approved,
and the maximum height of a stack.

## Making your own deck

1. Switch the mode to **Empty**.
2. Put parcels on the boat where the crates should be.
3. Press **Ctrl+B**.

The deck is saved to `BepInEx/config/BoatTweaks/layouts` and shows up in **Own layouts**.
The parcels the boat brought with it are left out, so take off anything else you don't want in the layout before saving.
Each file is a simple grid you can edit by hand: `#` is a crate, `.` is free.

Own and generated decks get real crates, bottles and lamps from the game's boats.
If you'd rather see bare spots, turn off **Decorate blockers**.

## Settings

Settings → Mods → Boat Tweaks. Each part of the settings only works in its own mode, so they never get in each other's way.

## Playing with friends

Everyone in the game needs Boat Tweaks, and the host's settings are the ones that count.
If someone joins without it, boats come as in the game until that player leaves.

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues). If the game crashed, attach the folder from the CatLib crash window,
otherwise `BepInEx/LogOutput.log` before you start the game again.
