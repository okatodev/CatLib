using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CatLib.DevTools;

internal static class DevMenuView
{
    public const float ReferenceHeight = 1080f;
    public const float BaseScale = 1.1f;
    public const float Left = 12f;
    public const float Top = 12f;
    public const float Width = 600f;
    public const float GroupWidth = 150f;
    public const float HeaderHeight = 24f;
    public const float RowHeight = 20f;
    public const float Padding = 6f;
    public const float NumberWidth = 20f;
    public const float StateWidth = 40f;
    public const float HintHeight = 34f;
    public const float LineHeight = 18f;
    public const int VisibleGroups = 12;
    public const int VisibleItems = 12;
    public const float FitSlack = 4f;
    public const int MaxFitCache = 512;

    private static readonly Color Back = new(0.06f, 0.065f, 0.08f, 1f);
    private static readonly Color HeaderBack = new(0.13f, 0.14f, 0.17f, 1f);
    private static readonly Color ColumnBack = new(0.1f, 0.105f, 0.125f, 1f);
    private static readonly Color Line = new(0.24f, 0.25f, 0.29f, 1f);
    private static readonly Color Accent = new(1f, 0.74f, 0.25f, 1f);
    private static readonly Color AccentBack = new(0.36f, 0.28f, 0.12f, 1f);
    private static readonly Color HoverBack = new(0.17f, 0.18f, 0.21f, 1f);
    private static readonly Color Text = new(1f, 1f, 1f, 1f);
    private static readonly Color Dim = new(0.84f, 0.85f, 0.88f, 1f);
    private static readonly Color Faint = new(0.62f, 0.64f, 0.69f, 1f);
    private static readonly Color Good = new(0.45f, 0.86f, 0.5f, 1f);
    private static readonly Color Bad = new(1f, 0.45f, 0.4f, 1f);
    private static readonly Color OnBack = new(0.2f, 0.55f, 0.28f, 1f);
    private static readonly Color OffBack = new(0.3f, 0.31f, 0.36f, 1f);

    private static readonly Dictionary<(string Text, int Width), string> Fitted = new();

    private static GUIStyle _fill;
    private static GUIStyle _label;
    private static GUIStyle _right;
    private static GUIStyle _center;
    private static GUIStyle _wrap;
    private static GUIContent _content;

    private static bool _runRequested;

    public static bool TakeRunRequest()
    {
        var requested = _runRequested;
        _runRequested = false;
        return requested;
    }

    public static float Scale => Mathf.Max(1f, Screen.height / ReferenceHeight * BaseScale);

