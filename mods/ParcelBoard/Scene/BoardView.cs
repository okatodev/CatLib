using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatLib.Game;
using CatLib.UI;
using ParcelBoard.Logic;
using UnityEngine;

namespace ParcelBoard.Scene;

public sealed class BoardLook
{
    public bool Open { get; set; }

    public BoardCorner Corner { get; set; } = BoardCorner.TopLeft;

    public float Scale { get; set; } = 0.9f;

    public float Opacity { get; set; } = 0.15f;

    public bool RegionNames { get; set; }

    public RegionOrder Order { get; set; } = RegionOrder.ByCount;

    public Func<BoardList, string> Title { get; set; } = list => list.ToString();

    public Func<PackageSize, string> SizeName { get; set; } = size => size.ToString();

    public Func<PackageSize, string> SizeTag { get; set; } = _ => string.Empty;
}

public sealed class BoardView
{
    public const string RootName = "ParcelBoard_Board";
    public const string IconPrefix = "ParcelBoard.Icons.";
    public const float Margin = 24f;
    public const float TableGap = 8f;

    private GameObject _root;
    private RectTransform _rect;
    private IntPtr _hudPointer;
    private CountTable _main;
    private CountTable _sizes;

    public bool IsAlive => _root != null && !_root.WasCollected;

    public RectTransform Rect => IsAlive ? _rect : null;

    public CountTable Main => _main != null && _main.IsAlive ? _main : null;

    public CountTable Sizes => _sizes != null && _sizes.IsAlive ? _sizes : null;

    public void Show(IReadOnlyList<BoardColumn> columns, BoardLook look)
    {
        var hud = HudLayer.Root;
        if (hud == null)
        {
            return;
        }

        if (!IsAlive || hud.Pointer != _hudPointer)
        {
            Create(hud);
        }

        if (!_root.activeSelf)
        {
            _root.SetActive(true);
        }

        var bottom = look.Corner == BoardCorner.BottomLeft || look.Corner == BoardCorner.BottomRight;
        var right = look.Corner == BoardCorner.TopRight || look.Corner == BoardCorner.BottomRight;
        var model = BoardTable.Build(columns, look.Order);
        var tables = new List<CountTable>();
        var mainContent = model.Columns.Count > 0 ? MainContent(model, look) : null;
        var lines = mainContent == null ? 0 : model.Lines.Count;
        var sizesContent = model.Sizes != null ? SizesContent(model.Sizes, look, BoardTable.SizeRowsPerBlock(model.Sizes.Rows.Count, lines)) : null;
        if (mainContent != null)
        {
            _main = Ensure(_main, "ParcelBoard_Table", look);
        }
        else
        {
            _main?.Destroy();
            _main = null;
        }

        if (sizesContent != null)
        {
            _sizes = Ensure(_sizes, "ParcelBoard_Sizes", look);
        }
        else
        {
            _sizes?.Destroy();
            _sizes = null;
        }

        var height = Mathf.Max(_main == null ? 0f : _main.HeightOf(mainContent), _sizes == null ? 0f : _sizes.HeightOf(sizesContent));
        if (_main != null)
        {
            _main.Show(mainContent, bottom, height);
            tables.Add(_main);
        }

        if (_sizes != null)
        {
            _sizes.Show(sizesContent, bottom, height);
            tables.Add(_sizes);
        }

        if (right)
        {
            tables.Reverse();
        }

        var x = 0f;
        height = 0f;
        foreach (var table in tables)
        {
            var rect = table.Rect;
            rect.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = rect.anchorMin;
            rect.anchoredPosition = new Vector2(x, 0f);
            x += table.Size.x + TableGap * look.Scale;
            height = Mathf.Max(height, table.Size.y);
        }

        var corner = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
        _rect.anchorMin = corner;
        _rect.anchorMax = corner;
        _rect.pivot = corner;
        _rect.sizeDelta = new Vector2(Mathf.Max(0f, x - TableGap * look.Scale), height);
        _rect.anchoredPosition = new Vector2(right ? -Margin : Margin, bottom ? Margin : -Margin);
    }

    public void Hide()
    {
        if (IsAlive && _root.activeSelf)
        {
            _root.SetActive(false);
        }
    }

