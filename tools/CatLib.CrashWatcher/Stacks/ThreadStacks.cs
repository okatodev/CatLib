using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CatLib.CrashWatcher.Symbols;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher.Stacks;

internal static class ThreadStacks
{
    public const int MaxThreads = 512;
    public const int MaxFrames = 48;
    public const int MaxNameLength = 64;

    public static List<CrashThreadStack> Capture(IntPtr process, int processId, CodeNames names, bool suspend, int firstThread, WatcherLog log)
    {
        var stacks = new List<CrashThreadStack>();
        if (names == null)
        {
            return stacks;
        }

        var clock = Stopwatch.StartNew();
        var threads = new List<KeyValuePair<int, IntPtr>>();
        var suspended = new List<IntPtr>();
        var walked = new List<KeyValuePair<CrashThreadStack, List<UnwoundFrame>>>();
        ModuleSet modules = null;
        try
        {
            modules = new ModuleSet(GameDebugger.Modules(process));
            var unwinder = new StackUnwinder(new LiveProcessMemory(process), modules, names);
            var access = NativeMethods.ThreadGetContext | NativeMethods.ThreadQueryInformation | NativeMethods.ThreadQueryLimitedInformation |
                         (suspend ? NativeMethods.ThreadSuspendResume : 0);
            foreach (var id in ThreadIds(processId))
            {
                var handle = NativeMethods.OpenThread(access, false, id);
                if (handle != IntPtr.Zero)
                {
                    threads.Add(new KeyValuePair<int, IntPtr>(id, handle));
                }
            }

            var first = threads.FindIndex(thread => thread.Key == firstThread);
            if (first > 0)
            {
                var crashed = threads[first];
                threads.RemoveAt(first);
                threads.Insert(0, crashed);
            }

            if (suspend)
            {
                foreach (var thread in threads)
                {
                    if (NativeMethods.SuspendThread(thread.Value) != NativeMethods.SuspendFailed)
                    {
                        suspended.Add(thread.Value);
                    }
                }
            }

            foreach (var thread in threads)
            {
                var stack = new CrashThreadStack { ThreadId = thread.Key };
                var frames = new List<UnwoundFrame>();
                try
                {
                    var registers = Registers(thread.Value);
                    if (registers == null)
                    {
                        stack.Note = "its registers could not be read";
                    }
                    else
                    {
                        frames = unwinder.Walk(registers, MaxFrames);
                        stack.Note = unwinder.LastStop == StackUnwinder.EndOfStack ? string.Empty : unwinder.LastStop;
                    }
                }
                catch (Exception exception)
                {
                    stack.Note = "walking it failed: " + exception.Message;
                }

                walked.Add(new KeyValuePair<CrashThreadStack, List<UnwoundFrame>>(stack, frames));
            }
        }
        catch (Exception exception)
        {
            log.Write($"Reading the stacks of the game threads failed: {exception.Message}");
        }
        finally
        {
            foreach (var handle in suspended)
            {
                NativeMethods.ResumeThread(handle);
            }
        }

        for (var index = 0; index < walked.Count; index++)
        {
            var stack = walked[index].Key;
            stack.Name = Name(threads[index].Value);
            foreach (var frame in walked[index].Value)
            {
                stack.Frames.Add(FrameText(modules, names, frame, out var gameMethod));
                if (stack.GameMethod.Length == 0 && gameMethod != null)
                {
                    stack.GameMethod = gameMethod;
                }
            }

            stacks.Add(stack);
        }

        foreach (var thread in threads)
        {
            NativeMethods.CloseHandle(thread.Value);
        }

        log.Write($"Stacks of {stacks.Count} game threads read in {clock.ElapsedMilliseconds} ms" +
                  (suspend ? $", the game was paused for {suspended.Count} threads" : string.Empty) +
                  (names.HasGameMethods ? ", with game method names" : string.Empty));
        return stacks;
    }

    public static string FrameText(ModuleSet modules, CodeNames names, UnwoundFrame frame, out string gameMethod)
    {
        gameMethod = null;
        var mark = frame.Scanned ? "? " : string.Empty;
        if (frame.NotCode)
        {
            return mark + CrashText.Hex64(frame.Address) + "  not code: the thread called or jumped to a bad address, the frame below made that call";
        }

        var module = modules.Find(frame.Address);
        if (module == null)
        {
            return mark + CrashText.Hex64(frame.Address) + "  not in a module: JIT-compiled .NET code of BepInEx or a mod, or other generated code";
        }

        var offset = frame.Address - module.Start;
        var where = module.Name + " + " + CodeNames.Offset(offset);
        string name;
        var isGameMethod = false;
        try
        {
            name = names.Name(module.Path, offset, !frame.Interrupted, out isGameMethod);
        }
        catch (Exception)
        {
            name = null;
        }

        if (name != null && isGameMethod && !frame.Scanned)
        {
            gameMethod = name;
        }

        return mark + (name == null ? where : where.PadRight(34) + "  " + name);
    }

    private static List<int> ThreadIds(int processId)
    {
        var ids = new List<int>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.SnapThreads, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            throw new Win32Exception();
        }

        var entry = Marshal.AllocHGlobal(NativeMethods.ThreadEntrySize);
        try
        {
            Marshal.WriteInt32(entry, 0, NativeMethods.ThreadEntrySize);
            var more = NativeMethods.Thread32First(snapshot, entry);
            while (more && ids.Count < MaxThreads)
            {
                if (Marshal.ReadInt32(entry, 12) == processId)
                {
                    ids.Add(Marshal.ReadInt32(entry, 8));
                }

                Marshal.WriteInt32(entry, 0, NativeMethods.ThreadEntrySize);
                more = NativeMethods.Thread32Next(snapshot, entry);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(entry);
            NativeMethods.CloseHandle(snapshot);
        }

        return ids;
    }

    private static RegisterSet Registers(IntPtr thread)
    {
        var memory = Marshal.AllocHGlobal(NativeMethods.ContextSize + 16);
        try
        {
            var context = new IntPtr((memory.ToInt64() + 15) & ~15L);
            Marshal.Copy(new byte[NativeMethods.ContextSize], 0, context, NativeMethods.ContextSize);
            Marshal.WriteInt32(context, NativeMethods.ContextFlagsOffset, NativeMethods.ContextControlInteger);
            if (!NativeMethods.GetThreadContext(thread, context))
            {
                return null;
            }

            var registers = new RegisterSet { Rip = unchecked((ulong)Marshal.ReadInt64(context, NativeMethods.ContextRipOffset)) };
            for (var index = 0; index < RegisterSet.Count; index++)
            {
                registers.Integer[index] = unchecked((ulong)Marshal.ReadInt64(context, NativeMethods.ContextIntegerOffset + index * 8));
            }

            return registers;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static string Name(IntPtr thread)
    {
        try
        {
            if (NativeMethods.GetThreadDescription(thread, out var text) < 0 || text == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                var name = (Marshal.PtrToStringUni(text) ?? string.Empty).Trim();
                return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
            }
            finally
            {
                NativeMethods.LocalFree(text);
            }
        }
        catch (EntryPointNotFoundException)
        {
            return string.Empty;
        }
    }
}
