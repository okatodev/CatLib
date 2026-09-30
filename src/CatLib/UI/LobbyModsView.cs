using System;
using CatLib.Logging;
using CatLib.Net;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class LobbyModsView
{
    public const string ObjectName = "CatLib_LobbyMods";
    public static readonly Vector2 Offset = new(0f, -44f);

    private static FoldoutList _list;
    private static IntPtr _lobby;
    private static CatLogger _log;
    private static bool _failed;

    public static FoldoutList List => _list;

    internal static void Initialize(CatLogger log) => _log = log;

    internal static void Update()
    {
        if (_failed)
        {
            return;
        }

        try
        {
            var lobby = MenuNotices.Lobby();
            if (lobby == null || !lobby.gameObject.activeInHierarchy)
            {
                return;
            }

            var roster = CatNetwork.Roster;
            if (_list == null || !_list.IsAlive || _lobby != lobby.Pointer)
            {
                if (roster == null)
                {
                    return;
                }

                Create(lobby);
            }

            _list.Root.SetActive(roster != null);
            if (roster == null)
            {
                return;
            }

            var localId = SessionNetwork.Transport?.LocalId ?? 0;
            _list.Show(LobbyRosterText.Build(roster, localId, SessionNetwork.Host != null, UiText.LanguageCode, TogglePolicy));
        }
        catch (Exception exception)
        {
            _failed = true;
            _log?.Error("The mods list in the lobby failed and is turned off until the game restarts", exception);
        }
    }

    internal static void Forget()
    {
        _list?.Destroy();
        _list = null;
        _lobby = IntPtr.Zero;
    }

    private static void Create(MultiplayerLobbyInterface lobby)
    {
        Forget();
        var old = lobby.transform.Find(ObjectName);
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        var style = StyleFrom(lobby);
        _list = FoldoutList.Create(lobby.transform, style, ObjectName, Offset, _log);
        _lobby = lobby.Pointer;
        _log?.Info("Added the mods list to the lobby");
    }

    private static FoldoutStyle StyleFrom(MultiplayerLobbyInterface lobby)
    {
        var slots = lobby._playerSlots;
        var slot = slots != null && slots.Count > 0 ? slots[0] : null;
        var text = slot == null ? null : slot.PlayerNameText;
        if (text == null)
        {
            throw new InvalidOperationException("The lobby has no player name text to copy");
        }

        return new FoldoutStyle
        {
            TextTemplate = text,
            HeaderSprite = SpriteAt(lobby.InviteButton == null ? null : lobby.InviteButton.transform, "img_Button_Background"),
            TapeSprite = SpriteAt(lobby.InviteButton == null ? null : lobby.InviteButton.transform, "img_Button_Background/img_Scotch"),
            ArrowSprite = SpriteAt(lobby.BackButton == null ? null : lobby.BackButton.transform, "img_Button_Background/img_Arrow"),
            BodySprite = slot.GetComponent<Image>()?.sprite,
            RowSprite = SpriteAt(lobby.transform, "img_background")
        };
    }

    private static Sprite SpriteAt(Transform root, string path)
    {
        var target = root == null ? null : root.Find(path);
        var image = target == null ? null : target.GetComponent<Image>();
        return image == null ? null : image.sprite;
    }

    private static void TogglePolicy()
    {
        var next = CatNetwork.IncompatiblePlayers == IncompatiblePlayerAction.Warn ? IncompatiblePlayerAction.Disconnect : IncompatiblePlayerAction.Warn;
        CatNetwork.IncompatiblePlayers = next;
        _log?.Info($"Players with other mods: {next}, changed in the lobby");
    }
}
