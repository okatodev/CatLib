using System;

namespace CatLib.CrashWatcher.Stacks;

internal sealed class RegisterSet
{
    public const int StackPointer = 4;
    public const int FramePointer = 5;
    public const int Count = 16;
    public const int AllStale = 0xFFFF & ~(1 << StackPointer);

    public ulong[] Integer { get; } = new ulong[Count];

    public ulong Rip { get; set; }

    public int Stale { get; set; }

    public ulong Rsp
    {
        get => Integer[StackPointer];
        set => Integer[StackPointer] = value;
    }

    public bool IsStale(int register) => (Stale & (1 << register)) != 0;

    public void Restore(int register, ulong value)
    {
        Integer[register] = value;
        Stale &= ~(1 << register);
    }

    public RegisterSet Copy()
    {
        var copy = new RegisterSet { Rip = Rip, Stale = Stale };
        Array.Copy(Integer, copy.Integer, Count);
        return copy;
    }
}
