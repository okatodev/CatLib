using System;
using System.Collections.Generic;
using TMPro;

namespace CatLib.UI;

internal static class MenuNotices
{
    public const string Marker = "\n<size=70%>";
    public const string MarkerEnd = "</size>";
    public const int UpdateIntervalFrames = 10;

    private static readonly Dictionary<ulong, string> PlayerNotices = new();
    private static string _lobbyNotice;
    private static int _countdown;

    public static string LobbyNotice => _lobbyNotice;

    public static void ShowInLobby(string text) => _lobbyNotice = text;

    public static void ClearLobby() => _lobbyNotice = null;

    public static IReadOnlyDictionary<ulong, string> Players => PlayerNotices;

    public static void SetPlayerNotice(ulong steamId, string text) => PlayerNotices[steamId] = text;

    public static void ClearPlayerNotice(ulong steamId) => PlayerNotices.Remove(steamId);

    public static void ClearPlayers() => PlayerNotices.Clear();

    internal static void Update()
    {
        if (ModsMenu.IsSuspended || --_countdown > 0)
        {
            return;
        }

        _countdown = UpdateIntervalFrames;
        try
        {
            Apply(LobbyLabel(), _lobbyNotice);
            ApplyToPlayerCards(Lobby());
        }
        catch (Exception)
        {
            _countdown = UpdateIntervalFrames * 10;
        }
    }

    internal static MultiplayerLobbyInterface Lobby()
    {
        if (!Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            return null;
        }

        var lobby = Singleton<MainMenuInterfacesManager>.Instance.MultiplayerLobbyInterface;
        return lobby == null ? null : lobby.TryCast<MultiplayerLobbyInterface>();
    }

    internal static TMP_Text LobbyLabel()
    {
        var lobby = Lobby();
        return lobby == null ? null : lobby.WaitingForHostText;
    }

    internal static void ApplyToPlayerCards(MultiplayerLobbyInterface lobby)
    {
        var slots = lobby == null ? null : lobby._playerSlots;
        if (slots == null)
        {
            return;
        }

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            if (slot == null)
            {
                continue;
            }

            var notice = !slot.IsEmpty && PlayerNotices.TryGetValue(slot.SteamId, out var text) ? text : null;
            Apply(slot.PlayerNameText, notice);
        }
    }

    internal static bool LeaveLobby()
    {
        var lobby = Lobby();
        if (lobby == null || !lobby.IsShown)
        {
            return false;
        }

        lobby.BackButton_OnClick();
        return true;
    }

    internal static void Apply(TMP_Text label, string notice)
    {
        if (label == null)
        {
            return;
        }

        var text = label.text ?? string.Empty;
        var index = text.IndexOf(Marker, StringComparison.Ordinal);
        var baseText = index >= 0 ? text.Substring(0, index) : text;
        var desired = string.IsNullOrEmpty(notice) ? baseText : baseText + Marker + notice.Replace("<", "\u2039") + MarkerEnd;
        if (text != desired)
        {
            label.text = desired;
        }
    }
}
