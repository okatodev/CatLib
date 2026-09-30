using System;
using System.Collections.Generic;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatLib.UI;

public sealed class FoldoutList
{
    public const float ArrowCollapsed = 180f;
    public const float ArrowExpanded = 90f;
    public const float TitleInset = 64f;
    public const float SummaryInset = 22f;
    public const float HeaderGap = 18f;
    public const float MaxTitleShare = 0.6f;
    public const float ReferencePixelsPerUnit = 100f;

    private static readonly Dictionary<IntPtr, Sprite> SlicedCache = new();

    private readonly FoldoutStyle _style;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly CatLogger _log;
    private RectTransform _header;
    private RectTransform _arrow;
    private TMP_Text _title;
    private TMP_Text _summary;
    private RectTransform _body;
    private RectTransform _content;
    private ScrollRect _scroll;
    private FoldoutContent _shown = FoldoutContent.Empty;
    private string _signature;
    private bool _expanded;

    private FoldoutList(FoldoutStyle style, CatLogger log)
    {
        _style = style;
        _log = log;
    }

    public event Action<bool> ExpandedChanged;

    public GameObject Root { get; private set; }

    public bool IsAlive => Root != null && !Root.WasCollected;

    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }

            _expanded = value;
            ApplyExpanded();
            Events.SafeInvoker.Invoke(ExpandedChanged, value, "FoldoutList.ExpandedChanged", _log);
        }
    }

    public static FoldoutList Create(Transform parent, FoldoutStyle style, string name, Vector2 topCenterOffset, CatLogger log = null)
    {
        if (parent == null || style == null || style.TextTemplate == null)
        {
            throw new ArgumentException("A parent and a style with a text template are required");
        }

        var list = new FoldoutList(style, log);
        list.Build(parent, name, topCenterOffset);
        return list;
    }

    public void Show(FoldoutContent content)
    {
        content ??= FoldoutContent.Empty;
        var signature = content.Signature();
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        _shown = content;
        SetText(_title, content.Title, _style.Text);
        SetText(_summary, content.Summary, _style.ToneColor(content.SummaryTone));
        FitHeader();
        RebuildBody();
    }

    public void Destroy()
    {
        if (IsAlive)
        {
            Object.Destroy(Root);
        }

        Root = null;
    }

    private void Build(Transform parent, string name, Vector2 offset)
    {
        Root = new GameObject(name);
        var root = Root.AddComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = offset;
        root.sizeDelta = new Vector2(_style.HeaderWidth, _style.HeaderHeight);
        root.SetAsLastSibling();

        _header = Rect("header", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(_style.HeaderWidth, _style.HeaderHeight));
        var headerImage = Paper(_header.gameObject, _style.HeaderSprite, _style.HeaderReferenceWidth);
        Click(_header.gameObject, headerImage, () => Expanded = !Expanded);

        if (_style.TapeSprite != null)
        {
            var tape = Rect("tape", _header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(150f, 32f));
            tape.localEulerAngles = new Vector3(0f, 0f, 2.5f);
            Picture(tape.gameObject, _style.TapeSprite, Color.white).raycastTarget = false;
        }

        _arrow = Rect("arrow", _header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38f, -2f), new Vector2(34f, 32f));
        if (_style.ArrowSprite != null)
        {
            Picture(_arrow.gameObject, _style.ArrowSprite, _style.Text).raycastTarget = false;
        }

        _title = Text(_header, "title", _style.TitleSize, TextAlignmentOptions.Left);
        Place(_title, 0f, 0.45f, 64f, 0f);
        _summary = Text(_header, "summary", _style.SummarySize, TextAlignmentOptions.Right);
        Place(_summary, 0.45f, 1f, 0f, SummaryInset);
        _summary.enableAutoSizing = true;
        _summary.fontSizeMax = _style.SummarySize;
        _summary.fontSizeMin = _style.SummarySize * 0.75f;

        _body = Rect("body", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(_style.HeaderHeight + _style.BodyGap)),
            new Vector2(_style.BodyWidth, 100f));
        Paper(_body.gameObject, _style.BodySprite, _style.BodyReferenceWidth);
        var viewport = Rect("viewport", _body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-_style.Padding * 2f, -_style.Padding * 1.4f));
        viewport.gameObject.AddComponent<RectMask2D>();
        _content = Rect("content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
        _scroll = _body.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;
        _scroll.inertia = false;
        ApplyExpanded();
    }

    private void FitHeader()
    {
        var available = _style.HeaderWidth - TitleInset - SummaryInset;
        var titleWidth = Mathf.Min(_title.GetPreferredValues(_title.text).x + 6f, available * MaxTitleShare);
        var titleRect = _title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.offsetMin = new Vector2(TitleInset, 0f);
        titleRect.offsetMax = new Vector2(TitleInset + titleWidth, 0f);
        Place(_summary, 0f, 1f, TitleInset + titleWidth + HeaderGap, SummaryInset);
    }

    private void ApplyExpanded()
    {
        if (!IsAlive)
        {
            return;
        }

        _body.gameObject.SetActive(_expanded);
        _arrow.localEulerAngles = new Vector3(0f, 0f, _expanded ? ArrowExpanded : ArrowCollapsed);
    }

    private void RebuildBody()
    {
        UiClone.DestroyChildren(_content);
        var width = _style.BodyWidth - _style.Padding * 2f;
        var y = 0f;
        foreach (var row in _shown.TopRows)
        {
            y = AddRow(row, y, 0f, width);
        }

        foreach (var section in _shown.Sections)
        {
            y += _style.SectionGap;
            y = AddSection(section, y, width);
            if (_collapsed.Contains(section.Key))
            {
                continue;
            }

            foreach (var row in section.Rows)
            {
                y = AddRow(row, y, _style.RowIndent, width);
            }
        }

        _content.sizeDelta = new Vector2(0f, y);
        var height = Mathf.Min(y + _style.Padding * 1.4f, _style.MaxBodyHeight);
        _body.sizeDelta = new Vector2(_style.BodyWidth, Mathf.Max(height, _style.RowHeight + _style.Padding * 1.4f));
        _content.anchoredPosition = Vector2.zero;
    }

    private float AddSection(FoldoutSection section, float y, float width)
    {
        var rect = Rect("section " + section.Key, _content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(width, _style.SectionHeight));
        var image = Picture(rect.gameObject, _style.RowSprite, _style.SectionTint);
        var key = section.Key;
        Click(rect.gameObject, image, () =>
        {
            if (!_collapsed.Remove(key))
            {
                _collapsed.Add(key);
            }

            Threading.MainThread.Post(() =>
            {
                if (IsAlive)
                {
                    RebuildBody();
                }
            });
        });

        var arrow = Rect("arrow", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(22f, 21f));
        if (_style.ArrowSprite != null)
        {
            Picture(arrow.gameObject, _style.ArrowSprite, _style.Text).raycastTarget = false;
        }

        arrow.localEulerAngles = new Vector3(0f, 0f, _collapsed.Contains(key) ? ArrowCollapsed : ArrowExpanded);
        var title = Text(rect, "title", _style.SectionSize, TextAlignmentOptions.Left);
        Place(title, 0f, 0.45f, 42f, 0f);
        SetText(title, section.Title, _style.Text);
        var detail = Text(rect, "detail", _style.RowSize, TextAlignmentOptions.Center);
        Place(detail, 0.45f, 0.64f, 0f, 0f);
        SetText(detail, section.Detail, _style.Muted);
        var status = Text(rect, "status", _style.RowSize, TextAlignmentOptions.Right);
        Place(status, 0.64f, 1f, 0f, 14f);
        SetText(status, section.Status, _style.ToneColor(section.StatusTone));
        return y + _style.SectionHeight;
    }

    private float AddRow(FoldoutRow row, float y, float indent, float width)
    {
        if (row.OnClick != null && _style.ButtonTemplate != null)
        {
            return AddButtonRow(row, y, indent, width);
        }

        var rect = Rect("row", _content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(indent, -y), new Vector2(width - indent, _style.RowHeight));
        var leftInset = 8f;
        if (row.OnClick != null)
        {
            var image = Picture(rect.gameObject, _style.RowSprite, _style.ClickTint);
            Click(rect.gameObject, image, row.OnClick);
            if (_style.ArrowSprite != null)
            {
                var arrow = Rect("arrow", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(22f, 21f));
                arrow.localEulerAngles = new Vector3(0f, 0f, ArrowCollapsed);
                Picture(arrow.gameObject, _style.ArrowSprite, _style.Text).raycastTarget = false;
                leftInset = 42f;
            }
        }

        var left = Text(rect, "left", _style.RowSize, TextAlignmentOptions.Left);
        Place(left, 0f, 0.46f, leftInset, 0f);
        SetText(left, row.Left, _style.Text);
        var middle = Text(rect, "middle", _style.RowSize, TextAlignmentOptions.Center);
        Place(middle, 0.46f, 0.62f, 0f, 0f);
        SetText(middle, row.Middle, _style.Muted);
        var right = Text(rect, "right", _style.RowSize, TextAlignmentOptions.Right);
        Place(right, 0.62f, 1f, 0f, 14f);
        SetText(right, row.Right, _style.ToneColor(row.Tone));
        return y + _style.RowHeight;
    }

    private float AddButtonRow(FoldoutRow row, float y, float indent, float width)
    {
        var height = _style.ButtonRowHeight;
        var rect = Rect("row", _content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(indent, -y), new Vector2(width - indent, height));
        var left = Text(rect, "left", _style.RowSize, TextAlignmentOptions.Left);
        Place(left, 0f, 0.55f, 8f, 0f);
        SetText(left, row.Left, _style.Text);

        var button = Object.Instantiate(_style.ButtonTemplate, rect, false);
        button.name = "button";
        button.SetActive(true);
        UiClone.StripLocalization(button);
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-6f, 0f);
        buttonRect.sizeDelta = _style.ButtonSize;
        buttonRect.localEulerAngles = Vector3.zero;
        buttonRect.localScale = Vector3.one;
        ShrinkDecorations(button.transform);

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = _style.ButtonTextSize;
            label.fontSizeMin = _style.ButtonTextSize * 0.7f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 2f);
            labelRect.offsetMax = new Vector2(-12f, -2f);
            label.text = (row.Right ?? string.Empty).Replace("<", "\u2039");
        }

        var selectable = button.GetComponent<Button>();
        if (selectable != null)
        {
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
            UiEvents.Listen(selectable.onClick, row.OnClick);
        }

        return y + height;
    }

    private void ShrinkDecorations(Transform button)
    {
        for (var index = 0; index < button.childCount; index++)
        {
            var child = button.GetChild(index);
            if (child.GetComponent<TMP_Text>() != null)
            {
                continue;
            }

            var childRect = child.GetComponent<RectTransform>();
            if (child.name == "img_Button_Background")
            {
                var tape = child.Find("img_Scotch");
                if (tape != null)
                {
                    tape.gameObject.SetActive(false);
                }

                var image = child.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = Sliced(image.sprite, _style.HeaderReferenceWidth);
                    image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                    image.pixelsPerUnitMultiplier = Multiplier(image.sprite, _style.HeaderReferenceWidth);
                }
            }
            else if (childRect != null && childRect.sizeDelta.x > 0f)
            {
                childRect.sizeDelta = childRect.sizeDelta * 0.5f;
                childRect.anchoredPosition = new Vector2(childRect.anchoredPosition.x * 0.5f, -6f);
            }
        }
    }

    private Image Paper(GameObject gameObject, Sprite sprite, float referenceWidth)
    {
        var sliced = Sliced(sprite, referenceWidth);
        var image = Picture(gameObject, sliced, Color.white);
        if (sliced != null && sliced.border != Vector4.zero)
        {
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = Multiplier(sliced, referenceWidth);
        }

        return image;
    }

    private Sprite Sliced(Sprite sprite, float referenceWidth)
    {
        if (sprite == null || referenceWidth <= 0f || sprite.border != Vector4.zero)
        {
            return sprite;
        }

        var key = sprite.Pointer;
        if (SlicedCache.TryGetValue(key, out var cached) && cached != null && !cached.WasCollected)
        {
            return cached;
        }

        try
        {
            var rect = sprite.rect;
            var border = new Vector4(rect.width * _style.SliceShare, rect.height * _style.SliceShare, rect.width * _style.SliceShare, rect.height * _style.SliceShare);
            var created = Sprite.Create(sprite.texture, sprite.textureRect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            created.name = sprite.name + " (CatLib sliced)";
            created.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SlicedCache[key] = created;
            return created;
        }
        catch (Exception exception)
        {
            _log?.Debug($"Slicing {sprite.name} failed, it is stretched instead: {exception.Message}");
            SlicedCache[key] = sprite;
            return sprite;
        }
    }

    private static float Multiplier(Sprite sprite, float referenceWidth)
    {
        if (sprite == null || referenceWidth <= 0f)
        {
            return 1f;
        }

        var nativeUnits = sprite.rect.width / sprite.pixelsPerUnit * ReferencePixelsPerUnit;
        return Mathf.Max(0.01f, nativeUnits / referenceWidth);
    }

    private TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions alignment)
    {
        var instance = Object.Instantiate(_style.TextTemplate.gameObject, parent, false);
        instance.name = name;
        instance.SetActive(true);
        UiClone.DestroyChildren(instance.transform);
        var text = instance.GetComponent<TMP_Text>();
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private static void Place(TMP_Text text, float minX, float maxX, float leftInset, float rightInset)
    {
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(leftInset, 0f);
        rect.offsetMax = new Vector2(-rightInset, 0f);
    }

    private static void SetText(TMP_Text text, string value, Color color)
    {
        if (text == null)
        {
            return;
        }

        var safe = (value ?? string.Empty).Replace("<", "‹");
        if (text.text != safe)
        {
            text.text = safe;
        }

        text.color = color;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var gameObject = new GameObject(name);
        var rect = gameObject.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Picture(GameObject gameObject, Sprite sprite, Color color)
    {
        var image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Simple;
        image.raycastTarget = true;
        return image;
    }

    private static void Click(GameObject gameObject, Graphic target, Action action)
    {
        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        UiEvents.Listen(button.onClick, action);
    }
}
