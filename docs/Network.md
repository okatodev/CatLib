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

## Session settings

On an accepted client, session settings take the host's values as overrides:
`Value` becomes the host value, `LocalValue` and the file stay untouched, and appliers run as usual.
Local edits during the session are saved but do not replace the override.
When the session ends, the overrides are removed and the local values apply again.

Host values that the client does not know, that are local settings on the client, that cannot be parsed
or that are outside the client's accepted values are ignored and reported.
Settings that require a restart are never changed live; a different host value is reported instead.

In the Mods tab, overridden settings are read-only, marked "(host)", and the context line shows the player's own value.

## Wire format

Little-endian binary. Every message starts with a 7-byte header: the magic `CATL`, a 16-bit protocol version and a message type byte.
Strings are a presence flag, a 16-bit byte length and UTF-8 bytes.
Limits: 64 KiB per message, 1 KiB per string, 1024 items per list.
A message with another protocol version is detected from the header and its payload is not parsed.
Malformed messages are rejected with `WireFormatException` and ignored by the sessions.
