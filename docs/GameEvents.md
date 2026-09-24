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

## Rules for mod authors

- Returning to the main menu is a full restart. Every manager is destroyed and recreated. Never cache manager references.
- `GameRestartStarted` can fire twice in a row. Handlers must be idempotent.
- `Gameplay.*` events are host only. Use `Bootstrap.LevelLoadFinalized` or `Player.LocalPlayerSpawned` to detect entering a level on every machine.
- `NetworkTick` does not mean a multiplayer session is active.
- There is no late join. A client that connects while a game is running stays connected but never loads the level and never spawns.
  Being connected is not the same as being in the session.
