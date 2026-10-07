using System;
using System.Collections.Generic;
using CatLib.Localization;
using CatLib.Logging;
using TMPro;
using UnityEngine;

namespace TooLate.Scene;

public sealed class LobbyOverflow
{
    public const string LabelName = "text_TooLateMorePlayers";
    public const float Gap = 24f;
    public const float LabelWidth = 260f;
    public const float LabelHeight = 60f;

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly List<ulong> _hidden = new();
    private IntPtr _lobby;
    private TMP_Text _label;

    public LobbyOverflow(CatLogger log, TextCatalog texts)
    {
        _log = log;
        _texts = texts;
    }

    public IReadOnlyList<ulong> Hidden => _hidden;

    public void OnJoined(MultiplayerLobbyInterface lobby, ulong steamId)
    {
        Track(lobby);
        if (steamId == 0 || HasSlot(lobby, steamId) || _hidden.Contains(steamId))
        {
            Refresh(lobby);
            return;
        }

        _hidden.Add(steamId);
        _log.Info($"[Lobby] {steamId} has no card in the lobby, {_hidden.Count} player(s) shown as a count");
        Refresh(lobby);
    }

    public void OnLeft(MultiplayerLobbyInterface lobby, ulong steamId)
    {
        Track(lobby);
        _hidden.Remove(steamId);
        if (_hidden.Count > 0 && HasEmptySlot(lobby))
        {
            var next = _hidden[0];
            _hidden.RemoveAt(0);
            lobby.ProcessClientJoin(next);
        }

        Refresh(lobby);
    }

    public void Forget()
    {
        _hidden.Clear();
        _lobby = IntPtr.Zero;
        _label = null;
    }

    private void Track(MultiplayerLobbyInterface lobby)
    {
        if (lobby != null && lobby.Pointer != _lobby)
        {
            Forget();
            _lobby = lobby.Pointer;
        }
    }

    private static bool HasSlot(MultiplayerLobbyInterface lobby, ulong steamId)
    {
        var slots = lobby._playerSlots;
        for (var index = 0; slots != null && index < slots.Count; index++)
        {
            var slot = slots[index];
            if (slot != null && !slot.IsEmpty && slot.SteamId == steamId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEmptySlot(MultiplayerLobbyInterface lobby)
    {
        var slots = lobby._playerSlots;
        for (var index = 0; slots != null && index < slots.Count; index++)
        {
            if (slots[index] != null && slots[index].IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private void Refresh(MultiplayerLobbyInterface lobby)
    {
        if (_hidden.Count == 0)
        {
            if (_label != null && !_label.WasCollected)
            {
                _label.gameObject.SetActive(false);
            }

            return;
        }

        var label = Label(lobby);
        if (label == null)
        {
            return;
        }

        label.text = _texts.Plural("lobby.more", _hidden.Count, _hidden.Count);
        label.gameObject.SetActive(true);
    }

    private TMP_Text Label(MultiplayerLobbyInterface lobby)
    {
        if (_label != null && !_label.WasCollected)
        {
            return _label;
        }

        var row = lobby.ConnectedPlayerParent;
        var template = lobby.WaitingForHostText;
        if (row == null || template == null)
        {
            _log.Warning("[Lobby] The lobby has no player row or text to copy, the count of more players is not shown");
            return null;
        }

        var rowRect = row.GetComponent<RectTransform>();
        var copy = UnityEngine.Object.Instantiate(template.gameObject, rowRect.parent, false);
        copy.name = LabelName;
        _label = copy.GetComponent<TMP_Text>();
        _label.enableAutoSizing = false;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        var rect = _label.rectTransform;
        rect.anchorMin = rect.anchorMax = rowRect.anchorMin;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        rowRect.GetWorldCorners(corners);
        rect.position = (corners[2] + corners[3]) * 0.5f;
        rect.anchoredPosition += new Vector2(Gap, 0f);
        return _label;
    }
}
