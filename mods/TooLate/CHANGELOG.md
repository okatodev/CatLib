# Changelog

## 0.2.2

Needs CatLib 0.7.5.

- Joining a level in progress works again after the game patch of November 2026. The check in the game's code that turns
  a joining player away from a started level moved a little, so the mod no longer found it and left joining closed, like in the game.

## 0.2.1

Needs CatLib 0.7.4.

- Writing the snapshot for a joining player is noted for crash reports: if the game crashes in the middle of it, the report names it.

## 0.2.0

- Players 5 to 8 start at spots of their own, between the game's four and half a step past the last one.
  Their own game picks the spot, so a player needs Too Late for it; the host still is the only one who must have it.
- Customers already waiting at a counter are there for a friend who joins, with their looks, their request and the parcels they brought.
  Before, the friend saw an empty counter until the next customer came.
- What players carry when a friend joins is in their hands for the friend too, also a parcel just taken from a customer.
  Before, the friend got the parcel where it was when they started loading, but in nobody's hands.

## 0.1.1

Needs CatLib 0.7.1.

- A single player game no longer loads forever when Steam cannot reach its relay network. The game then gives your own client
  the id 1, and Too Late took it for a player who joins late and kept it in the lobby. Now only other Steam players join late,
  and without the Steam network the log says that nobody can join.
- The log says when your game started as soon as the game has its server, also without the Steam network,
  where it used to say that players can join although nobody can reach the game.

## 0.1.0

- Players can join a game in progress. They get a snapshot of the warehouse as it is at that moment, written into a separate file;
  the host's save is not touched. Messages of the game that arrive while they load are kept and sent to them in order once their level is ready.
- During the day's results a joining player waits in the lobby and loads right after.
- The counter of a player who joins late opens like for anyone else.
- Up to 8 players in the settings, 4 by default like in the game. The lobby shows players without a card as a count.
- Only the host needs the mod.
- Developer menu: recording the game's network messages, the state of joining players, and a snapshot written on demand.