    public static void Draw(DevMenuModel model, string closeKeys)
    {
        EnsureStyles();
        var groups = model.GroupSizes;
        var items = model.Current;
        var groupRows = Math.Min(Math.Max(groups.Count, 1), VisibleGroups);
        var itemRows = Math.Min(Math.Max(items.Count, 1), VisibleItems);
        var bodyHeight = Math.Max(groupRows, itemRows) * RowHeight + Padding * 2f;
        var footerHeight = HintHeight + LineHeight * 3f + Padding * 2f;
        var panel = new Rect(Left, Top, Width, HeaderHeight + bodyHeight + footerHeight);
        Fill(panel, Back);

        var header = new Rect(panel.x, panel.y, panel.width, HeaderHeight);
        Fill(header, HeaderBack);
        Fill(new Rect(header.x, header.yMax - 2f, header.width, 2f), Accent);
        Label(new Rect(header.x + Padding, header.y, 200f, HeaderHeight - 2f), "CatLib developer menu", _label, Accent);
        Label(new Rect(header.x + 210f, header.y, header.width - 210f - Padding, HeaderHeight - 2f),
            $"{model.CurrentGroup}   {model.GroupIndex + 1}/{groups.Count}", _right, Dim);

        var body = new Rect(panel.x, header.yMax, panel.width, bodyHeight);
        var column = new Rect(body.x, body.y, GroupWidth, body.height);
        Fill(column, ColumnBack);
        Fill(new Rect(column.xMax, body.y, 1f, body.height), Line);
        DrawGroups(model, groups, new Rect(column.x, column.y + Padding, column.width, groupRows * RowHeight));
        DrawItems(model, items, new Rect(column.xMax + 1f, body.y + Padding, body.width - GroupWidth - 1f, itemRows * RowHeight));

        var footer = new Rect(panel.x, body.yMax, panel.width, footerHeight);
        Fill(new Rect(footer.x, footer.y, footer.width, 1f), Line);
        var hint = model.SelectedItem?.Hint;
        Label(new Rect(footer.x + Padding, footer.y + Padding, footer.width - Padding * 2f, HintHeight),
            string.IsNullOrWhiteSpace(hint) ? "No description" : hint, _wrap, string.IsNullOrWhiteSpace(hint) ? Faint : Dim);
        var statusRect = new Rect(footer.x + Padding, footer.y + Padding + HintHeight, footer.width - Padding * 2f, LineHeight * 2f);
        if (string.IsNullOrEmpty(model.Status))
        {
            Label(statusRect, "Nothing run yet", _wrap, Faint);
        }
        else
        {
            var time = model.StatusTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Label(statusRect, $"{time}  {(model.StatusFailed ? "FAILED" : "OK")}  {model.Status}", _wrap, model.StatusFailed ? Bad : Good);
        }

        Label(new Rect(footer.x + Padding, statusRect.yMax, footer.width - Padding * 2f, LineHeight),
            $"↑↓ Home End select · ←→ PgUp PgDn group · Enter 1-9 click run · {closeKeys} close", _label, Faint);
    }

    private static void DrawGroups(DevMenuModel model, IReadOnlyList<(string Group, int Count)> groups, Rect area)
    {
        var shown = groups.Count > VisibleGroups ? VisibleGroups - 1 : VisibleGroups;
        var first = DevMenuModel.FirstVisible(groups.Count, model.GroupIndex, shown);
        var last = Math.Min(groups.Count, first + shown);
        for (var index = first; index < last; index++)
        {
            var row = new Rect(area.x, area.y + (index - first) * RowHeight, area.width, RowHeight);
            var selected = index == model.GroupIndex;
            if (selected)
            {
                Fill(row, AccentBack);
                Fill(new Rect(row.x, row.y, 3f, row.height), Accent);
            }
            else if (IsHovered(row))
            {
                Fill(row, HoverBack);
            }

            var (name, count) = groups[index];
            Label(new Rect(row.x + Padding + 2f, row.y, row.width - Padding * 2f - 30f, row.height), name, _label, selected ? Text : Dim);
            Label(new Rect(row.xMax - Padding - 30f, row.y, 30f, row.height), count.ToString(CultureInfo.InvariantCulture), _right, Faint);
            if (Clicked(row))
            {
                model.SelectGroup(index);
            }
        }

        More(area, shown, first, groups.Count - last);
    }

