# Purr!

Talk to the other players while you play. A voice comes from the player's cat and gets quieter the farther away it is,
so you hear who is next to you and the one across the warehouse only faintly. Hold V to talk, or keep the microphone open.

The voice goes through Steam: Steam records the microphone chosen in Steam (Settings, Voice), compresses it and plays it back,
with its own noise and volume handling. The mod is `ClientOnly`: it works with a host without it, but you only talk to players
who have it too, and only when the host has it, because every voice passes through the host.

## Keys

| Key | What it does |
|---|---|
| V | hold to talk (push to talk) |
| Ctrl+M | microphone off or on: nobody hears you, you still hear everyone |
| Ctrl+H | headphones off or on: you hear nobody, and your microphone is off too |

The keys work in the lobby and in a level, not while you type in a text field. A short notice says when the microphone
or the headphones go off or on.

## On the screen

- During a level, a small list in a corner shows who is talking right now, you first.
- The name over a cat turns green while that player talks.

## Settings

Every setting is personal.

| Setting | Meaning |
|---|---|
| Voice chat | on or off; off, you neither talk nor hear anyone |
| Volume of others | 0 to 2: 1 is as Steam gives the voices, 2 twice as loud |
| Mode | push to talk or open microphone |
| Push to talk key, Microphone key, Headphones key | the three keys |
| Microphone off, Headphones off | the states the keys turn on and off, kept between games |
| Voices | in space (from the cat, quieter with distance) or flat (everyone the same); in the lobby voices are always flat |
| Hearing distance | in space, how far a voice is still heard: 5 to 80 m, 25 by default |
| Full volume up to | in space, how close a voice is at full volume: 0 to 10 m, 3 by default |
| Who is talking, Corner | the list of who is talking and its corner |
| Coloured names | the green name over a talking cat |
| Notices | the notices of the keys |

## How it works

- Voice is the `CatVoice` API of CatLib, see [Voice](../../docs/Voice.md). Steam voice records and compresses,
  the voice goes over CatLib's own Steam channel as small packets that may get lost rather than arrive late,
  and every player's voice plays from an `AudioSource` at their cat, through the game's master volume.
- The game's code still has traces of a voice chat it was once meant to have (a push-to-talk action, saved voice volumes),
  but no voice library; the mod does not touch those.
- The developer menu of CatLib has **Voice → Hear yourself**, which plays your own voice back to you through Steam,
  and **Voice → Voice state in the log**.

## Changes

See [CHANGELOG.md](CHANGELOG.md).
