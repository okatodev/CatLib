using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

public sealed class CountRow
{
    public CountRow(Sprite icon, string label, string count, bool separated = false, bool muted = false)
    {
        Icon = icon;
        Label = label ?? string.Empty;
        Count = count ?? string.Empty;
        Separated = separated;
        Muted = muted;
    }

    public Sprite Icon { get; }

    public string Label { get; }

    public string Count { get; }

    public bool Separated { get; }

    public bool Muted { get; }
}

public sealed class CountListContent
{
    public CountListContent(CountRow header, IReadOnlyList<CountRow> rows, bool expanded)
    {
        Header = header ?? new CountRow(null, string.Empty, string.Empty);
        Rows = rows ?? Array.Empty<CountRow>();
        Expanded = expanded;
    }

    public CountRow Header { get; }

    public IReadOnlyList<CountRow> Rows { get; }

    public bool Expanded { get; }

    public string Signature()
    {
        var builder = new StringBuilder();
        Append(builder, Header);
        builder.Append(Expanded ? '+' : '-');
        if (Expanded)
        {
            foreach (var row in Rows)
            {
                Append(builder, row);
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, CountRow row) =>
        builder.Append('\u0002').Append(row.Icon == null ? 0L : row.Icon.Pointer.ToInt64()).Append('\u0001').Append(row.Label)
            .Append('\u0001').Append(row.Count).Append('\u0001').Append(row.Separated).Append(row.Muted);
}

public sealed class CountListStyle
{
    public float HeaderTextSize { get; set; } = 24f;

    public float RowTextSize { get; set; } = 21f;

    public float HeaderHeight { get; set; } = 40f;

    public float RowHeight { get; set; } = 32f;

    public float HeaderIconSize { get; set; } = 32f;

    public float RowIconSize { get; set; } = 26f;

    public float Padding { get; set; } = 10f;

    public float Gap { get; set; } = 8f;

    public float CountGap { get; set; } = 16f;

    public float BodyGap { get; set; } = 4f;

    public int CornerRadius { get; set; } = 12;

    public Color Text { get; set; } = new(1f, 0.96f, 0.9f, 1f);

    public Color Muted { get; set; } = new(1f, 0.96f, 0.9f, 0.55f);

    public Color Outline { get; set; } = new(0.16f, 0.11f, 0.08f, 1f);

    public float OutlineWidth { get; set; } = 0.22f;

    public Color Plate { get; set; } = new(0.16f, 0.11f, 0.08f, 1f);

    public Color Line { get; set; } = new(1f, 0.96f, 0.9f, 0.35f);

    public float PlateAlpha { get; set; } = 0.15f;

    public void Decorate(TMPro.TMP_Text text)
    {
        if (OutlineWidth > 0f)
        {
            text.outlineColor = Outline;
            text.outlineWidth = OutlineWidth;
        }
    }

    public float Scale { get; set; } = 1f;
}

public sealed class CountList
{
    private readonly CountListStyle _style;
    private readonly GameObject _root;
    private readonly RectTransform _rect;
    private readonly Image _plate;
    private readonly RectTransform _content;
    private string _signature;
    private bool _growUp;

    private CountList(Transform parent, string name, CountListStyle style)
    {
        _style = style ?? new CountListStyle();
        _root = new GameObject(name);
        _root.layer = parent.gameObject.layer;
        _rect = _root.AddComponent<RectTransform>();
        _rect.SetParent(parent, false);
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _plate = _root.AddComponent<Image>();
        _plate.sprite = UiSprites.RoundedPlate(_style.CornerRadius);
        _plate.type = Image.Type.Sliced;
        _plate.raycastTarget = false;
        var content = new GameObject("content");
        content.layer = _root.layer;
        _content = content.AddComponent<RectTransform>();
        _content.SetParent(_rect, false);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(0f, 1f);
        _content.pivot = new Vector2(0f, 1f);
        ApplyPlate();
    }

    public RectTransform Rect => _rect;

    public Vector2 Size => IsAlive ? _rect.sizeDelta : Vector2.zero;

    public bool IsAlive => UiClone.IsAlive(_root);

    public CountListStyle Style => _style;

    public static CountList Create(Transform parent, string name, CountListStyle style = null) => new(parent, name, style);

    public bool Show(CountListContent content, bool growUp = false)
    {
        if (!IsAlive || content == null)
        {
            return false;
        }

        var signature = content.Signature() + (growUp ? "^" : "v") + _style.Scale.ToString("0.###");
        if (signature == _signature)
        {
            return false;
        }

        _signature = signature;
        _growUp = growUp;
        Build(content);
        return true;
    }

    public void ApplyPlate()
    {
        if (!IsAlive)
        {
            return;
        }

        var color = _style.Plate;
        color.a = Mathf.Clamp01(_style.PlateAlpha);
        _plate.color = color;
        _plate.enabled = color.a > 0.001f;
    }

    public void Invalidate() => _signature = null;

    public void Destroy()
    {
        if (IsAlive)
        {
            UnityEngine.Object.Destroy(_root);
        }
    }

    private void Build(CountListContent content)
    {
        UiClone.DestroyChildren(_content);
        var scale = Mathf.Max(0.3f, _style.Scale);
        var rows = new List<(CountRow Row, bool Header)> { (content.Header, true) };
        if (content.Expanded)
        {
            foreach (var row in content.Rows)
            {
                rows.Add((row, false));
            }
        }

        var built = new List<(RectTransform Line, Image Icon, TMP_Text Label, TMP_Text Count, float Height, float LabelWidth, float CountWidth, bool Separated)>();
        var labelColumn = 0f;
        var countColumn = 0f;
        var hasIcon = false;
        foreach (var (row, header) in rows)
        {
            var height = (header ? _style.HeaderHeight : _style.RowHeight) * scale;
            var textSize = (header ? _style.HeaderTextSize : _style.RowTextSize) * scale;
            var color = row.Muted ? _style.Muted : _style.Text;
            var line = NewRect(header ? "header" : "row", _content);
            Image icon = null;
            if (row.Icon != null)
            {
                icon = NewRect("icon", line).gameObject.AddComponent<Image>();
                icon.sprite = row.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = row.Muted ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                hasIcon = true;
            }

            var label = HudLayer.CreateText(line, "label", textSize, color, TextAlignmentOptions.MidlineLeft);
            _style.Decorate(label);
            label.text = row.Label;
            var count = HudLayer.CreateText(line, "count", textSize, color, TextAlignmentOptions.MidlineRight);
            _style.Decorate(count);
            count.text = header ? "<b>" + row.Count + "</b>" : row.Count;
            var labelWidth = row.Label.Length == 0 ? 0f : label.GetPreferredValues(label.text).x;
            var countWidth = row.Count.Length == 0 ? 0f : count.GetPreferredValues(count.text).x;
            labelColumn = Mathf.Max(labelColumn, labelWidth);
            countColumn = Mathf.Max(countColumn, countWidth);
            built.Add((line, icon, label, count, height, labelWidth, countWidth, !header && row.Separated));
        }

        var padding = _style.Padding * scale;
        var gap = _style.Gap * scale;
        var iconColumn = hasIcon ? _style.HeaderIconSize * scale : 0f;
        var labelStart = padding + (hasIcon ? iconColumn + gap : 0f);
        var countGap = labelColumn > 0f && countColumn > 0f ? _style.CountGap * scale : 0f;
        var width = labelStart + labelColumn + countGap + countColumn + padding;
        var order = new List<int>();
        for (var index = 0; index < built.Count; index++)
        {
            order.Add(index);
        }

        if (_growUp)
        {
            order.Reverse();
        }

        var y = padding * 0.5f;
        foreach (var index in order)
        {
            var item = built[index];
            var header = index == 0;
            if (header && content.Expanded && built.Count > 1 && _growUp)
            {
                y += _style.BodyGap * scale;
            }

            if (item.Separated && !_growUp)
            {
                AddSeparator(y, width, padding, scale);
                y += 3f * scale;
            }

            Place(item.Line, 0f, y, width, item.Height);
            if (item.Icon != null)
            {
                var size = (header ? _style.HeaderIconSize : _style.RowIconSize) * scale;
                Place(item.Icon.rectTransform, padding + (iconColumn - size) * 0.5f, (item.Height - size) * 0.5f, size, size);
            }

            Place(item.Label.rectTransform, labelStart, 0f, labelColumn + 2f, item.Height);
            Place(item.Count.rectTransform, width - padding - countColumn - 2f, 0f, countColumn + 2f, item.Height);
            y += item.Height;
            if (item.Separated && _growUp)
            {
                AddSeparator(y, width, padding, scale);
                y += 3f * scale;
            }

            if (header && content.Expanded && built.Count > 1 && !_growUp)
            {
                y += _style.BodyGap * scale;
            }
        }

        y += padding * 0.5f;
        _content.sizeDelta = new Vector2(width, y);
        _rect.sizeDelta = new Vector2(width, y);
    }

    private void AddSeparator(float y, float width, float padding, float scale)
    {
        var line = NewRect("separator", _content);
        var image = line.gameObject.AddComponent<Image>();
        image.color = _style.Line;
        image.raycastTarget = false;
        Place(line, padding, y + 1f * scale, width - padding * 2f, Mathf.Max(1f, 1.5f * scale));
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var item = new GameObject(name);
        item.layer = parent.gameObject.layer;
        var rect = item.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
