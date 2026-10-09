namespace CatLib.CrashWatcher.Stacks;

internal sealed class UnwoundFrame
{
    public UnwoundFrame(ulong address, ulong stackPointer, bool scanned, bool interrupted, bool notCode = false)
    {
        NotCode = notCode;
        Address = address;
        StackPointer = stackPointer;
        Scanned = scanned;
        Interrupted = interrupted;
    }

    public ulong Address { get; }

    public ulong StackPointer { get; }

    public bool Scanned { get; }

    public bool Interrupted { get; }

    public bool NotCode { get; }
}
