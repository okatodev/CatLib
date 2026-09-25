using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CatLib.Tests.Diagnostics.Inspection;

public static unsafe class MultiDimensionalArray
{
    public const int BoundsOffset = 16;
    public const int LengthOffset = 24;
    public const int DataOffset = 32;
    public const int BoundsSize = 16;
    public const int MaxElements = 1 << 20;

    public static bool TryFormat(Il2CppSystem.Object value, int maxSide, out string text)
    {
        text = null;
        var typeName = value.GetIl2CppType().FullName ?? string.Empty;
        var open = typeName.LastIndexOf('[');
        if (open < 0 || !typeName.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        var rank = typeName.Length - open - 1;
        if (rank != 2 || typeName.Substring(open) != "[,]")
        {
            return false;
        }

        var elementType = typeName.Substring(0, open);
        var pointer = (byte*)value.Pointer;
        var bounds = *(byte**)(pointer + BoundsOffset);
        var total = *(long*)(pointer + LengthOffset);
        if (bounds == null || total < 0 || total > MaxElements)
        {
            text = $"<{typeName} unreadable>";
            return true;
        }

        var rows = (int)*(long*)bounds;
        var columns = (int)*(long*)(bounds + BoundsSize);
        if (rows < 0 || columns < 0 || (long)rows * columns != total)
        {
            text = $"<{typeName} with inconsistent bounds>";
            return true;
        }

        var data = pointer + DataOffset;
        text = elementType switch
        {
            "System.Boolean" => Grid(rows, columns, maxSide, (row, column) => data[row * columns + column] != 0 ? '#' : '.', $"bool[{rows},{columns}] (# = true)"),
            "System.Int32" => Sample(rows, columns, index => ((int*)data)[index].ToString(CultureInfo.InvariantCulture), "int"),
            "System.Single" => Sample(rows, columns, index => ((float*)data)[index].ToString("0.###", CultureInfo.InvariantCulture), "float"),
            "UnityEngine.Vector3" => Sample(rows, columns, index => Vector((Vector3*)data + index), "Vector3"),
            _ => $"{elementType}[{rows},{columns}]"
        };
        return true;
    }

    private static string Grid(int rows, int columns, int maxSide, Func<int, int, char> cell, string title)
    {
        var builder = new StringBuilder(title);
        for (var row = 0; row < Math.Min(rows, maxSide); row++)
        {
            builder.Append("\n      ");
            for (var column = 0; column < Math.Min(columns, maxSide); column++)
            {
                builder.Append(cell(row, column));
            }
        }

        return builder.ToString();
    }

    private static string Sample(int rows, int columns, Func<int, string> element, string name)
    {
        var total = rows * columns;
        var shown = Math.Min(total, 8);
        var parts = new string[shown];
        for (var index = 0; index < shown; index++)
        {
            parts[index] = element(index);
        }

        return $"{name}[{rows},{columns}] {{ {string.Join(", ", parts)}{(total > shown ? ", ..." : string.Empty)} }}";
    }

    private static string Vector(Vector3* vector) =>
        $"({vector->x.ToString("0.###", CultureInfo.InvariantCulture)}, {vector->y.ToString("0.###", CultureInfo.InvariantCulture)}, {vector->z.ToString("0.###", CultureInfo.InvariantCulture)})";
}
