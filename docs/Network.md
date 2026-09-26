# Multiplayer compatibility

`CatLib.Net` checks that the host and every client run compatible mods and shares session settings from the host.
This document describes the protocol and the logic. The adapter to the game's network layer is added separately.

## Declaring a mod

```csharp
CatNetwork.Declare(this, SessionPolicy.RequiredOnAll);
CatNetwork.Declare(this, SessionPolicy.HostOnly);
CatNetwork.Declare(this, SessionPolicy.ClientOnly);
CatNetwork.Declare(this, SessionPolicy.RequiredOnAll, VersionRule.Exact);
```

| Policy | Meaning |
|---|---|
| `RequiredOnAll` | Changes gameplay for everyone. The host and every client need it in a compatible version. |
| `HostOnly` | Runs on the host only. Clients may or may not have it. |
| `ClientOnly` | Local only, for example visuals. Never checked. |

| Version rule | Compatible when |
|---|---|
| `Exact` | Versions are identical |
| `SameMinor` (default) | Major and minor are equal, patch may differ |
| `SameMajor` | Major is equal |
| `Any` | Always |

The host's declaration wins when both sides declare a mod differently.
Versions that are not numeric are compared as exact strings.

## Handshake

1. A client connects. It sends `Hello`: CatLib protocol version, the full game version and its declared mods.
2. The host compares both sides and answers with `Verdict`: accepted or not, the list of problems and, when accepted, a snapshot of all session settings.
3. While the session lasts the host sends `SettingsUpdate` to accepted clients when a session setting changes.

Problems: `ProtocolMismatch`, `GameVersionMismatch`, `MissingOnClient`, `MissingOnHost`, `VersionMismatch`.

A client that never sends `Hello` is treated as a client without CatLib after a timeout.
It is compatible only if the host has no `RequiredOnAll` mods.
A host that never answers is treated as a host without CatLib; the client is compatible only if it has no `RequiredOnAll` mods.
A verdict that arrives after the timeout is ignored.

## Session role

`CatNetwork.Role` tells what the local player is in the current session:

| Role | When |
|---|---|
| `Offline` | main menu, no session |
| `Host` | hosting, including single player, where the game runs its own server |
| `Client` | joined another player's lobby or game |

`CatNetwork.IsAuthority` is true for `Offline` and `Host`. A mod whose decisions must be the same for everyone
makes them only while it has authority and passes the result to clients in a hidden session setting:

```csharp
var plan = settings.Session("Sync", "Plan", "", "Written by the mod.").HiddenInMenu();
if (CatNetwork.IsAuthority)
{
    plan.LocalValue = Decide();
}
```

A client reads `plan.Value`, which is the host's value once the handshake is accepted.
Until then, and when the host rejected the client, `plan.IsOverridden` is false and the client should not act on its own old value.

## Session settings

On an accepted client, session settings take the host's values as overrides:
`Value` becomes the host value, `LocalValue` and the file stay untouched, and appliers run as usual.
Local edits during the session are saved but do not replace the override.
When the session ends, the overrides are removed and the local values apply again.

Host values that the client does not know, that are local settings on the client, that cannot be parsed
or that are outside the client's accepted values are ignored and reported.
Settings that require a restart are never changed live; a different host value is reported instead.

In the Mods tab, overridden settings are read-only, marked "(host)", and the context line shows the player's own value.

## Mod messages

A mod talks to the same mod on other players through its channel:

```csharp
var channel = CatNetwork.Channel(this);

channel.Received += message =>
{
    if (!message.FromHost)
    {
        if (TryApply(message.Text))
        {
            channel.Broadcast("state", message.Text);
        }

        return;
    }

    ShowState(message.Text);
};

channel.PeerJoined += peer => channel.SendTo(peer, "full", FullState());

void OnPlayerClicked(string change) => channel.SendToHost("click", change);
```

- Messages go only between the host and players, never between two players directly:
  a player asks with `SendToHost`, the host decides and answers with `Broadcast` or `SendTo`.
- The same code works everywhere. In single player, in the main menu and on the host,
  `SendToHost` and `Broadcast` also deliver to this game itself, with the next frame and in the order they were sent.
  `IsLocal` marks those copies; `FromHost` tells a request (`false`) from a decision of the host (`true`).
