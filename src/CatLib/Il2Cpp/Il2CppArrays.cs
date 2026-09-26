using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace CatLib.Il2Cpp;

public static unsafe class Il2CppArrays
{
    public const int BoundsOffset = 16;
    public const int LengthOffset = 24;
    public const int DataOffset = 32;
    public const int BoundsSize = 16;
    public const int MaxElements = 1 << 20;

    public static bool TryGetRank(Il2CppObjectBase array, out int rank, out int elementSize)
    {
        rank = 0;
        elementSize = 0;
        if (array == null || array.Pointer == IntPtr.Zero)
        {
            return false;
        }

        var klass = IL2CPP.il2cpp_object_get_class(array.Pointer);
        if (klass == IntPtr.Zero)
        {
            return false;
        }

        rank = IL2CPP.il2cpp_class_get_rank(klass);
        if (rank <= 0)
        {
            return false;
        }

        var elementClass = IL2CPP.il2cpp_class_get_element_class(klass);
        if (elementClass == IntPtr.Zero)
        {
            return false;
        }

        elementSize = IL2CPP.il2cpp_class_array_element_size(elementClass);
        return elementSize > 0;
    }

    public static bool TryGetBounds(Il2CppObjectBase array, out int rows, out int columns)
    {
        rows = 0;
        columns = 0;
        if (!TryGetRank(array, out var rank, out _) || rank != 2)
        {
            return false;
        }

        var pointer = (byte*)array.Pointer;
        var bounds = *(byte**)(pointer + BoundsOffset);
        var total = *(long*)(pointer + LengthOffset);
        if (bounds == null || total < 0 || total > MaxElements)
        {
            return false;
        }

        var first = *(long*)bounds;
        var second = *(long*)(bounds + BoundsSize);
        if (first < 0 || second < 0 || first * second != total)
        {
            return false;
        }

        rows = (int)first;
        columns = (int)second;
        return true;
    }

    public static bool TryRead2D<T>(Il2CppObjectBase array, out T[,] values) where T : unmanaged
    {
        values = null;
        if (!Fits<T>(array, out var rows, out var columns))
        {
            return false;
        }

        var data = (T*)((byte*)array.Pointer + DataOffset);
        values = new T[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                values[row, column] = data[row * columns + column];
            }
        }

        return true;
    }

    public static bool TrySet<T>(Il2CppObjectBase array, int row, int column, T value) where T : unmanaged
    {
        if (!Fits<T>(array, out var rows, out var columns) || row < 0 || column < 0 || row >= rows || column >= columns)
        {
            return false;
        }

        var data = (T*)((byte*)array.Pointer + DataOffset);
        data[row * columns + column] = value;
        return true;
    }

    private static bool Fits<T>(Il2CppObjectBase array, out int rows, out int columns) where T : unmanaged
    {
        rows = 0;
        columns = 0;
        return TryGetRank(array, out var rank, out var elementSize)
               && rank == 2
               && elementSize == sizeof(T)
               && TryGetBounds(array, out rows, out columns);
    }
}
