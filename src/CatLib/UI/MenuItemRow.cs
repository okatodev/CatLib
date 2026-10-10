using System;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

internal abstract class MenuItemRow
{
    private int _revision = -1;

    protected MenuItemRow(MenuItem item, GameObject root, CatLogger log)
    {
        Item = item;
        Root = root;
        Log = log;
    }

    public MenuItem Item { get; }

    public GameObject Root { get; }

    public virtual Selectable Control => null;

    public float Height { get; protected set; }

    protected CatLogger Log { get; }

    public bool Update(string language)
    {
        if (_revision == Item.Revision)
        {
            return false;
        }

        _revision = Item.Revision;
        Root.SetActive(!Item.IsHidden);
        Refresh(language);
        return true;
    }

    protected abstract void Refresh(string language);
}

internal sealed class GalleryRow : MenuItemRow
{
    public const float TitleHeight = 40f;
    public const float Thumb = 72f;
    public const float Gap = 10f;
    public const float Bottom = 12f;
    public const float EmptyHeight = 34f;
    public const int MaxShown = 60;
    public const float EmptyAlpha = 0.6f;

    private readonly RowTemplates _templates;
    private readonly float _width;
    private readonly TMP_Text _title;
    private readonly TMP_Text _empty;
    private readonly RectTransform _grid;

    public GalleryRow(MenuGallery gallery, RowTemplates templates, Transform parent, float width, CatLogger log)
        : base(gallery, CreateRoot(parent, gallery), log)
    {
        _templates = templates;
        _width = width;
        _title = templates.CreateText(Root.transform, "text_CatLibGalleryTitle");
        Place(_title.rectTransform, 0f, TitleHeight);
        _title.alignment = TextAlignmentOptions.MidlineLeft;
        _title.textWrappingMode = TextWrappingModes.NoWrap;
        _title.overflowMode = TextOverflowModes.Ellipsis;
        _empty = templates.CreateText(Root.transform, "text_CatLibGalleryEmpty");
        Place(_empty.rectTransform, TitleHeight, EmptyHeight);
        _empty.alignment = TextAlignmentOptions.MidlineLeft;
        _empty.fontStyle = FontStyles.Italic;
        var color = _empty.color;
        _empty.color = new Color(color.r, color.g, color.b, color.a * EmptyAlpha);
        var gridObject = new GameObject("group_CatLibGalleryImages");
        _grid = gridObject.AddComponent<RectTransform>();
        _grid.SetParent(Root.transform, false);
        Place(_grid, TitleHeight, 0f);
    }

    public MenuGallery Gallery => (MenuGallery)Item;

    protected override void Refresh(string language)
    {
        _title.text = SettingTexts.ItemLabel(Item, language);
        UiClone.DestroyChildren(_grid);
        var images = Gallery.Images;
        var columns = Math.Max(1, (int)((_width + Gap) / (Thumb + Gap)));
        var shown = Math.Min(images.Count, images.Count > MaxShown ? MaxShown - 1 : MaxShown);
        for (var index = 0; index < shown; index++)
        {
            AddImage(images[index], index, columns);
        }

        if (images.Count > shown)
        {
            var more = _templates.CreateText(_grid, "text_CatLibGalleryMore");
            var rect = more.rectTransform;
            PlaceCell(rect, shown, columns);
            more.text = "+" + (images.Count - shown);
            more.alignment = TextAlignmentOptions.Center;
        }

        var cells = images.Count > shown ? shown + 1 : shown;
        _empty.gameObject.SetActive(cells == 0);
        _empty.text = cells == 0 ? SettingTexts.GalleryEmpty(Gallery, language) : string.Empty;
        var rows = (cells + columns - 1) / columns;
        var gridHeight = rows == 0 ? EmptyHeight : rows * (Thumb + Gap) - Gap;
        _grid.sizeDelta = new Vector2(0f, gridHeight);
        Height = TitleHeight + gridHeight + Bottom;
        RowSizer.Fit(Root, _width, Height);
    }

    private void AddImage(Sprite sprite, int index, int columns)
    {
        var cell = new GameObject("image_CatLibGallery " + index);
        var rect = cell.AddComponent<RectTransform>();
        rect.SetParent(_grid, false);
        PlaceCell(rect, index, columns);
        var image = cell.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void PlaceCell(RectTransform rect, int index, int columns)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(Thumb, Thumb);
        rect.anchoredPosition = new Vector2(index % columns * (Thumb + Gap), -(index / columns) * (Thumb + Gap));
    }