- A message only travels between two players when both have the mod with compatible versions.
  The host lists these shared mods in its verdict, so this also works for a player who was rejected because of another mod
  and stayed in the game with the `Warn` action.
- `PeerJoined` runs on the host when a player with the mod finished the handshake; send it the full state there.
  `PeerLeft` runs when that player disconnects.
- `SendToHost` returns `false` on a player whose handshake is not finished or whose host does not share the mod.
  `CanSendToHost` checks that beforehand. `Broadcast` and `SendTo` on a player do nothing.
- Limits: a name of up to 64 bytes and up to 16 KiB of data. Larger values throw `ArgumentException`.
  The host accepts up to 60 messages per second from one player and drops the rest with a warning.
- Messages are reliable and ordered per player. Handlers run on the main thread; an exception in one handler does not stop the others.
- The host is the authority: check every request before applying it, a player can send anything.

## Wire format

Little-endian binary. Every message starts with a 7-byte header: the magic `CATL`, a 16-bit protocol version and a message type byte.
Strings are a presence flag, a 16-bit byte length and UTF-8 bytes.
Limits: 64 KiB per message, 1 KiB per string, 1024 items per list.
Protocol 4 added mod messages (type 5) and the list of shared mods at the end of the verdict.
A message with another protocol version is detected from the header and its payload is not parsed.
Malformed messages are rejected with `WireFormatException` and ignored by the sessions.

## Game adapter

CatLib talks over a separate Steam channel (`SteamNetworkingMessages`, channel `0x4341`) and never adds messages to the game's own protocol.
Players without CatLib never read that channel, so they are not affected.

- Game network events are only recorded in the game's callbacks; every Steam call happens on the next frame from CatLib's own loop.
- The host starts when the game raises `ServerStarted` and sends `Announce` every second to each connected player that has not answered yet.
- A client starts after `ClientConnectionAcknowledged`. It accepts an `Announce` only from a player the game reported through
  `OtherClientConnected`, takes the sender as the host and answers with `Hello`, resent every 2 seconds until the verdict arrives.
- Either side gives up after 12 seconds and treats the other as a player without CatLib.
- The host answers a repeated `Hello` with the same verdict without evaluating the player again.
- When a session setting changes on the host, the new value is sent to every accepted player.
- Leaving the session, or any game restart, stops the session and removes all session overrides.
- Connections that are not made through Steam (direct IP, offline mode) skip the checks.

### Incompatible players

With `OnIncompatiblePlayer = Warn` both sides are told and the player stays.
With `Disconnect` the verdict carries a flag that the host will disconnect the player:

- the client shows the reason in the lobby and leaves after 4 seconds through the lobby's own back button, the same way a player leaves;
- the host disconnects the player itself after 8 seconds if the player is still connected on its side.
  The game does not tell the host when a client leaves the lobby, so without this the host would only notice after its connection timeout.

### What players see

- In a level: the game's own notification, two lines of at most 34 characters, for example "RENTAI was disconnected:" and "CatLib Demo Network Mod".
  The full text with versions goes to the log and to the status line of the Mods tab.
- In the lobby, as a client: a line under "Waiting for the host".
- In the lobby, as the host: "other mods" under the player's name on the player's card.
- Player names come from Steam, mod names from the declarations; ids are used only when a name is unknown.

Two ways to call Steam are available. `Interop` goes through the game's Steamworks.NET.
`Flat` calls the flat C API of `steam_api64` directly with buffers laid out as in the Steamworks SDK;
it looks up the functions first and falls back to `Interop` when they are missing.
Before the first send, accept and receive of each session, CatLib records a `Net.NativeCall` event,
so a crash inside Steam can be traced to the call that caused it.

CatLib's own settings, shown as "CatLib" on the Mods tab:

| Setting | Default | Meaning |
|---|---|---|
| `Network.Enabled` | `true` | Take part in checks and session settings. Applies from the next session. |
| `Network.OnIncompatiblePlayer` | `Warn` | `Warn` shows a message to the host, `Disconnect` also disconnects the player. |
| `Network.SteamApi` | `Interop` | How Steam is called. Only for diagnostics. Applies from the next session. |
