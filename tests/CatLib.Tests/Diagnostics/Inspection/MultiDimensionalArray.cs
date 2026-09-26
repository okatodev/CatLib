using System;
using System.Globalization;
using System.Text;
using CatLib.Il2Cpp;
using UnityEngine;

namespace CatLib.Tests.Diagnostics.Inspection;

public static class MultiDimensionalArray
{
    public const int SampleSize = 8;

    public static bool TryFormat(Il2CppSystem.Object value, int maxSide, out string text)
    {
        text = null;
        if (!Il2CppArrays.TryGetRank(value, out var rank, out _) || rank != 2)
        {
            return false;
        }

        var typeName = value.GetIl2CppType().FullName ?? "?";
        var elementType = typeName.EndsWith("[,]", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 3) : typeName;
        if (!Il2CppArrays.TryGetBounds(value, out var rows, out var columns))
        {
            text = $"<{typeName} unreadable>";
            return true;
        }

        text = elementType switch
        {
            "System.Boolean" when Il2CppArrays.TryRead2D<bool>(value, out var cells) => Grid(cells, maxSide, $"bool[{rows},{columns}] (# = true)"),
            "System.Int32" when Il2CppArrays.TryRead2D<int>(value, out var numbers) => Sample(numbers, number => number.ToString(CultureInfo.InvariantCulture), "int"),
            "System.Single" when Il2CppArrays.TryRead2D<float>(value, out var floats) => Sample(floats, number => number.ToString("0.###", CultureInfo.InvariantCulture), "float"),
            "UnityEngine.Vector3" when Il2CppArrays.TryRead2D<Vector3>(value, out var vectors) => Sample(vectors, Vector, "Vector3"),
            _ => $"{elementType}[{rows},{columns}]"
        };
        return true;
    }

    private static string Grid(bool[,] cells, int maxSide, string title)
    {
        var builder = new StringBuilder(title);
        for (var row = 0; row < Math.Min(cells.GetLength(0), maxSide); row++)
        {
            builder.Append("\n      ");
            for (var column = 0; column < Math.Min(cells.GetLength(1), maxSide); column++)
            {
                builder.Append(cells[row, column] ? '#' : '.');
            }
        }

        return builder.ToString();
    }

    private static string Sample<T>(T[,] values, Func<T, string> format, string name)
    {
        var rows = values.GetLength(0);
        var columns = values.GetLength(1);
        var total = rows * columns;
        var parts = new string[Math.Min(total, SampleSize)];
        for (var index = 0; index < parts.Length; index++)
        {
            parts[index] = format(values[index / columns, index % columns]);
        }

        return $"{name}[{rows},{columns}] {{ {string.Join(", ", parts)}{(total > parts.Length ? ", ..." : string.Empty)} }}";
    }

    private static string Vector(Vector3 vector) =>
        $"({vector.x.ToString("0.###", CultureInfo.InvariantCulture)}, {vector.y.ToString("0.###", CultureInfo.InvariantCulture)}, {vector.z.ToString("0.###", CultureInfo.InvariantCulture)})";
}