    private static void Place(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    private static GameObject CreateRoot(Transform parent, MenuItem item)
    {
        var root = new GameObject("group_CatLibGallery " + item.Key);
        root.AddComponent<RectTransform>();
        root.transform.SetParent(parent, false);
        return root;
    }
}

internal sealed class ButtonRow : MenuItemRow
{
    public const float Inset = 4f;
    public const float DefaultWidth = 230f;
    public const float DefaultHeight = 36f;
    public const float MinCaptionSize = 12f;
    public const string ScotchName = "img_Button_Background/img_Scotch";

    private static bool _described;

    private readonly Button _button;
    private readonly TMP_Text _caption;
    private readonly TMP_Text _label;

    public ButtonRow(MenuButton button, RowTemplates templates, Selectable fallbackTemplate, Transform parent, float width, CatLogger log)
        : base(button, RowTemplates.Create(templates.Text, parent, "group_CatLibButton " + button.Key), log)
    {
        Height = RowTemplates.RowHeight;
        RowSizer.Fit(Root, width, Height);
        RowGeometry.FitDash(Root);
        _label = Root.transform.Find("panel_Label")?.GetComponentInChildren<TMP_Text>(true);
        var value = Root.transform.Find("panel_Value") ?? Root.transform;
        UiClone.DestroyChildren(value);
        var compact = templates.Button != null;
        var template = compact ? templates.Button : fallbackTemplate.gameObject;
        var clone = UnityEngine.Object.Instantiate(template, value, false);
        clone.name = "button_CatLib " + button.Key;
        UiClone.StripLocalization(clone);
        var rect = clone.transform.TryCast<RectTransform>();
        if (compact)
        {
            PlaceCompact(rect, templates.ButtonSize);
            _caption = clone.transform.Find(RowTemplates.ButtonCaptionName)?.GetComponent<TMP_Text>() ?? clone.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            PlaceStretched(rect);
            clone.transform.Find(ScotchName)?.gameObject.SetActive(false);
            _caption = clone.GetComponentInChildren<TMP_Text>(true);
        }

        if (_caption != null)
        {
            _caption.gameObject.SetActive(true);
            _caption.enabled = true;
            _caption.alpha = 1f;
            _caption.textWrappingMode = TextWrappingModes.NoWrap;
            _caption.overflowMode = TextOverflowModes.Ellipsis;
            var size = _caption.fontSize > 1f ? _caption.fontSize : 18f;
            _caption.enableAutoSizing = true;
            _caption.fontSizeMax = size;
            _caption.fontSizeMin = Math.Min(MinCaptionSize, size);
        }

        clone.SetActive(true);
        _button = clone.GetComponent<Button>();
        if (!_described)
        {
            _described = true;
            Log.Info($"Mods tab buttons copy {template.name} ({(compact ? "a key binding button" : "the reset button")}), caption " +
                     (_caption == null ? "not found" : $"{_caption.name}, font size {_caption.fontSize}, color {_caption.color}, active {_caption.isActiveAndEnabled}"));
        }
        if (_button != null)
        {
            UiClone.DisablePersistentListeners(_button.onClick);
            UiEvents.Listen(_button.onClick, OnClicked);
        }
    }

    private static void PlaceCompact(RectTransform rect, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        var width = size.x > 1f ? size.x : DefaultWidth;
        var height = size.y > 1f ? size.y : DefaultHeight;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
        rect.localEulerAngles = Vector3.zero;
    }

    private static void PlaceStretched(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(Inset, Inset);
        rect.offsetMax = new Vector2(-Inset, -Inset);
    }

    public MenuButton MenuButton => (MenuButton)Item;

    public override Selectable Control => _button;

    protected override void Refresh(string language)
    {
        if (_label != null)
        {
            _label.text = SettingTexts.ItemLabel(Item, language);
        }

        if (_caption != null)
        {
            _caption.text = SettingTexts.ButtonCaption(MenuButton, language);
        }
    }

    private void OnClicked()
    {
        try
        {
            MenuButton.Clicked();
        }
        catch (Exception exception)
        {
            Log.Error($"The button {Item.Id} failed", exception);
        }
    }
}
