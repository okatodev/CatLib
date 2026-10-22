# Voice

`CatVoice` in `CatLib.Voice` lets players talk to each other. It records with Steam voice, sends the voice over the CatLib Steam channel
and plays every player's voice from their cat. [Purr!](../mods/Purr/README.md) is built on it; a mod only needs
`CatVoice` when it wants voice of its own, for example a radio that only works in one room.

## What the game has

The game's code still has traces of a voice chat on Dissonance, but the released game does not contain it. What is left:
the push-to-talk action `PushToTalk` in `GameInputs` (F), `SettingsManager.IsLocallyMuted` and `IsLocallyDeafened`,
the saved `MicrophoneName`, `PushToTalk`, `VoiceVolume` and a volume per Steam player in the `RemoteUser` table,
the protocol codes `PlayerVoiceRoom` and `PlayerVoiceRoomBroadcast` and an event `NetworkManager.DissonanceInitialized` that nothing raises.
Dissonance itself, its codec and its transport are not in the game, and nothing records or plays a voice.

What the game does have is the whole of Steamworks: `ISteamUser` records the microphone chosen in Steam, compresses the voice
with Steam's codec and decompresses it again. `CatVoice` uses that.

## Turning voice on

Voice only travels between players who have the same mod that turned it on:

```csharp
CatNetwork.Declare(this, SessionPolicy.ClientOnly);
CatVoice.Enable(this);
```

Only one mod can own voice; a second `Enable` with another mod id throws. `CatVoice.Disable()` turns it off again.

Every frame the mod says whether the player talks, and how voices play:

```csharp
CatVoice.Transmitting = Input.GetKey(KeyCode.V);
CatVoice.Deafened = headphonesOff;
CatVoice.Volume = 1f;
CatVoice.Sound = VoiceSound.Spatial;
CatVoice.HearDistance = 25f;
CatVoice.FullVolumeDistance = 3f;
```

| Member | Meaning |
|---|---|
| `Transmitting` | Steam records while it is true; the voice goes to the other players |
| `IsRecording` | Steam is recording right now |
| `Deafened` | no voice plays; who is talking is still known |
| `Volume` | 0 to 2 for all voices; above 1 the samples are made louder |
| `PlayerVolume(id)`, `SetPlayerVolume(id, volume)` | 0 to 2 for one player, on top of `Volume`; kept for the session |
| `IsPlayerMuted(id)`, `SetPlayerMuted(id, muted)` | one player is not played |
| `Sound` | `Spatial`: from the player's cat, quieter with distance; `Flat`: the same everywhere |
| `HearDistance`, `FullVolumeDistance` | in metres, for `Spatial` |
| `IsSpeaking(id)`, `Speakers`, `IsLocalSpeaking` | who is talking, by Steam id, this player too |
| `SpeakingChanged` | `(steamId, speaking)` when someone starts or stops talking |
| `LocalId` | this player's Steam id |

A player counts as talking while their voice is louder than a whisper (`SpeakingLevel`), and for a third of a second after it.
Steam may send quiet packets with an open microphone; they play but do not count.

## How it travels

- Steam gives the compressed voice every frame while it records. Each piece goes out as one unreliable packet
  on its own Steam channel, `CatVoice.Channel` (`0x4356`), next to the CatLib channel: the packet has the speaker's Steam id
  and a sequence number, the rest is Steam's data.
- Like mod messages, voice goes between the host and the players only. A player sends to the host; the host plays it
  and passes it on to every other player who shares the mod, with the speaker set to who really sent it.
  So the host passes on every voice, and a player who joins a host without the mod talks to nobody.
- The host takes at most 150 packets a second from one player. Old and repeated packets are skipped.
- Nothing is sent in single player or before the lobby check has finished.

## How it plays

- Every speaker has an `AudioSource` under `CatLib_Voice`, which stays between scenes. It plays a looping clip of one second,
  and CatLib writes the voice into it in blocks of 10 ms, about 100 ms ahead of what plays, from the main thread.
  A burst of voice starts once 60 ms of it have arrived, and the source stops after 0.6 s of silence.
- With `Spatial` the source stands at the speaker's cat (`GamePlayers.TryGetGhost`, which unlike the game's `TryGetPlayerGhost` writes no error while the cat is not there yet), a little above the ground,
  and gets quieter along a curve: full volume up to `FullVolumeDistance`, silent at `HearDistance`. Without a cat, in the lobby or
  before the level is loaded, the voice is flat.
- The source plays through the game's mixer group for voice if there is one, otherwise through `Master`,
  so the game's master volume and its pause muffling apply. The log names the group the first time.
- Voice plays at the output rate of the game, at most 48 kHz, the rate Steam decompresses to.

## Developer menu

**Voice → Voice state in the log** writes the owner, whether Steam records, what was sent, received and passed on,
and for every speaker the queue, gaps, hitches and the distance to their cat.
**Voice → Hear yourself** keeps the microphone on and plays your own voice back flat, through Steam's compression and the whole
playback; it works offline and without Purr!, turn it off after the check.
**Voice → Record a voice sample** keeps the microphone on and keeps what Steam records; turned off, it writes the sample
to `CatLib-voice-sample.bin` in the game's data folder (`AppData/LocalLow/Maracas Studio/CatMailCo`), which every game copy on the computer reads.
**Voice → Send the voice sample** sends it to the other players at its own pace as if you said it now: a second copy of the game
that cannot use the microphone, for example one in a sandbox, talks this way.

## Limits

- The microphone is the one chosen in Steam (Settings, Voice), with Steam's own noise and volume handling.
- Voice depends on Steam: without the Steam network, nobody can join and nothing is sent.
- Every voice passes through the host, so the host's upload carries all of them: a few kilobytes a second per speaker and listener.
