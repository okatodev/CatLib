using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using CatLib.Voice;
using Purr.Logic;
using UnityEngine;

namespace Purr.Settings;

public sealed class VoiceSettings
{
    public VoiceSettings(CatSettings settings)
    {
        Enabled = settings.Local("Voice", "Enabled", true,
            "Voice chat with the players who have this mod too. Off, you neither talk nor hear anyone.");
        Volume = settings.Local("Voice", "Volume", 1f,
            "How loud the other players are: 1 is as Steam gives them, 2 is twice as loud.", new AcceptableValueRange<float>(0f, 2f));

        Mode = settings.Local("Microphone", "Mode", MicMode.PushToTalk,
            "Push to talk sends your voice only while you hold the key below. Open microphone sends it whenever Steam hears you. The microphone itself is the one chosen in Steam: Settings, Voice.");
        PushToTalkKey = settings.Local("Microphone", "PushToTalkKey", new KeyboardShortcut(KeyCode.V),
            "Hold it to talk in push to talk mode.");
        Muted = settings.Local("Microphone", "Muted", false,
            "Turns your microphone off: nobody hears you, you still hear everyone.");
        MuteKey = settings.Local("Microphone", "MuteKey", new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl),
            "Turns the microphone off or on.");

        Deafened = settings.Local("Headphones", "Deafened", false,
            "Turns the other players off: you hear nobody, and your microphone is off too until you turn them back on.");
        DeafenKey = settings.Local("Headphones", "DeafenKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl),
            "Turns the other players off or on.");

        Sound = settings.Local("Sound", "Sound", VoiceSound.Spatial,
            "In space, a voice comes from the player's cat and gets quieter with distance. Flat, everyone sounds the same wherever they are. In the lobby voices are always flat.");
        HearDistance = settings.Local("Sound", "HearDistance", CatVoice.DefaultHearDistance,
            "In space, how far away a voice is still heard, in metres.", new AcceptableValueRange<float>(5f, 80f));
        FullVolumeDistance = settings.Local("Sound", "FullVolumeDistance", CatVoice.DefaultFullVolumeDistance,
            "In space, up to this distance a voice is at full volume, in metres.", new AcceptableValueRange<float>(0f, 10f));

        ShowSpeakers = settings.Local("Screen", "ShowSpeakers", true,
            "Lists who is talking right now in a corner of the screen during a level.");
        Corner = settings.Local("Screen", "Corner", SpeakersCorner.TopRight,
            "The corner of the screen for the list of who is talking.");
        NameTags = settings.Local("Screen", "NameTags", true,
            "Colours the name over a cat while that player is talking.");
        Notices = settings.Local("Screen", "Notices", true,
            "Says on the screen when the microphone or the headphones are turned off or on with the keys.");
    }

    public Setting<bool> Enabled { get; }

    public Setting<float> Volume { get; }

    public Setting<MicMode> Mode { get; }

    public Setting<KeyboardShortcut> PushToTalkKey { get; }

    public Setting<bool> Muted { get; }

    public Setting<KeyboardShortcut> MuteKey { get; }

    public Setting<bool> Deafened { get; }

    public Setting<KeyboardShortcut> DeafenKey { get; }

    public Setting<VoiceSound> Sound { get; }

    public Setting<float> HearDistance { get; }

    public Setting<float> FullVolumeDistance { get; }

    public Setting<bool> ShowSpeakers { get; }

    public Setting<SpeakersCorner> Corner { get; }

    public Setting<bool> NameTags { get; }

    public Setting<bool> Notices { get; }
}
