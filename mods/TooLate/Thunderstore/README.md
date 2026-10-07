# Too Late

In the game a friend can only join in the lobby, before the day starts. Once you are playing, they have to wait for the next game.
With Too Late they can join whenever they want.

## What it does

- **Join any time.** A friend joins through Steam, the same way as into a lobby, and loads into the warehouse as it is right now:
  the parcels on the shelves, the stamps, the time of day and your progress.
- **Their counter opens.** The game opens a counter for each player, the third one for the third player and so on.
  A friend who joins late gets theirs too.
- **The game goes on.** Nobody waits while a friend loads. If they come during the day's results, they wait in the lobby and come in right after.
- **Up to 8 players.** Raise the limit in the settings. The lobby shows four player cards and the others as "+2 players".
  The warehouse keeps its four counters, and the day is as busy as for four players.

## Settings

Settings → Mods → Too Late. These are the host's settings.

- **Join a running game**: on or off. Off, joining works as in the game.
- **Players at most**: from 4 to 8, you included. 4 by default, like in the game.
- **Joining messages**: a short note when a friend starts joining, waits for the results and is in the game.

## Playing with friends

Only the host needs it. Friends can join without any mods.
If a friend has mods that every player needs and you don't, those mods are paused for the game, like for any player.

## Saves

Your save is not changed. For a friend who joins late, the warehouse is copied into a separate file,
and their game keeps a copy of your save like it always does in multiplayer.

## Not yet

- Customers already standing at a counter appear for the friend with the next customer.
- Players 5 to 8 start at the same spots as players 1 to 4.

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues). Tell us whether you were hosting or joining and when the friend came in.
If the game crashed, attach the folder from the CatLib crash window, otherwise `BepInEx/LogOutput.log` from the host
and the friend before you start the game again.
