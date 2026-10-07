# Changelog

## 0.1.0

- Players can join a game in progress. They get a snapshot of the warehouse as it is at that moment, written into a separate file;
  the host's save is not touched. Messages of the game that arrive while they load are kept and sent to them in order once their level is ready.
- During the day's results a joining player waits in the lobby and loads right after.
- The counter of a player who joins late opens like for anyone else.
- Up to 8 players in the settings, 4 by default like in the game. The lobby shows players without a card as a count.
- Only the host needs the mod.
- Developer menu: recording the game's network messages, the state of joining players, and a snapshot written on demand.
