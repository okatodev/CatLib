using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
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
    public const float PaddingX = 14f;
    public const float PaddingY = 8f;
    public const int RecheckFrames = 30;
    public const int CornerRadius = 12;
    public const float PulseSeconds = 4f;
    public const float PulsesPerSecond = 1.2f;
    public const float MaxObstacleShare = 0.5f;

    private static readonly Color Ink = new(0.325f, 0.247f, 0.2f, 1f);
    private static readonly Color QuietInk = new(0.325f, 0.247f, 0.2f, 0.72f);
    private static readonly Color WarningPlate = new(1f, 0.87f, 0.62f, 0.96f);
    private static readonly Color WarningGlow = new(1f, 0.72f, 0.42f, 1f);
    private static readonly Color CalmPlate = new(1f, 0.96f, 0.88f, 0.92f);
    private static readonly Color QuietPlate = new(1f, 0.96f, 0.88f, 0.55f);

    private static CatLogger _log;
    private static InterfaceBase _menu;
    private static IntPtr _menuPointer;
    private static GameObject _root;
    private static RectTransform _rootRect;
    private static RectTransform _textRect;
    private static Image _plate;
    private static TMP_Text _text;
    private static CanvasGroup _group;
    private static Canvas _canvas;
    private static Sprite _plateSprite;
    private static string _rendered;
    private static Vector2 _baseSize;
    private static float _lift;
    private static float _shownSince;
    private static bool _visible;
    private static bool _hovered;
    private static int _countdown;
    private static int _modCount = -1;
    private static GameBuildStatus? _pulsedStatus;
    private static (GameBuildCheck Check, bool Hovered, string Language) _state;

    public static Func<Vector3?> PointerOverride { get; set; }

    public static GameBuildCheck PreviewCheck { get; set; }

    public static GameObject Root => UiClone.IsAlive(_root) ? _root : null;

    public static TMP_Text Label => UiClone.IsAlive(_root) ? _text : null;

    public static bool IsHovered => _hovered;

    public static bool IsPlateShown => UiClone.IsAlive(_root) && _plate.enabled;

    public static bool IsPlateWarning => UiClone.IsAlive(_root) && _plate.color.g < QuietPlate.g - 0.05f;

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

        _plate = _root.AddComponent<Image>();
        _plate.sprite = PlateSprite();
        _plate.type = Image.Type.Sliced;
        _plate.raycastTarget = false;
        _plate.enabled = false;

        var textObject = new GameObject("text_CatLibVersion");
        textObject.layer = _root.layer;
        _textRect = textObject.AddComponent<RectTransform>();
        _textRect.SetParent(_rootRect, false);
        _textRect.anchorMin = new Vector2(1f, 0f);
        _textRect.anchorMax = new Vector2(1f, 0f);
        _textRect.pivot = new Vector2(1f, 0f);
        _textRect.anchoredPosition = new Vector2(-PaddingX, PaddingY);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.fontSize = FontSize;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        text.color = QuietInk;
        _text = text;

        _rendered = null;
        _state = default;
        _baseSize = Vector2.zero;
        _lift = 0f;
        _visible = false;
        _hovered = false;
        _rootRect.SetAsLastSibling();
        var check = Check;
        _log?.Info($"Version badge added to the main menu ({canvas.name}), the game build check says {(check == null ? "not checked yet" : check.Status.ToString())}");
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
        if (check?.Status != _pulsedStatus)
        {
            _pulsedStatus = check?.Status;
            _shownSince = Time.unscaledTime;
        }

        _hovered = IsPointerOver();
        var language = UiText.LanguageCode;
        var state = (check, _hovered, language);
        if (recheck || !state.Equals(_state))
        {
            _state = state;
            var content = VersionBadgeText.Compose(check, _hovered, ModCount(), language);
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

        Pulse(check);
    }

    private static void Render(string content, GameBuildCheck check)
    {
        _rendered = content;
        _text.text = content;
        var warning = check != null && check.IsWarning;
        _text.color = warning || _hovered ? Ink : QuietInk;
        var size = _text.GetPreferredValues(content);
        _textRect.sizeDelta = size;
        _rootRect.sizeDelta = new Vector2(size.x + PaddingX * 2f, size.y + PaddingY * 2f);
        if (!_hovered)
        {
            _baseSize = _rootRect.sizeDelta;
        }

        _plate.enabled = true;
        _plate.color = warning ? WarningPlate : _hovered ? CalmPlate : QuietPlate;
    }

    private static void Pulse(GameBuildCheck check)
    {
        if (check == null || !check.IsWarning)
        {
            return;
        }

        var elapsed = Time.unscaledTime - _shownSince;
        if (elapsed > PulseSeconds)
        {
            _plate.color = WarningPlate;
            return;
        }

        var wave = 0.5f - 0.5f * Mathf.Cos(elapsed * PulsesPerSecond * Mathf.PI * 2f);
        var fade = 1f - elapsed / PulseSeconds;
        _plate.color = Color.Lerp(WarningPlate, WarningGlow, wave * fade);
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

    private static int ModCount()
    {
        if (_modCount < 0)
        {
            _modCount = CountMods();
        }

        return _modCount;
    }

    private static int CountMods()
    {
        try
        {
            var count = 0;
            foreach (var plugin in IL2CPPChainloader.Instance.Plugins.Values)
            {
                if (plugin.Metadata.GUID != PluginMeta.Guid)
                {
                    count++;
                }
            }

            return count;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static Sprite PlateSprite()
    {
        if (_plateSprite != null && !_plateSprite.WasCollected)
        {
            return _plateSprite;
        }

        var size = CornerRadius * 2 + 4;
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = Math.Max(Math.Max(CornerRadius - (x + 0.5f), x + 0.5f - (size - CornerRadius)), 0f);
                var dy = Math.Max(Math.Max(CornerRadius - (y + 0.5f), y + 0.5f - (size - CornerRadius)), 0f);
                var coverage = Mathf.Clamp01(CornerRadius - (float)Math.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Math.Round(coverage * 255f));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels), 0);
        texture.Apply(false, false);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var border = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);
        _plateSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _plateSprite.name = "CatLib version plate";
        _plateSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return _plateSprite;
    }
}
