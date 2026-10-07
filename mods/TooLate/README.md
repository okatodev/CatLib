# Too Late

Joining a game in progress, and up to 8 players. Only the host needs the mod (`HostOnly`).

## How the game lets players in

The host's `Server.HandleClientConnect` turns a player away in two cases, both compiled into that method:

- `BootstrapManager.LoadingStarted` is set. In a running level the current game leaves it unset, so a player who connects then is accepted;
- the server already has 5 connections, the host's own one and the new player's included, which makes 4 players.

What keeps a late player out is the start: `ServerGameStart` goes to the players in the lobby once, when the host presses start.
A player who connects later is accepted, gets the save and waits in the lobby forever.

Everything else already works per player in the game's code: the host sends its save file to every new connection
(`SaveManager.NetworkManager_OnClientConnected`), the player asks to appear right after connecting and the host grants it at once,
the identifiers of the networked entities are sent to one player when that player's level has loaded,
and a player who appears gets the other players sent to them (`Server.HandlePlayerSpawned`).
In the lobby the host does all of this for everyone at the same time, so nobody plays while others load.

## What the mod does

1. `CodePatch` replaces the jump after the "level started" check while the host is in a ready level, and restores it when the level unloads or restarts,
   in case a game update starts setting that flag.
2. A player who connects after the host pressed start (`GameStartedFromLobby`, until the main menu or a restart) gets a ticket and waits in the lobby, which the game shows them on its own.
   The game's own save sending on connect is skipped for them.
3. When gameplay allows it (not during the day's results), the host writes a snapshot: a new SQLite file in `BepInEx/cache/TooLate`
   with the tables created by the game's own statements, `Metadata`, `Generic` and `ProgressionGeneric` copied from the save,
   and the entities, stamps and changes since the last save written by the game's `ExecuteSaveEntities`, `ExecuteSaveStamps`
   and `ExecuteEnqueuedGenericSaveData`. The save itself is only read.
4. The snapshot goes to the player as the game's `SaveSynchronization`, then `ServerGameStart`, only to that player.
5. Until the player has appeared, every message of the game to that player passes through `Server.SendMessage`:
   the joining steps pass, movement and animations are dropped (they are sent again all the time), the save copies the game broadcasts are dropped,
   entity messages (`GenericMessage`) are dropped (see step 8), only the newest game time is kept, everything else is held in order.
6. Clients match networked entities by the position where they appeared (`EntityNetwork.InitialPosition`).
   A client loads the saved entities only after `ServerFinalizeLoad`, so the game sends the identifiers twice: when the level has loaded,
   for the level's own entities, and when the player has appeared, for all of them. The mod does the same with its own list:
   the level's entities from the host's list, then also every saved entity with its position in the snapshot, which is exactly where it appears on the player.
   The host's own list holds where its saved entities appeared when the host loaded, which no longer fits a snapshot.
7. After the first identifiers comes `ServerFinalizeLoad`. The player finishes loading and appears, the game sends them the other players
   and the second identifiers, and only then the held messages follow, 200 per frame so Steam's send buffer never fills;
   before, they would reach saved entities the player does not have yet. `PlayerAmountChanged` opens the counters.
8. Much of the warehouse is not in the save for the other players: when the day starts, the host's game sends it.
   Players in the lobby get it, a late player does not. So from the start of the level the mod keeps the latest message of every kind
   for every entity and sends it to the joining player right after the second identifiers, in the order they last changed, only for entities they have:
   - the time of day (`GameTimePeriod`), which switches the light and the music;
   - `SetParcelDamaged`, `SetInteractionsLocked` and `BoatDestinations` (`Logic/LastingMessages.cs`), seen in `Server.BroadcastMessage`,
     which the game calls even when nobody else is connected;
   - entity messages (`Logic/StateLedger.cs`), like `Unlock` for opened doors and bars, `Pile:3` for the size of a pile,
     `CustomerCounter;OpenState;True` for an open counter, without what is of the moment (the captain's lines, customers, the counter's number wheel).
   Disposed entities are forgotten.
9. The player limit is the constant 5 in `Server.HandleClientConnect`, changed to the setting plus one.

## What a player from the lobby gets

Recorded with **Record game messages** on the host, a game started from the lobby with two players. Between `ServerGameStart` and the first minute:
the save on connect (`SaveSynchronization`), `ServerGameStart`, about 60 `EntityDisposed` for entities of the level the save removed,
the entity messages of the day's start (`Unlock` x14, `Pile` x3, `CustomerCounter;OpenState` per open counter, `SwitchLens`, a captain's line),
the first identifiers (level only) and `ServerFinalizeLoad`, `SetParcelDamaged` for every saved parcel, the other players,
the second identifiers (with the saved entities) and `GameTimePeriod`. A late player gets the same, except the `EntityDisposed`:
the player from the lobby cannot use them either (the entities have no identifier yet, the game writes "Failed to execute methods for entity"),
its own save loading removes those entities.

Also from the game itself, with or without Too Late: the player's name comes from the game's settings (`SaveManager.NetworkName`, else the Steam name),
so two copies of the game on one computer share it; storing into a disposed entity throws in `EntityInteractablePickable.HandleStored`
on a player from the lobby, about a hundred times when the level starts.

Protocol codes and the flow are listed in `Logic/ProtocolCodes.cs` and `Logic/MessageRoute.cs`.

## Not covered yet

- Entities the game does not save, like customers standing at a counter, appear for the late player with the next customer.
- Players 5 to 8 appear at the spawn points of players 1 to 4.

## Developer menu

Group **Too Late**:

- **Record game messages**: every network message of the game in the log, what the host sends to each player
  and what this game sends to the host; moving, animation and time messages as counts every 5 seconds. Turn it on on both games for a full picture.
- **Joining players in the log**: whether joining is open, the player limit, and each joining player with stage, held and dropped messages.
- **Snapshot in the log**: writes the snapshot now, without anyone joining, and what it holds.

The steps of every join are also records of the timeline: `TooLate.Connected`, `TooLate.Loading`, `TooLate.Synced`, `TooLate.Joined`, `TooLate.Left`, `TooLate.Failed`.
