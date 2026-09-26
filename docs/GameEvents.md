# Game events: observed behaviour

Everything below was recorded with `CatLib.Tests` timelines on game version `CMC 1.01.00.1763.9722.17497`
in two two-player sessions (one host, one client, both on Windows). It describes what the game does, not what the event names suggest.

## Who receives what

| Event | Host | Client | Meaning |
|---|---|---|---|
| `Network.ServerStarted` | yes | no | The local machine started hosting. |
| `Network.ClientConnected` | yes, for every client including itself | no | A client connected to the local server. |
| `Network.OtherClientConnected` | yes, for every other client | yes, for the host and other players | Another player joined the session. |
| `Network.ClientReady` | only its own id | only its own id | The local client finished connecting. |
| `Network.ClientConnectionAcknowledged` | only its own id | only its own id | The server acknowledged the local client. |
| `Network.ClientDisconnected` | every client that leaves, and itself on exit | only itself | A connection was closed. |
| `Player.PlayerDisconnected` | yes | not observed | A player left the level. |
| `Gameplay.InitializingParcels`, `Gameplay.GameStarted`, `Gameplay.GameStartedPhase2` | yes | **no** | Server-side game start. |
| `Player.LocalPlayerSpawned`, `Player.PlayerGhostSpawned` | yes | yes | Player objects spawned in the level. |
| `Network.NetworkTick` | always | always | Runs at about 30 Hz, including the main menu. Resets to 0 on restart. |

Client ids are SteamID64 values.

## Sequences

Hosting a lobby:
`ServerStarted` → `Client` object appears → `ClientConnected(self)` → `ClientReady(self)` → `ClientConnectionAcknowledged(self)`.
The host is also a regular client of its own server.

Joining a lobby as a client:
`Client` object appears → 1 to 2 s → `ClientReady(self)` → `ClientConnectionAcknowledged(self)` → `OtherClientConnected(host)`.

Starting a game from the lobby, both sides:
`GameStartedFromLobby` → `LevelLoadStarted` → `GameManager` appears → `ReceivedEntitySynchronization` → `LevelLoadFinalized`
→ `PlayerAmountChanged(1)` → `LocalPlayerSpawned` → `PlayerGhostSpawned` per other player → `PlayerAmountChanged(n)` → `ReceivedEntitySynchronization`.
Only the host additionally receives `InitializingParcels` → `GameStarted` → `GameStartedPhase2`.

A client leaves during a game, host side:
`ClientDisconnected(client)` → `PlayerDisconnected(client)` → `PlayerAmountChanged(n - 1)`.

A client leaves, client side:
`GameRestartStarted` → `ClientDisconnected(self)` → all managers are recreated → `MainMenuLoaded`.

The host leaves while the client is connected but not in a level, client side:
`GameRestartStarted` → `ClientDisconnected(self)` → 1.5 to 3 s → **a second `GameRestartStarted`** → all managers are recreated → `MainMenuLoaded`.
Reproduced in both sessions. The host leaving while the client is inside a level has not been observed yet.

## Saves

`SaveEvents` exposes `SaveFileSelected`, `GameSavingStarted`, `SuccessfullySaved` and `UnsuccessfullySaved` of the game's `SaveManager`.
Each record in the timeline carries the save file name and whether it is new, loading and hosted.

Observed in single player:

```
42.225  Save.SaveFileSelected  file=GameSave_20260923_201740_Cat-Mail-Co.bin new=no loading=no host=no
43.225  Bootstrap.LevelLoadStarted
43.516  Network.ServerStarted
94.049  Save.GameSavingStarted file=GameSave_20260923_201740_Cat-Mail-Co.bin new=no loading=no host=yes
94.073  Save.SuccessfullySaved file=GameSave_20260923_201740_Cat-Mail-Co.bin new=no loading=no host=yes
102.210 Bootstrap.GameRestartStarted
```

- `SaveFileSelected` comes in the main menu, before the level and before the server starts, so `host` is still `no`.
- Before a save is selected `GameInfo.SaveFileName` is `.bin`, an empty name with the extension.
- Starting and finishing a save happen in the same frame, 24 ms apart.
- Leaving to the main menu does not save.
- A freshly started save reports `new=yes` already in `SaveFileSelected`, with a file name the game has not written yet.
  Its first save follows about one second after `GameStartedPhase2`, still with `new=yes`.
- Which events a client receives is not observed yet.

The game saves on its own: after a level loads, when the game starts, when a customer is satisfied,
when the time of day changes and when a client connects. A save is an SQLite database per slot in `GameInfo.SaveDirectory`.
In multiplayer the host sends the whole save file to clients.

## Settings menu

The settings menu in the main menu is not initialized until the player opens it for the first time.
The in-game settings menu is created with the level and destroyed on every restart.
CatLib injects the Mods tab when a menu becomes ready and injects it again into every new instance.

## Rules for mod authors

- Returning to the main menu is a full restart. Every manager is destroyed and recreated. Never cache manager references.
- `GameRestartStarted` can fire twice in a row. Handlers must be idempotent.
- `Gameplay.*` events are host only. Use `Bootstrap.LevelLoadFinalized` or `Player.LocalPlayerSpawned` to detect entering a level on every machine.
- `NetworkTick` does not mean a multiplayer session is active.
- There is no late join. A client that connects while a game is running stays connected but never loads the level and never spawns.
  Being connected is not the same as being in the session.
