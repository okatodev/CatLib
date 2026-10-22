using System;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.UI;
using CatLib.Voice;
using Purr.Logic;
using Purr.Scene;
using Purr.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Purr;

public sealed class PurrController
{
    public const string IconResource = "Purr.Icons.mic.png";

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly SpeakersView _speakers = new();
    private readonly NameTags _tags;
    private Sprite _icon;
    private bool _iconTried;
    private bool _wasTransmitting;

    public PurrController(CatLogger log, TextCatalog texts, VoiceSettings settings)
    {
        _log = log;
        _texts = texts;
        Settings = settings;
        _tags = new NameTags(log);
    }

    public VoiceSettings Settings { get; }

    public void Update()
    {
        try
        {
            ReadKeys();
            Apply();
            Draw();
        }
        catch (Exception exception)
        {
            _log.Error("Voice chat update failed", exception);
        }
    }

    public void Forget()
    {
        _speakers.Destroy();
        _tags.Clear();
    }

    private void ReadKeys()
    {
        if (!Settings.Enabled.Value || IsTyping())
        {
            return;
        }

        if (Settings.MuteKey.Value.IsDown())
        {
            var muted = !Settings.Muted.Value;
            Settings.Muted.LocalValue = muted;
            Notice(MicLogic.MuteNotice(muted));
        }

        if (Settings.DeafenKey.Value.IsDown())
        {
            var deafened = !Settings.Deafened.Value;
            Settings.Deafened.LocalValue = deafened;
            Notice(MicLogic.DeafenNotice(deafened));
        }
    }

    private void Apply()
    {
        var enabled = Settings.Enabled.Value;
        var held = !IsTyping() && Held(Settings.PushToTalkKey.Value);
        var transmitting = MicLogic.Transmits(enabled, Settings.Mode.Value, held, Settings.Muted.Value, Settings.Deafened.Value);
        if (transmitting != _wasTransmitting)
        {
            _wasTransmitting = transmitting;
            _log.Debug(transmitting ? "The microphone is on" : "The microphone is off");
        }

        CatVoice.Transmitting = transmitting;
        CatVoice.Deafened = !MicLogic.Hears(enabled, Settings.Deafened.Value);
        CatVoice.Volume = Settings.Volume.Value;
        CatVoice.Sound = Settings.Sound.Value;
        CatVoice.HearDistance = Settings.HearDistance.Value;
        CatVoice.FullVolumeDistance = Settings.FullVolumeDistance.Value;
    }

    private void Draw()
    {
        var speaking = CatVoice.Speakers;
        if (Settings.Enabled.Value && Settings.ShowSpeakers.Value && !HudLayer.IsGameMenuOpen && speaking.Count > 0)
        {
            var names = MicLogic.SpeakerNames(speaking, CatVoice.LocalId, CatNetwork.PlayerName, _texts.Get("speakers.you"));
            _speakers.Show(names, Icon(), _texts.Get("speakers.title"), Settings.Corner.Value);
        }
        else
        {
            _speakers.Hide();
        }

        if (Settings.Enabled.Value && Settings.NameTags.Value)
        {
            _tags.Show(speaking.Where(id => id != CatVoice.LocalId).ToList());
        }
        else
        {
            _tags.Clear();
        }
    }

    private void Notice(MicNotice notice)
    {
        if (notice == MicNotice.None || !Settings.Notices.Value)
        {
            return;
        }

        Notifications.Show(_texts.Get("notice." + notice));
    }

    private Sprite Icon()
    {
        if (!_iconTried)
        {
            _iconTried = true;
            _icon = UiSprites.FromResource(typeof(PurrController).Assembly, IconResource);
        }

        return _icon;
    }

    public static bool Held(KeyboardShortcut shortcut) =>
        shortcut.MainKey != KeyCode.None && UnityInput.Current.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(UnityInput.Current.GetKey);

    private static bool IsTyping()
    {
        try
        {
            var events = EventSystem.current;
            var selected = events == null ? null : events.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            var input = selected.GetComponent<TMP_InputField>();
            return input != null && input.isFocused;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
