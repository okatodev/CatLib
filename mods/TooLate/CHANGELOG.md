# Changelog

## Not released yet

- A single player game no longer loads forever when Steam cannot reach its relay network. The game then gives your own client
  the id 1, and Too Late took it for a player who joins late and kept it in the lobby. Now only other Steam players join late,
  and without the Steam network the log says that nobody can join. Needs CatLib 0.8.0.

## 0.1.0

- Players can join a game in progress. They get a snapshot of the warehouse as it is at that moment, written into a separate file;
  the host's save is not touched. Messages of the game that arrive while they load are kept and sent to them in order once their level is ready.
- During the day's results a joining player waits in the lobby and loads right after.
- The counter of a player who joins late opens like for anyone else.
- Up to 8 players in the settings, 4 by default like in the game. The lobby shows players without a card as a count.
- Only the host needs the mod.
- Developer menu: recording the game's network messages, the state of joining players, and a snapshot written on demand.
