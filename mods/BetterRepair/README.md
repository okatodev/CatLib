# Better Repair

Changes how much cardboard the repair table has and lets it repair several parcels at once. In the game the table holds three sheets:
every repair of a damaged parcel uses one, and a new day brings all three back. The table repairs only when exactly one parcel lies on it.

## Settings

| Setting | Scope | Meaning |
|---|---|---|
| Unlimited cardboard | session | the table never runs out |
| Stock | session | how many sheets the table holds, 1 to 99; 3 is the game's amount |
| New day brings | session | the full stock, like in the game, or a set number of sheets |
| Sheets per day | session | how many sheets a new day adds when a set number is chosen |
| Several parcels at once | session | one press repairs every damaged parcel on the table, a sheet for each |
| Parcels at once | session | how many parcels on the table one press repairs, 2 to 8 |
| Cardboard messages | local | shows how much is left after a repair and how much a new day brought |

The defaults match the game: 3 sheets, the full stock every new day, one parcel at a time.

## How it works

The table shows up to three sheets. With a larger stock it keeps showing three until fewer are left,
then one sheet disappears with every repair, like in the game. With an empty stock the table cannot repair until the next day.

For the cardboard the mod does not patch the game. It reads which sheet the table would use next and after every repair sets it back
to match the stock, showing or hiding the sheets on the table. A new day starts with the dawn recap, the moment the game itself brings its three sheets back, so the recap save already has the new stock.

## Several parcels at once

The game turns the repair button on and repairs only when exactly one parcel lies on the table, the stacked ones included.
With several parcels at once turned on, the mod patches `EntityRepairWorkstation.RepairParcel` and
`LinkedStore_OnEntityStoreChanged`, and while they run, `EntityInteractableStore.CountChildParcels` and `GetFirstEntity` of that table:

- With 2 parcels up to the set number on the table and at least one of them damaged, the game sees one damaged parcel,
  lights the button and repairs it with its own sound, effect and achievement. The mod repairs the other damaged parcels right after it,
  each with the repair effect.
- Every repaired parcel uses a sheet; undamaged parcels on the table use none. With fewer sheets than damaged parcels, as many are repaired
  as there are sheets.
- With more parcels than the set number on the table, the button stays off, like in the game.
- A press reaches every player's game, and every game repairs the same parcels; only the host counts the cardboard and sends the stock.

## Saves

The stock is kept with CatLib per game save, in `CatLibSaves/<save>/catlib.betterrepair.json` next to the game's save folder,
under the key `cardboard`. It is written only when the game saves, and only by the host.
A save without it starts with a full stock. Removing the mod leaves the game save as it was.

## Multiplayer

Every player needs the mod; while someone does not have it, the mod is paused for everyone. The host counts the cardboard and sends the stock to everyone;
a player who joins gets it from the host.

## Changes

See [CHANGELOG.md](CHANGELOG.md).
