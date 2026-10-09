using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CatLib.CrashWatcher.Stacks;

internal sealed class LiveProcessMemory : IProcessMemory
{
    private readonly IntPtr _process;
    private readonly Dictionary<ulong, bool> _executablePages = new Dictionary<ulong, bool>();

    public LiveProcessMemory(IntPtr process)
    {
        _process = process;
    }

    public bool TryRead(ulong address, byte[] buffer, int count)
    {
        if (address == 0 || count <= 0 || count > buffer.Length)
        {
            return false;
        }

        return NativeMethods.ReadProcessMemory(_process, new IntPtr(unchecked((long)address)), buffer, new IntPtr(count), out var read) && read.ToInt64() == count;
    }

    public bool IsExecutable(ulong address)
    {
        var page = address & ~0xFFFUL;
        if (_executablePages.TryGetValue(page, out var known))
        {
            return known;
        }

        var executable = false;
        var info = Marshal.AllocHGlobal(NativeMethods.MemoryInfoSize);
        try
        {
            if (NativeMethods.VirtualQueryEx(_process, new IntPtr(unchecked((long)address)), info, new IntPtr(NativeMethods.MemoryInfoSize)) != IntPtr.Zero)
            {
                var state = unchecked((uint)Marshal.ReadInt32(info, 32));
                var protect = unchecked((uint)Marshal.ReadInt32(info, 36));
                executable = state == NativeMethods.MemoryCommit && (protect & NativeMethods.ExecutableProtection) != 0;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(info);
        }

        _executablePages[page] = executable;
        return executable;
    }
}
