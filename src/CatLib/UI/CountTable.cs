using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatLib.UI;

public sealed class TableColumn
{
    public TableColumn(Sprite icon, string title, string total, bool muted = false)
    {
        Icon = icon;
        Title = title ?? string.Empty;
        Total = total ?? string.Empty;
        Muted = muted;
    }

    public Sprite Icon { get; }

    public string Title { get; }

    public string Total { get; }

    public bool Muted { get; }
}

public sealed class TableRow
{
    public TableRow(Sprite icon, string label, IReadOnlyList<string> cells, bool separated = false, bool mutedLabel = false)
    {
        Icon = icon;
        Label = label ?? string.Empty;
        Cells = cells ?? Array.Empty<string>();
        Separated = separated;
        MutedLabel = mutedLabel;
    }

    public Sprite Icon { get; }

    public string Label { get; }

    public IReadOnlyList<string> Cells { get; }

    public bool Separated { get; }

    public bool MutedLabel { get; }
}

public sealed class CountTableContent
{
    public CountTableContent(IReadOnlyList<TableColumn> columns, IReadOnlyList<TableRow> rows, bool expanded, int rowsPerBlock = 0)
    {
        Columns = columns ?? Array.Empty<TableColumn>();
        Rows = rows ?? Array.Empty<TableRow>();
        Expanded = expanded;
        RowsPerBlock = Math.Max(0, rowsPerBlock);
    }

    public IReadOnlyList<TableColumn> Columns { get; }

    public IReadOnlyList<TableRow> Rows { get; }

    public bool Expanded { get; }

    public int RowsPerBlock { get; }

    public int VisibleRows => !Expanded || Rows.Count == 0 ? 0 : RowsPerBlock > 0 ? Math.Min(RowsPerBlock, Rows.Count) : Rows.Count;

    public int Blocks => VisibleRows == 0 ? 1 : (Rows.Count + VisibleRows - 1) / VisibleRows;

    public string Signature()
    {
        var builder = new StringBuilder();
        foreach (var column in Columns)
        {
            builder.Append('\u0002').Append(Key(column.Icon)).Append('\u0001').Append(column.Title).Append('\u0001').Append(column.Total).Append(column.Muted);
        }

        builder.Append(Expanded ? '+' : '-').Append(RowsPerBlock);
        if (Expanded)
        {
            foreach (var row in Rows)
            {
                builder.Append('\u0003').Append(Key(row.Icon)).Append('\u0001').Append(row.Label).Append(row.Separated).Append(row.MutedLabel);
                foreach (var cell in row.Cells)
                {
                    builder.Append('\u0001').Append(cell);
                }
            }
        }

        return builder.ToString();
    }

    private static long Key(Sprite sprite) => sprite == null ? 0L : sprite.Pointer.ToInt64();
}

public sealed class CountTable
{
    private readonly CountListStyle _style;
    private readonly GameObject _root;
    private readonly RectTransform _rect;
    private readonly Image _plate;
    private readonly RectTransform _content;
    private string _signature;

