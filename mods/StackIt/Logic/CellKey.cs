namespace StackIt.Logic;

public static class CellKey
{
    public static long Of(int x, int y) => ((long)x << 32) | (uint)y;
}
