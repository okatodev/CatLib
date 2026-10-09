namespace CatLib.CrashWatcher.Symbols;

internal readonly struct RuntimeFunction
{
    public RuntimeFunction(uint begin, uint end, uint unwindInfo)
    {
        Begin = begin;
        End = end;
        UnwindInfo = unwindInfo;
    }

    public uint Begin { get; }

    public uint End { get; }

    public uint UnwindInfo { get; }
}