    private CountTable(Transform parent, string name, CountListStyle style)
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
        Anchor(_content);
        ApplyPlate();
    }

    public RectTransform Rect => _rect;

    public Vector2 Size => IsAlive ? _rect.sizeDelta : Vector2.zero;

    public bool IsAlive => UiClone.IsAlive(_root);

    public CountListStyle Style => _style;

    public static CountTable Create(Transform parent, string name, CountListStyle style = null) => new(parent, name, style);

    public bool Show(CountTableContent content, bool growUp = false, float minHeight = 0f)
    {
        if (!IsAlive || content == null)
        {
            return false;
        }

        var signature = content.Signature() + (growUp ? "^" : "v") + _style.Scale.ToString("0.###") + "|" + minHeight.ToString("0.#");
        if (signature == _signature)
        {
            return false;
        }

        _signature = signature;
        Build(content, growUp, minHeight);
        return true;
    }

    public float HeightOf(CountTableContent content)
    {
        var scale = Mathf.Max(0.3f, _style.Scale);
        var rows = content == null ? 0 : content.VisibleRows;
        var header = _style.HeaderIconSize * scale + _style.Gap * scale * 0.5f + _style.RowHeight * scale;
        var body = rows == 0 ? 0f : _style.BodyGap * scale + rows * _style.RowHeight * scale;
        return _style.Padding * scale * 2f + header + body;
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

    private void Build(CountTableContent content, bool growUp, float minHeight)
    {
        UiClone.DestroyChildren(_content);
        var scale = Mathf.Max(0.3f, _style.Scale);
        var padding = _style.Padding * scale;
        var gap = _style.Gap * scale;
        var iconSize = _style.HeaderIconSize * scale;
        var rowIcon = _style.RowIconSize * scale;
        var titleSize = _style.RowTextSize * scale;
        var totalSize = _style.HeaderTextSize * scale;
        var rows = content.Expanded ? content.Rows : Array.Empty<TableRow>();

        var widths = new float[content.Columns.Count];
        var headerTexts = new List<(TMP_Text Title, TMP_Text Total)>();
        for (var column = 0; column < content.Columns.Count; column++)
        {
            var item = content.Columns[column];
            var color = item.Muted ? _style.Muted : _style.Text;
            TMP_Text title = null;
            if (item.Icon == null && item.Title.Length > 0)
            {
                title = Text("title", titleSize, color, TextAlignmentOptions.Center, item.Title);
                widths[column] = Mathf.Max(widths[column], Width(title));
            }
            else
            {
                widths[column] = Mathf.Max(widths[column], iconSize);
            }

            var total = Text("total", totalSize, color, TextAlignmentOptions.Center, "<b>" + item.Total + "</b>");
            widths[column] = Mathf.Max(widths[column], Width(total));
            headerTexts.Add((title, total));
        }

        var labelColumn = 0f;
        var hasRowIcon = false;
        var rowTexts = new List<(TMP_Text Label, TMP_Text[] Cells)>();
        foreach (var row in rows)
        {
            hasRowIcon |= row.Icon != null;
            TMP_Text label = null;
            if (row.Label.Length > 0)
            {
                label = Text("label", row.MutedLabel ? titleSize * 0.8f : titleSize, row.MutedLabel ? Color.Lerp(_style.Text, _style.Muted, 0.5f) : _style.Text, TextAlignmentOptions.MidlineLeft, row.Label);
                labelColumn = Mathf.Max(labelColumn, Width(label));
            }

            var cells = new TMP_Text[content.Columns.Count];
            for (var column = 0; column < cells.Length && column < row.Cells.Count; column++)
            {
                if (row.Cells[column].Length == 0)
                {
                    continue;
                }

                cells[column] = Text("cell", titleSize, _style.Text, TextAlignmentOptions.Center, row.Cells[column]);
                widths[column] = Mathf.Max(widths[column], Width(cells[column]));
            }

            rowTexts.Add((label, cells));
        }

        var rowHeader = rows.Count == 0 ? 0f : (hasRowIcon ? rowIcon : 0f) + (labelColumn > 0f ? (hasRowIcon ? gap : 0f) + labelColumn : 0f);
        var columnGap = _style.CountGap * scale * 0.6f;
        var cellsLeft = rowHeader > 0f ? rowHeader + columnGap : 0f;
        var lefts = new float[widths.Length];
        var blockWidth = cellsLeft;
        for (var column = 0; column < widths.Length; column++)
        {
            lefts[column] = blockWidth;
            blockWidth += widths[column] + (column + 1 < widths.Length ? columnGap : 0f);
        }

        var perBlock = content.VisibleRows;
        var blocks = content.Blocks;
        var blockGap = columnGap * 2.5f;
        var width = padding * 2f + blocks * blockWidth + (blocks - 1) * blockGap;
        var iconRow = iconSize + gap * 0.5f;
        var totalRow = _style.RowHeight * scale;
        var bodyRow = _style.RowHeight * scale;
        var headerHeight = iconRow + totalRow;
        var natural = HeightOf(content);
        var height = Mathf.Max(natural, minHeight);
        var extra = height - natural;
        var headerTop = growUp ? height - padding - headerHeight : padding;
        var bodyTop = growUp ? padding + extra : padding + headerHeight + _style.BodyGap * scale;

        var spanHeader = blocks > 1 && content.Columns.Count == 1;
        for (var column = 0; column < content.Columns.Count; column++)
        {
            var item = content.Columns[column];
            var (title, total) = headerTexts[column];
            var left = spanHeader ? padding : padding + lefts[column];
            var span = spanHeader ? width - padding * 2f : widths[column];
            if (item.Icon != null)
            {
                var image = NewRect("icon", _content).gameObject.AddComponent<Image>();
                image.sprite = item.Icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = item.Muted ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                Place(image.rectTransform, left + (span - iconSize) * 0.5f, headerTop, iconSize, iconSize);
            }
            else if (title != null)
            {
                Place(title.rectTransform, left, headerTop, span, iconSize);
            }

            Place(total.rectTransform, left, headerTop + iconRow, span, totalRow);
        }

        for (var block = 0; block < blocks && perBlock > 0; block++)
        {
            var x0 = padding + block * (blockWidth + blockGap);
            var first = block * perBlock;
            var count = Math.Min(perBlock, rows.Count - first);
            for (var slot = 0; slot < count; slot++)
            {
                var index = first + slot;
                var y = growUp ? bodyTop + (perBlock - 1 - slot) * bodyRow : bodyTop + slot * bodyRow;
                var row = rows[index];
                if (row.Separated)
                {
                    Separator(x0, growUp ? y + bodyRow : y, blockWidth, scale);
                }

                var (label, cells) = rowTexts[index];
                var x = x0;
                if (row.Icon != null)
                {
                    var image = NewRect("icon", _content).gameObject.AddComponent<Image>();
                    image.sprite = row.Icon;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    Place(image.rectTransform, x, y + (bodyRow - rowIcon) * 0.5f, rowIcon, rowIcon);
                }

                if (hasRowIcon)
                {
                    x += rowIcon + gap;
                }

                if (label != null)
                {
                    Place(label.rectTransform, x, y, labelColumn + 2f, bodyRow);
                }

                for (var column = 0; column < cells.Length; column++)
                {
                    if (cells[column] != null)
                    {
                        Place(cells[column].rectTransform, x0 + lefts[column], y, widths[column], bodyRow);
                    }
                }
            }
        }

        _content.sizeDelta = new Vector2(width, height);
        _rect.sizeDelta = new Vector2(width, height);
    }

    private TMP_Text Text(string name, float size, Color color, TextAlignmentOptions alignment, string value)
    {
        var text = HudLayer.CreateText(_content, name, size, color, alignment);
        _style.Decorate(text);
        text.text = value;
        return text;
    }

    private static float Width(TMP_Text text) => text.GetPreferredValues(text.text).x;

    private void Separator(float x, float y, float width, float scale)
    {
        var line = NewRect("separator", _content);
        var image = line.gameObject.AddComponent<Image>();
        image.color = _style.Line;
        image.raycastTarget = false;
        var thickness = Mathf.Max(1f, 1.5f * scale);
        Place(line, x, y - thickness * 0.5f, width, thickness);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var item = new GameObject(name);
        item.layer = parent.gameObject.layer;
        var rect = item.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        Anchor(rect);
        return rect;
    }

    private static void Anchor(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        Anchor(rect);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
