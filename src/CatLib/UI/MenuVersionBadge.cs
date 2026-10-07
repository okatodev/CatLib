using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Config;
using CatLib.Game;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal static class MenuVersionBadge
{
    public const string ObjectName = "CatLib_VersionBadge";
    public const float Margin = 20f;
    public const float Gap = 12f;
    public const float FontSize = 24f;
    public const float ModsFontSize = 16f;
    public const float PaddingX = 18f;
    public const float PaddingY = 12f;
    public const float IconSize = 34f;
    public const float IconGap = 10f;
    public const float LineGap = 6f;
    public const float ModsWidth = 430f;
    public const float StampRoom = 40f;
    public const float StampSize = 58f;
    public const float CalmTilt = -2f;
    public const float TapeTilt = 38f;
    public static readonly Vector2 TapeSize = new(76f, 24f);
    public static readonly Vector2 TapeOffset = new(8f, -7f);
    public const float PaperSlice = 0.2f;
    public const float PaperBorder = 22f;
    public const float Smoothing = 14f;
    public const float StampDelay = 0.35f;
    public const float StampSeconds = 0.16f;
    public const float StampFrom = 1.8f;
    public const float StampAlpha = 0.85f;
    public const int RecheckFrames = 30;
    public const int CornerRadius = 12;
    public const float MaxObstacleShare = 0.5f;

    private static readonly Color Ink = new(0.325f, 0.247f, 0.2f, 1f);
    private static readonly Color QuietInk = new(0.325f, 0.247f, 0.2f, 0.78f);
    private static readonly Color MutedInk = new(0.325f, 0.247f, 0.2f, 0.66f);
    private static readonly Color StampInk = new(0.753f, 0.275f, 0.169f, 1f);
    private static readonly Color Paper = new(1f, 1f, 1f, 1f);
    private static readonly Color QuietPaper = new(1f, 1f, 1f, 0.86f);
    private static readonly Color WarmPaper = new(1f, 0.9f, 0.76f, 1f);
    private static readonly Color PlainPaper = new(1f, 0.96f, 0.88f, 0.94f);

    private static CatLogger _log;
    private static InterfaceBase _menu;
    private static IntPtr _menuPointer;
    private static GameObject _root;
    private static RectTransform _rootRect;
    private static RectTransform _card;
    private static RectTransform _textRect;
    private static RectTransform _modsRect;
    private static RectTransform _stamp;
    private static Image _paper;
    private static Image _tape;
    private static Image _icon;
    private static Image _stampRing;
    private static TMP_Text _text;
    private static TMP_Text _mods;
    private static TMP_Text _stampMark;
    private static CanvasGroup _group;
    private static Canvas _canvas;
    private static bool _gamePaper;
    private static string _rendered;
    private static Vector2 _baseSize;
    private static Vector2 _size;
    private static Vector2 _targetSize;
    private static float _tilt = CalmTilt;
    private static float _lift;
    private static float _shownSince;
    private static bool _visible;
    private static bool _hovered;
    private static bool _warning;
    private static int _countdown;
    private static List<string> _modNames;
    private static GameBuildStatus? _stampedStatus;
    private static (GameBuildCheck Check, bool Hovered, string Language) _state;

    public static Func<Vector3?> PointerOverride { get; set; }

    public static GameBuildCheck PreviewCheck { get; set; }

    public static GameObject Root => UiClone.IsAlive(_root) ? _root : null;

    public static TMP_Text Label => UiClone.IsAlive(_root) ? _text : null;

    public static TMP_Text ModsLabel => UiClone.IsAlive(_root) ? _mods : null;

    public static bool IsHovered => _hovered;

    public static bool IsPlateShown => UiClone.IsAlive(_root) && _paper.enabled;

    public static bool IsPlateWarning => UiClone.IsAlive(_root) && _warning;

    public static bool UsesGamePaper => _gamePaper;

    public static float Lift => _lift;

    public static GameBuildCheck Check => PreviewCheck ?? GameCompatibility.TryCheck();

    internal static void Initialize(CatLogger log) => _log = log;

    internal static void Update()
    {
        var recheck = false;
        if (--_countdown <= 0)
        {
            _countdown = RecheckFrames;
            recheck = true;
            Track();
        }

        if (!UiClone.IsAlive(_root) || !UiClone.IsAlive(_menu))
        {
            return;
        }

        try
        {
            Refresh(recheck);
        }
        catch (Exception exception)
        {
            _log?.Error("Updating the version badge failed, it is removed", exception);
            Destroy();
        }
    }

    internal static void Rebuild()
    {
        _rendered = null;
        _state = default;
        _countdown = 0;
    }

    internal static void Destroy()
    {
        if (UiClone.IsAlive(_root))
        {
            UnityEngine.Object.Destroy(_root);
        }

        _root = null;
        _menu = null;
        _menuPointer = IntPtr.Zero;
        _rendered = null;
    }

    private static void Track()
    {
        var menu = FindMenu();
        if (menu == null)
        {
            return;
        }

        if (UiClone.IsAlive(_root) && menu.Pointer == _menuPointer)
        {
            _menu = menu;
            return;
        }

        Destroy();
        try
        {
            Create(menu);
        }
        catch (Exception exception)
        {
            _log?.Error("Creating the version badge failed", exception);
            Destroy();
        }
    }

    private static InterfaceBase FindMenu()
    {
        if (!Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            return null;
        }

        var menu = Singleton<MainMenuInterfacesManager>.Instance.MainMenuInterface;
        return UiClone.IsAlive(menu) ? menu : null;
    }

    private static void Create(InterfaceBase menu)
    {
        var canvas = menu.GetComponentInParent<Canvas>();
        canvas = canvas == null ? null : canvas.rootCanvas;
        var source = FontSource(menu);
        if (canvas == null || source == null)
        {
            return;
        }

        _menu = menu;
        _menuPointer = menu.Pointer;
        _canvas = canvas;

        _root = new GameObject(ObjectName);
        _root.layer = canvas.gameObject.layer;
        _rootRect = _root.AddComponent<RectTransform>();
        _rootRect.SetParent(canvas.transform, false);
        _rootRect.anchorMin = new Vector2(1f, 0f);
        _rootRect.anchorMax = new Vector2(1f, 0f);
        _rootRect.pivot = new Vector2(1f, 0f);
        _rootRect.anchoredPosition = new Vector2(-Margin, Margin);
        _group = _root.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;

        _card = Child("card", _rootRect, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        _paper = _card.gameObject.AddComponent<Image>();
        _paper.raycastTarget = false;
        _paper.enabled = false;
        var (paper, tape) = GameSprites();
        _gamePaper = paper != null;
        var sliced = UiSprites.Sliced(paper ?? UiSprites.RoundedPlate(CornerRadius), _gamePaper ? PaperSlice : 0f);
        _paper.sprite = sliced;
        _paper.type = sliced != null && sliced.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        if (_gamePaper)
        {
            _paper.pixelsPerUnitMultiplier = UiSprites.BorderMultiplier(sliced, PaperBorder);
        }

        var tapeRect = Child("tape", _card, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), TapeOffset, TapeSize);
        tapeRect.localEulerAngles = new Vector3(0f, 0f, TapeTilt);
        _tape = tapeRect.gameObject.AddComponent<Image>();
        _tape.sprite = tape;
        _tape.raycastTarget = false;
        _tape.enabled = tape != null;

        var iconRect = Child("icon", _card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PaddingX, -PaddingY), new Vector2(IconSize, IconSize));
        _icon = iconRect.gameObject.AddComponent<Image>();
        _icon.raycastTarget = false;
        _icon.preserveAspect = true;
        var icon = OwnIcon(out var iconScale);
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        iconRect.sizeDelta = new Vector2(IconSize, IconSize) * iconScale;
        iconRect.anchoredPosition = new Vector2(PaddingX - (iconRect.sizeDelta.x - IconSize) / 2f, -PaddingY + (iconRect.sizeDelta.y - IconSize) / 2f);

        _text = Text("text_CatLibVersion", source, FontSize, TextAlignmentOptions.TopLeft);
        _textRect = _text.rectTransform;
        _mods = Text("text_CatLibMods", source, ModsFontSize, TextAlignmentOptions.TopLeft);
        _mods.textWrappingMode = TextWrappingModes.Normal;
        _mods.color = MutedInk;
        _modsRect = _mods.rectTransform;
        _mods.gameObject.SetActive(false);

        _stamp = Child("stamp", _card, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-StampSize * 0.62f, -StampSize * 0.5f), new Vector2(StampSize, StampSize));
        _stamp.localEulerAngles = new Vector3(0f, 0f, 14f);
        _stampRing = _stamp.gameObject.AddComponent<Image>();
        _stampRing.sprite = UiSprites.InkRing(64, 9);
        _stampRing.color = StampInk;
        _stampRing.raycastTarget = false;
        _stampMark = Text("mark", source, StampSize * 0.62f, TextAlignmentOptions.Center, _stamp);
        _stampMark.rectTransform.anchorMin = Vector2.zero;
        _stampMark.rectTransform.anchorMax = Vector2.one;
        _stampMark.rectTransform.offsetMin = Vector2.zero;
        _stampMark.rectTransform.offsetMax = new Vector2(0f, -2f);
        _stampMark.text = "<b>!</b>";
        _stampMark.color = StampInk;
        _stamp.gameObject.SetActive(false);

        _rendered = null;
        _state = default;
        _baseSize = Vector2.zero;
        _size = Vector2.zero;
        _targetSize = Vector2.zero;
        _tilt = CalmTilt;
        _lift = 0f;
        _visible = false;
        _hovered = false;
        _warning = false;
        _stampedStatus = null;
        _rootRect.SetAsLastSibling();
        var check = Check;
        _log?.Info($"Version badge added to the main menu ({canvas.name}) on {(_gamePaper ? "the game's paper" : "a plain plate")}{(tape == null ? string.Empty : " with tape")}, " +
                   $"the game build check says {(check == null ? "not checked yet" : check.Status.ToString())}");
    }

    private static (Sprite Paper, Sprite Tape) GameSprites()
    {
        try
        {
            var lobby = MenuNotices.Lobby();
            if (lobby == null)
            {
                return (null, null);
            }

            var slots = lobby._playerSlots;
            var slot = slots != null && slots.Count > 0 ? slots[0] : null;
            var paper = slot == null ? null : slot.GetComponent<Image>()?.sprite;
            var tapeTarget = lobby.InviteButton == null ? null : lobby.InviteButton.transform.Find("img_Button_Background/img_Scotch");
            var tape = tapeTarget == null ? null : tapeTarget.GetComponent<Image>()?.sprite;
            return (paper, tape);
        }
        catch (Exception exception)
        {
            _log?.Debug($"The lobby paper for the version badge is not reachable, a plain plate is used: {exception.Message}");
            return (null, null);
        }
    }

    private static Sprite OwnIcon(out float scale)
    {
        scale = 1f;
        var settings = CatConfig.All.FirstOrDefault(entry => entry.OwnerId == PluginMeta.Guid);
        return settings == null ? null : ModIcons.Trimmed(settings, out scale);
    }

    private static RectTransform Child(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.layer = parent.gameObject.layer;
        var rect = gameObject.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(string name, TMP_Text source, float size, TextAlignmentOptions alignment, Transform parent = null)
    {
        var rect = Child(name, parent ?? _card, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        text.color = QuietInk;
        return text;
    }

    private static TMP_Text FontSource(InterfaceBase menu)
    {
        var mainMenu = menu.TryCast<MainMenuInterface>();
        var button = mainMenu == null ? null : mainMenu.SettingsButton;
        var label = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.font != null)
        {
            return label;
        }

        foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.font != null)
            {
                return text;
            }
        }

        return null;
    }

    private static void Refresh(bool recheck)
    {
        var alpha = MenuAlpha();
        var visible = alpha > 0.01f;
        if (visible && !_visible)
        {
            _shownSince = Time.unscaledTime;
            recheck = true;
        }

        _visible = visible;
        _group.alpha = alpha;
        if (!visible)
        {
            _hovered = false;
            return;
        }

        var check = Check;
        if (check?.Status != _stampedStatus)
        {
            _stampedStatus = check?.Status;
            _shownSince = Time.unscaledTime;
        }

        _hovered = IsPointerOver();
        var language = UiText.LanguageCode;
        var state = (check, _hovered, language);
        if (recheck || !state.Equals(_state))
        {
            _state = state;
            var content = VersionBadgeText.Compose(check, _hovered, ModNames().Count, language);
            if (!string.Equals(content, _rendered, StringComparison.Ordinal))
            {
                Render(content, check);
                recheck |= !_hovered;
            }
        }

        if (recheck)
        {
            Place();
        }

        Animate();
    }

    private static void Render(string content, GameBuildCheck check)
    {
        _rendered = content;
        _warning = check != null && check.IsWarning;
        _text.text = content;
        _text.color = _warning || _hovered ? Ink : QuietInk;
        var textSize = _text.GetPreferredValues(content);
        _textRect.sizeDelta = textSize;
        var left = PaddingX + (_icon.enabled ? IconSize + IconGap : 0f);
        _textRect.anchoredPosition = new Vector2(left, -PaddingY + 2f);

        var width = left + textSize.x;
        var height = Mathf.Max(_icon.enabled ? IconSize : 0f, textSize.y);
        var names = VersionBadgeText.ModList(ModNames());
        var showMods = _hovered && names.Length > 0;
        _mods.gameObject.SetActive(showMods);
        if (showMods)
        {
            _mods.text = names;
            var modsSize = _mods.GetPreferredValues(names, ModsWidth, 0f);
            modsSize.x = Mathf.Min(modsSize.x, ModsWidth);
            _modsRect.sizeDelta = new Vector2(Mathf.Max(modsSize.x, textSize.x), modsSize.y);
            _modsRect.anchoredPosition = new Vector2(left, -PaddingY - height - LineGap);
            width = Mathf.Max(width, left + modsSize.x);
            height += LineGap + modsSize.y;
        }

        var target = new Vector2(width + PaddingX + (_warning ? StampRoom : 0f), height + PaddingY * 2f);
        _targetSize = target;
        if (_size == Vector2.zero)
        {
            _size = target;
        }

        if (!_hovered)
        {
            _baseSize = target;
        }

        _paper.enabled = true;
        _paper.color = _warning ? WarmPaper : !_gamePaper ? PlainPaper : _hovered ? Paper : QuietPaper;
        _stamp.gameObject.SetActive(_warning);
        ApplySize();
    }

    private static void Animate()
    {
        var step = 1f - Mathf.Exp(-Time.unscaledDeltaTime * Smoothing);
        _size = Vector2.Lerp(_size, _targetSize, step);
        if ((_size - _targetSize).sqrMagnitude < 0.25f)
        {
            _size = _targetSize;
        }

        _tilt = Mathf.Lerp(_tilt, _hovered ? 0f : CalmTilt, step);
        _card.localEulerAngles = new Vector3(0f, 0f, _tilt);
        ApplySize();
        if (!_warning)
        {
            return;
        }

        var progress = Mathf.Clamp01((Time.unscaledTime - _shownSince - StampDelay) / StampSeconds);
        var eased = 1f - (1f - progress) * (1f - progress);
        _stamp.localScale = Vector3.one * Mathf.Lerp(StampFrom, 1f, eased);
        var ink = StampInk;
        ink.a = StampAlpha * progress;
        _stampRing.color = ink;
        _stampMark.color = ink;
    }

    private static void ApplySize()
    {
        _rootRect.sizeDelta = _size;
        _card.sizeDelta = _size;
    }

    private static float MenuAlpha()
    {
        if (!_menu.gameObject.activeInHierarchy || !_menu.IsShown && _menu._canvasGroup == null)
        {
            return 0f;
        }

        var group = _menu._canvasGroup;
        return group == null ? 1f : group.alpha;
    }

    private static bool IsPointerOver()
    {
        var pointer = PointerOverride?.Invoke();
        if (pointer == null)
        {
            var input = UnityInput.Current;
            if (!input.mousePresent)
            {
                return false;
            }

            pointer = input.mousePosition;
        }

        var camera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(_rootRect, new Vector2(pointer.Value.x, pointer.Value.y), camera);
    }

    private static void Place()
    {
        var canvasRect = _canvas.transform.TryCast<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        var area = canvasRect.rect;
        var obstacles = Obstacles(canvasRect, area);
        var size = _baseSize == Vector2.zero ? _rootRect.sizeDelta : _baseSize;
        var bottom = Margin;
        string blocker = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var mine = new Rect(area.xMax - Margin - size.x, area.yMin + bottom, size.x, size.y);
            var hit = false;
            foreach (var obstacle in obstacles)
            {
                if (obstacle.Rect.Overlaps(mine))
                {
                    bottom = obstacle.Rect.yMax - area.yMin + Gap;
                    blocker = obstacle.Name;
                    hit = true;
                    break;
                }
            }

            if (!hit)
            {
                break;
            }
        }

        var lift = bottom - Margin;
        if (Math.Abs(lift - _lift) > 0.5f)
        {
            _log?.Info(lift > 0f
                ? $"Version badge moved up by {lift:0} to keep clear of {blocker}"
                : "Version badge is back in the corner");
        }

        _lift = lift;
        _rootRect.anchoredPosition = new Vector2(-Margin, Margin + _lift);
    }

    private static List<(string Name, Rect Rect)> Obstacles(RectTransform canvasRect, Rect area)
    {
        var found = new List<(string Name, Rect Rect)>();
        foreach (var selectable in _menu.GetComponentsInChildren<Selectable>(false))
        {
            AddObstacle(found, selectable == null ? null : selectable.transform.TryCast<RectTransform>(), canvasRect, area);
        }

        foreach (var text in _menu.GetComponentsInChildren<TMP_Text>(false))
        {
            if (text != null && text.enabled && !string.IsNullOrWhiteSpace(text.text))
            {
                AddObstacle(found, text.rectTransform, canvasRect, area);
            }
        }

        return found;
    }

    private static void AddObstacle(List<(string Name, Rect Rect)> found, RectTransform rect, RectTransform canvasRect, Rect area)
    {
        if (rect == null || rect.IsChildOf(_rootRect))
        {
            return;
        }

        var corners = new Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        var min = canvasRect.InverseTransformPoint(corners[0]);
        var max = canvasRect.InverseTransformPoint(corners[2]);
        var bounds = Rect.MinMaxRect(Math.Min(min.x, max.x), Math.Min(min.y, max.y), Math.Max(min.x, max.x), Math.Max(min.y, max.y));
        if (bounds.width <= 0f || bounds.height <= 0f || bounds.width > area.width * MaxObstacleShare || bounds.height > area.height * MaxObstacleShare)
        {
            return;
        }

        found.Add((rect.name, bounds));
    }

    private static IReadOnlyList<string> ModNames()
    {
        if (_modNames == null)
        {
            _modNames = ReadModNames();
        }

        return _modNames;
    }

    private static List<string> ReadModNames()
    {
        try
        {
            return IL2CPPChainloader.Instance.Plugins.Values
                .Where(plugin => plugin.Metadata.GUID != PluginMeta.Guid)
                .Select(plugin => plugin.Metadata.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }
}