    private static void DrawItems(DevMenuModel model, IReadOnlyList<DevItem> items, Rect area)
    {
        if (items.Count == 0)
        {
            Label(new Rect(area.x + Padding, area.y, area.width - Padding * 2f, RowHeight), "No commands in this group", _label, Faint);
            return;
        }

        var shown = items.Count > VisibleItems ? VisibleItems - 1 : VisibleItems;
        var first = DevMenuModel.FirstVisible(items.Count, model.Selected, shown);
        var last = Math.Min(items.Count, first + shown);
        for (var index = first; index < last; index++)
        {
            var item = items[index];
            var row = new Rect(area.x, area.y + (index - first) * RowHeight, area.width, RowHeight);
            var selected = index == model.Selected;
            if (selected)
            {
                Fill(row, AccentBack);
                Fill(new Rect(row.x, row.y, 3f, row.height), Accent);
            }
            else if (IsHovered(row))
            {
                Fill(row, HoverBack);
            }

            var number = index < DevMenuModel.DigitItems ? (index + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
            Label(new Rect(row.x + Padding, row.y, NumberWidth, row.height), number, _label, selected ? Accent : Faint);
            var state = item.State;
            var labelWidth = row.width - Padding * 2f - NumberWidth - (state == null ? 0f : StateWidth + 8f);
            Label(new Rect(row.x + Padding + NumberWidth, row.y, labelWidth, row.height), item.Label, _label, selected ? Text : Dim);
            if (state != null)
            {
                var on = string.Equals(state, "on", StringComparison.OrdinalIgnoreCase);
                var off = string.Equals(state, "off", StringComparison.OrdinalIgnoreCase);
                var pill = new Rect(row.xMax - Padding - StateWidth, row.y + 4f, StateWidth, row.height - 8f);
                Fill(pill, on ? OnBack : off ? OffBack : HeaderBack);
                Label(pill, state.ToUpperInvariant(), _center, on || off ? Text : Accent);
            }

            if (Clicked(row))
            {
                model.SelectItem(index);
                _runRequested = true;
            }
        }

        More(area, shown, first, items.Count - last);
    }

    private static void More(Rect area, int shown, int above, int below)
    {
        if (above == 0 && below == 0)
        {
            return;
        }

        var row = new Rect(area.x + Padding, area.y + shown * RowHeight, area.width - Padding * 2f, RowHeight);
        Label(row, $"↑ {above} above · ↓ {below} below", _label, Faint);
    }

    private static bool IsHovered(Rect rect)
    {
        var current = Event.current;
        return current != null && rect.Contains(current.mousePosition);
    }

    private static bool Clicked(Rect rect)
    {
        var current = Event.current;
        if (current == null || current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
        {
            return false;
        }

        current.Use();
        return true;
    }

    private static void Fill(Rect rect, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        _content.text = string.Empty;
        GUI.Box(rect, _content, _fill);
        GUI.color = old;
    }

    private static void Label(Rect rect, string text, GUIStyle style, Color color)
    {
        var old = GUI.color;
        GUI.color = Color.white;
        style.normal.textColor = color;
        _content.text = style == _wrap ? text ?? string.Empty : Fit(text ?? string.Empty, rect.width - FitSlack);
        GUI.Label(rect, _content, style);
        GUI.color = old;
    }

    private static string Fit(string text, float width)
    {
        if (text.Length == 0 || width <= 0f)
        {
            return string.Empty;
        }

        var key = (text, Mathf.RoundToInt(width));
        if (Fitted.TryGetValue(key, out var fitted))
        {
            return fitted;
        }

        if (Fitted.Count > MaxFitCache)
        {
            Fitted.Clear();
        }

        fitted = text;
        if (Measure(text) > width)
        {
            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (Measure(text.Substring(0, middle).TrimEnd() + "…") <= width)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            fitted = low == 0 ? "…" : text.Substring(0, low).TrimEnd() + "…";
        }

        Fitted[key] = fitted;
        return fitted;
    }

    private static float Measure(string text)
    {
        _content.text = text;
        _label.CalcMinMaxWidth(_content, out _, out var max);
        return max;
    }

    private static void EnsureStyles()
    {
        if (_label != null && !_label.WasCollected && _fill != null && !_fill.WasCollected)
        {
            return;
        }

        _content = new GUIContent();
        _fill = new GUIStyle();
        _fill.normal.background = Texture2D.whiteTexture;
        _label = Style(TextAnchor.MiddleLeft);
        _right = Style(TextAnchor.MiddleRight);
        _center = Style(TextAnchor.MiddleCenter);
        _wrap = Style(TextAnchor.UpperLeft);
        Fitted.Clear();
    }

    private static GUIStyle Style(TextAnchor anchor)
    {
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = anchor;
        return style;
    }
}
