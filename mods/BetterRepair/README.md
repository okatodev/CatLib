# Better Repair

Changes how much cardboard the repair table has. In the game the table holds three sheets:
every repair of a damaged parcel uses one, and a new day brings all three back.

## Settings

| Setting | Scope | Meaning |
|---|---|---|
| Unlimited cardboard | session | the table never runs out |
| Stock | session | how many sheets the table holds, 1 to 99; 3 is the game's amount |
| New day brings | session | the full stock, like in the game, or a set number of sheets |
| Sheets per day | session | how many sheets a new day adds when a set number is chosen |
| Cardboard messages | local | shows how much is left after a repair and how much a new day brought |

The defaults match the game: 3 sheets, the full stock every new day.

## How it works

The table shows up to three sheets. With a larger stock it keeps showing three until fewer are left,
then one sheet disappears with every repair, like in the game. With an empty stock the table cannot repair until the next day.

The mod does not patch the game. It reads which sheet the table would use next and after every repair sets it back
to match the stock, showing or hiding the sheets on the table. A new day starts with the dawn recap, the moment the game itself brings its three sheets back, so the recap save already has the new stock.

## Saves

The stock is kept with CatLib per game save, in `CatLibSaves/<save>/catlib.betterrepair.json` next to the game's save folder,
under the key `cardboard`. It is written only when the game saves, and only by the host.
A save without it starts with a full stock. Removing the mod leaves the game save as it was.

## Multiplayer

Every player needs the mod. The host counts the cardboard and sends the stock to everyone;
a player who joins gets it from the host.

## Changes

### 0.1.0

- Unlimited cardboard, the stock size and what a new day brings.
