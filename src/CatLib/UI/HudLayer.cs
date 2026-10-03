using System;
using CatLib.Logging;
using TMPro;
using UnityEngine;

namespace CatLib.UI;

public static class HudLayer
{
    public const string ObjectName = "CatLib_Hud";
    public const int RecheckFrames = 30;

    private static CatLogger _log;
    private static GameObject _root;
    private static RectTransform _rect;
    private static CanvasGroup _group;
    private static IntPtr _managerPointer;
    private static TMP_FontAsset _font;
    private static Material _fontMaterial;
    private static int _countdown;

    public static bool IsAvailable => UiClone.IsAlive(_root);

    public static RectTransform Root => IsAvailable ? _rect : null;

    public static bool IsGameMenuOpen { get; private set; }

    public static float Alpha => IsAvailable ? _group.alpha : 0f;

    public static TMP_FontAsset Font => IsAvailable ? _font : null;

    public static event Action<RectTransform> Created;

    internal static void Initialize(CatLogger log) => _log = log;

    internal static void Update()
    {
        if (--_countdown <= 0)
        {
            _countdown = RecheckFrames;
            Track();
        }

        if (!IsAvailable || !Singleton<InterfaceManager>.HasInstance())
        {
            return;
        }

        try
        {
            var manager = Singleton<InterfaceManager>.Instance;
            IsGameMenuOpen = IsMenuOpen(manager);
            var alpha = IsGameMenuOpen ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, alpha, Time.unscaledDeltaTime * 8f);
        }
        catch (Exception exception)
        {
            _log?.Debug($"Reading the game menus failed: {exception.Message}");
        }
    }

    public static TMP_Text CreateText(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name);
        textObject.layer = parent.gameObject.layer;
        var rect = textObject.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
        {
            text.font = _font;
            if (_fontMaterial != null)
            {
                text.fontSharedMaterial = _fontMaterial;
            }
        }

        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private static void Track()
    {
        if (!Singleton<InterfaceManager>.HasInstance())
        {
            return;
        }

        var manager = Singleton<InterfaceManager>.Instance;
        if (IsAvailable && manager.Pointer == _managerPointer)
        {
            return;
        }

        try
        {
            Create(manager);
        }
        catch (Exception exception)
        {
            _log?.Error("Creating the HUD layer failed", exception);
            _root = null;
        }
    }

    private static void Create(InterfaceManager manager)
    {
        var canvas = RootCanvas(manager.NotificationInterface) ?? RootCanvas(manager.PauseInterface) ?? RootCanvas(manager);
        if (canvas == null)
        {
            _log?.Warning("The game's HUD canvas was not found, the HUD layer waits for the next level");
            return;
        }

        FindFont(manager);
        _managerPointer = manager.Pointer;
        _root = new GameObject(ObjectName);
        _root.layer = canvas.gameObject.layer;
        _rect = _root.AddComponent<RectTransform>();
        _rect.SetParent(canvas.transform, false);
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
        _group = _root.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;
        _rect.SetAsLastSibling();
        _log?.Info($"HUD layer added to {canvas.name}, font {(_font == null ? "not found" : _font.name)}");
        Created?.Invoke(_rect);
    }

    private static Canvas RootCanvas(Component component)
    {
        var canvas = component == null ? null : component.GetComponentInParent<Canvas>(true);
        return canvas == null ? null : canvas.rootCanvas;
    }

    private static void FindFont(InterfaceManager manager)
    {
        if (_font != null && !_font.WasCollected)
        {
            return;
        }

        foreach (var text in manager.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.font != null)
            {
                _font = text.font;
                _fontMaterial = text.fontSharedMaterial;
                return;
            }
        }
    }

    private static bool IsMenuOpen(InterfaceManager manager) =>
        Shown(manager.PauseInterface) || Shown(manager.SettingsInterface) || Shown(manager.RecapScreenInterface) ||
        Shown(manager.CheatInterface) || Shown(manager.SpawnerInterface) || Shown(manager.CreditsInterface) || Shown(manager.DemoEndInterface);

    private static bool Shown(InterfaceBase ui) => ui != null && ui.IsShown;
}
