# Parcel Board

A small board in the corner of the screen that counts the parcels of the level: all of them, and those with each mark,
by destination. Parcels for Cat's Island are the ones that arrived; every other destination is a parcel to send.

The mod only reads what the game shows and changes nothing. It is `ClientOnly`: it shows in the players' mods list of the lobby,
works with a host without it, even without CatLib, and is never paused.

## The board

One table in a corner of the screen. Every list is a column with its icon and total; the rows are the destinations,
with their stamp icons, and a cell shows how many parcels of that column go there. A cell without parcels stays empty.
Closed, only the icons and totals show; open, the rows come below them, or above them in a bottom corner.
Cat's Island comes first, a line parts it from the destinations to send. Sizes are a small table of their own next to it.

| List | Counts |
|---|---|
| All | every parcel |
| Heavy, Fragile, No contact, Lovers, Broken | parcels with that behaviour mark |
| Dark, Frozen, Hot, Cold, Bright | parcels with that storage mark |
| Damaged | damaged parcels |
| To stamp | parcels for other destinations that still miss a stamp: destination, weight or a mark |
| Sizes | every parcel by size instead of destination: a drawn box and the cells it takes on a shelf, like 3×2 |

Ctrl+O opens or closes the lists, Ctrl+P hides or shows the board. The board hides with the pause menu, the settings and the day recap.

## Settings

Every setting is personal.

| Setting | Meaning |
|---|---|
| Show the board | on or off |
| Show or hide, Open or close lists | the two keys |
| Open at start | lists open when a level starts |
| Corner | top left, top right, bottom left or bottom right; at the bottom the lists open upwards |
| Size, Background | scale of the board and how solid its faint dark plate is, from clear (default almost clear) to solid |
| Destination names | text next to the destination icons |
| Order | the most parcels first, or the game's order of destinations |
| Hide empty rows | leaves out destinations and sizes with no parcels, until there is one |
| Count | everything in the level, or without the parcels on the boat and at customers |
| Lists | per list: always, when not empty, or never |

## How it works

- Parcels come from the game's own register, `ParcelManager`, through `CatParcels` of CatLib, twice a second.
- Icons are the game's stamps: the destination stamp of a region, the stamp of a mark. All, Damaged and To stamp use small pictures
  from the game's own art, embedded in the mod. Region names come from the game's translations.
- "To stamp" reads the stamps on every parcel to send: its destination stamp, as many weight stamps as its weight class asks for,
  and the stamps of its marks. **Parcel Board → Parcels in the log** names what each parcel still misses.
- The board is drawn on the game's HUD canvas with `CountTable` of CatLib and hides while the game's menus are open.
  Texts are light with a dark outline, like the game's own hints, so they read without a background.
- The developer menu has **Parcel Board → Parcels in the log**: every parcel with its destination, marks, missing stamps, size and place,
  and the counts of every list; and **Parcel sizes in the log**: the footprint and box of one parcel of every size.

## Changes

See [CHANGELOG.md](CHANGELOG.md).
