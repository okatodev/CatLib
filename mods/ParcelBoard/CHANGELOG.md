# Changelog

## Unreleased

- The list of cursed parcels has the curse's own look: the glowing purple swirl the game shows on them at night, drawn by CatLib from the game's picture.
  The board's own icon for it, used before a level is loaded, is the same swirl instead of a broken box.
- The list is called Cursed in every language, it was Broken.

## 0.1.0

- A table of parcel counts in a corner of the screen: a column for all parcels and for every mark, a row per destination,
  damaged parcels, parcels still to stamp, and parcels by size in a small table of their own: a drawn box for every size
  with the cells it takes on a shelf, like 2×2. A long list of sizes wraps into columns as tall as the destinations.
- Every column has an icon. The game has none for broken parcels and parcels that must not touch others, so the board draws its own.
- Lists open and close with Ctrl+O, the board hides with Ctrl+P and with the game's menus.
- Per list: always, when not empty or never. Empty rows hidden until a parcel comes. Corner, size, a faint background and destination names are settings.
- Only for the player who has it: never paused and works with any host.
