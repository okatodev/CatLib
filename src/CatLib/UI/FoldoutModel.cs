using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CatLib.UI;

public enum FoldoutTone
{
    Normal,
    Good,
    Warning,
    Bad,
    Muted
}

public sealed class FoldoutRow
{
    public FoldoutRow(string left, string middle = null, string right = null, FoldoutTone tone = FoldoutTone.Normal, Action onClick = null)
    {
        Left = left ?? string.Empty;
        Middle = middle ?? string.Empty;
        Right = right ?? string.Empty;
        Tone = tone;
        OnClick = onClick;
    }

    public string Left { get; }

    public string Middle { get; }

    public string Right { get; }

    public FoldoutTone Tone { get; }

    public Action OnClick { get; }
}

public sealed class FoldoutSection
{
    public FoldoutSection(string key, string title, string detail, string status, FoldoutTone statusTone, IReadOnlyList<FoldoutRow> rows)
    {
        Key = key ?? string.Empty;
        Title = title ?? string.Empty;
        Detail = detail ?? string.Empty;
        Status = status ?? string.Empty;
        StatusTone = statusTone;
        Rows = rows ?? Array.Empty<FoldoutRow>();
    }

    public string Key { get; }

    public string Title { get; }

    public string Detail { get; }

    public string Status { get; }

    public FoldoutTone StatusTone { get; }

    public IReadOnlyList<FoldoutRow> Rows { get; }
}

public sealed class FoldoutContent
{
    public static readonly FoldoutContent Empty = new(string.Empty, string.Empty, FoldoutTone.Normal, Array.Empty<FoldoutRow>(), Array.Empty<FoldoutSection>());

    public FoldoutContent(string title, string summary, FoldoutTone summaryTone, IReadOnlyList<FoldoutRow> topRows, IReadOnlyList<FoldoutSection> sections)
    {
        Title = title ?? string.Empty;
        Summary = summary ?? string.Empty;
        SummaryTone = summaryTone;
        TopRows = topRows ?? Array.Empty<FoldoutRow>();
        Sections = sections ?? Array.Empty<FoldoutSection>();
    }

    public string Title { get; }

    public string Summary { get; }

    public FoldoutTone SummaryTone { get; }

    public IReadOnlyList<FoldoutRow> TopRows { get; }

    public IReadOnlyList<FoldoutSection> Sections { get; }

    public int RowCount => TopRows.Count + Sections.Sum(section => section.Rows.Count);

    public string Signature()
    {
        var builder = new StringBuilder();
        builder.Append(Title).Append('\u0001').Append(Summary).Append('\u0001').Append((int)SummaryTone);
        foreach (var row in TopRows)
        {
            Append(builder, row);
        }

        foreach (var section in Sections)
        {
            builder.Append('\u0002').Append(section.Key).Append('\u0001').Append(section.Title).Append('\u0001').Append(section.Detail)
                .Append('\u0001').Append(section.Status).Append('\u0001').Append((int)section.StatusTone);
            foreach (var row in section.Rows)
            {
                Append(builder, row);
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, FoldoutRow row) =>
        builder.Append('\u0003').Append(row.Left).Append('\u0001').Append(row.Middle).Append('\u0001').Append(row.Right)
            .Append('\u0001').Append((int)row.Tone).Append('\u0001').Append(row.OnClick != null);
}
