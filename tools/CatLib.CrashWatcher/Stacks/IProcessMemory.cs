namespace CatLib.CrashWatcher.Stacks;

internal interface IProcessMemory
{
    bool TryRead(ulong address, byte[] buffer, int count);

    bool IsExecutable(ulong address);
}