    public void Forget()
    {
        if (IsAlive)
        {
            UnityEngine.Object.Destroy(_root);
        }

        _root = null;
        _main = null;
        _sizes = null;
    }

    public static CountTableContent MainContent(BoardTableModel model, BoardLook look)
    {
        var columns = model.Columns.Select(column =>
        {
            var icon = ListIcon(column.List);
            return new TableColumn(icon, icon == null ? look.Title(column.List) : string.Empty, Number(column.Total), column.Total == 0);
        }).ToList();
        var rows = model.Lines.Select(line =>
        {
            var icon = CatParcels.RegionIcon(line.Region);
            var label = icon == null || look.RegionNames ? CatParcels.RegionName(line.Region) : string.Empty;
            return new TableRow(icon, label, line.Counts.Select(count => count == 0 ? string.Empty : Number(count)).ToList(), line.Separated);
        }).ToList();
        return new CountTableContent(columns, rows, look.Open);
    }

    public static CountTableContent SizesContent(BoardColumn sizes, BoardLook look, int rowsPerBlock = 0)
    {
        var header = SizesIcon();
        var columns = new[] { new TableColumn(header, header == null ? look.Title(BoardList.Sizes) : string.Empty, Number(sizes.Total), sizes.Total == 0) };
        var rows = sizes.Rows.Select(row =>
        {
            var icon = SizeIcon(row.Size);
            var tag = look.SizeTag(row.Size) ?? string.Empty;
            var cells = new[] { row.Count == 0 ? string.Empty : Number(row.Count) };
            return icon == null
                ? new TableRow(null, look.SizeName(row.Size), cells)
                : new TableRow(icon, tag, cells, mutedLabel: true);
        }).ToList();
        return new CountTableContent(columns, rows, look.Open, rowsPerBlock);
    }

    public static Sprite SizesIcon() => UiSprites.FromResource(typeof(BoardView).Assembly, IconPrefix + "sizes.png");

    public static Sprite SizeIcon(PackageSize size) =>
        size == PackageSize.None ? null : UiSprites.FromResource(typeof(BoardView).Assembly, IconPrefix + "size_" + size + ".png");

    public static Sprite ListIcon(BoardList list) => list switch
    {
        BoardList.All => UiSprites.FromResource(typeof(BoardView).Assembly, IconPrefix + "all.png"),
        BoardList.Damaged => UiSprites.FromResource(typeof(BoardView).Assembly, IconPrefix + "damaged.png"),
        BoardList.NeedsStamps => UiSprites.FromResource(typeof(BoardView).Assembly, IconPrefix + "stamps.png"),
        BoardList.Heavy => CatParcels.ConstraintIcon(BehaviorConstraint.Heavy),
        BoardList.Fragile => CatParcels.ConstraintIcon(BehaviorConstraint.Fragile),
        BoardList.ContactForbidden => CatParcels.ConstraintIcon(BehaviorConstraint.ContactForbidden),
        BoardList.Lover => CatParcels.ConstraintIcon(BehaviorConstraint.Lover),
        BoardList.Corrupted => CatParcels.ConstraintIcon(BehaviorConstraint.Corrupted),
        BoardList.Dark => CatParcels.ConstraintIcon(StorageConstraint.Dark),
        BoardList.Frozen => CatParcels.ConstraintIcon(StorageConstraint.Frozen),
        BoardList.Hot => CatParcels.ConstraintIcon(StorageConstraint.Hot),
        BoardList.Cold => CatParcels.ConstraintIcon(StorageConstraint.Cold),
        BoardList.Bright => CatParcels.ConstraintIcon(StorageConstraint.Bright),
        _ => null
    };

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private CountTable Ensure(CountTable table, string name, BoardLook look)
    {
        if (table == null || !table.IsAlive)
        {
            table = CountTable.Create(_rect, name);
        }

        table.Style.Scale = look.Scale;
        if (Math.Abs(table.Style.PlateAlpha - look.Opacity) > 0.001f)
        {
            table.Style.PlateAlpha = look.Opacity;
            table.ApplyPlate();
        }

        return table;
    }

    private void Create(RectTransform hud)
    {
        Forget();
        _hudPointer = hud.Pointer;
        _root = new GameObject(RootName);
        _root.layer = hud.gameObject.layer;
        _rect = _root.AddComponent<RectTransform>();
        _rect.SetParent(hud, false);
    }
}
