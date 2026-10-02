using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal sealed class DebugOutcome
{
    public GameExit Exit { get; set; }

    public CrashEventInfo Crash { get; set; }

    public string DumpPath { get; set; }

    public int PassedExceptions { get; set; }

    public int EarlyDumps { get; set; }
}

internal static class GameDebugger
{
    public const uint DumpType = 0x0004 | 0x0020 | 0x0040 | 0x0100 | 0x0800 | 0x1000;
    public const uint BreakpointCode = 0x80000003;
    public const uint Wow64BreakpointCode = 0x4000001F;
    public const int MaxEarlyDumps = 10;
    public static readonly TimeSpan EarlyDumpPause = TimeSpan.FromSeconds(5);
    public static readonly uint[] FatalCodes = { 0xC0000005, 0xC000001D, 0xC0000096, 0xC00000FD, 0xC0000374, 0xC0000409, 0xC0000420 };

    public static DebugOutcome Watch(int processId, string dumpPath, WatcherLog log)
    {
        var access = NativeMethods.Synchronize | NativeMethods.ProcessQueryLimitedInformation | NativeMethods.ProcessQueryInformation |
                     NativeMethods.ProcessVmRead | NativeMethods.ProcessDuplicateHandle;
        var process = NativeMethods.OpenProcess(access, false, processId);
        if (process == IntPtr.Zero)
        {
            log.Write($"Could not open the game for dumps ({new Win32Exception().Message}), waiting without them");
            return null;
        }

        if (!NativeMethods.DebugActiveProcess(processId))
        {
            log.Write($"Could not follow the game as a debugger ({new Win32Exception().Message}), waiting without dumps");
            NativeMethods.CloseHandle(process);
            return null;
        }

        NativeMethods.DebugSetProcessKillOnExit(false);
        var outcome = new DebugOutcome { Exit = new GameExit { ImagePath = GameProcess.ImagePath(process) } };
        var debugEvent = Marshal.AllocHGlobal(NativeMethods.DebugEventSize);
        var initialBreak = false;
        var finished = false;
        var earlyPath = string.IsNullOrEmpty(dumpPath) ? null : Path.ChangeExtension(dumpPath, ".early.dmp");
        CrashEventInfo early = null;
        var earlyAt = DateTime.MinValue;
        try
        {
            while (!finished)
            {
                if (!NativeMethods.WaitForDebugEvent(debugEvent, NativeMethods.Infinite))
                {
                    log.Write($"Following the game stopped ({new Win32Exception().Message}), waiting without dumps");
                    NativeMethods.DebugActiveProcessStop(processId);
                    break;
                }

                var code = Marshal.ReadInt32(debugEvent, 0);
                var eventProcess = Marshal.ReadInt32(debugEvent, 4);
                var thread = Marshal.ReadInt32(debugEvent, 8);
                var status = NativeMethods.DebugContinue;
                switch (code)
                {
                    case NativeMethods.ExceptionDebugEvent:
                        var exceptionCode = unchecked((uint)Marshal.ReadInt32(debugEvent, 16));
                        var firstChance = Marshal.ReadInt32(debugEvent, 16 + NativeMethods.ExceptionRecordSize) != 0;
                        if (firstChance && !initialBreak && (exceptionCode == BreakpointCode || exceptionCode == Wow64BreakpointCode))
                        {
                            initialBreak = true;
                        }
                        else if (firstChance)
                        {
                            status = NativeMethods.DebugExceptionNotHandled;
                            outcome.PassedExceptions++;
                            if (earlyPath != null && outcome.Crash == null && Array.IndexOf(FatalCodes, exceptionCode) >= 0 &&
                                outcome.EarlyDumps < MaxEarlyDumps && DateTime.UtcNow - earlyAt >= EarlyDumpPause)
                            {
                                var candidate = Describe(process, thread, exceptionCode, debugEvent, log);
                                if (!CrashText.IsManaged(candidate))
                                {
                                    earlyAt = DateTime.UtcNow;
                                    outcome.EarlyDumps++;
                                    early = candidate;
                                    early.Early = true;
                                    log.Write($"The game raised {CrashText.Hex(exceptionCode)} in {early.Module} + {early.Offset} on thread {thread}, keeping a dump in case it does not recover");
                                    if (!TryWriteDump(process, processId, thread, debugEvent, earlyPath, log))
                                    {
                                        early = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            status = NativeMethods.DebugExceptionNotHandled;
                            if (outcome.Crash == null)
                            {
                                outcome.Crash = Describe(process, thread, exceptionCode, debugEvent, log);
                                log.Write($"The game crashed with {CrashText.Hex(exceptionCode)} in {outcome.Crash.Module} + {outcome.Crash.Offset} on thread {thread}");
                                if (!string.IsNullOrEmpty(dumpPath) && TryWriteDump(process, processId, thread, debugEvent, dumpPath, log))
                                {
                                    outcome.DumpPath = dumpPath;
                                }
                            }
                        }

                        break;
                    case NativeMethods.CreateProcessDebugEvent:
                    case NativeMethods.LoadDllDebugEvent:
                        var file = Marshal.ReadIntPtr(debugEvent, 16);
                        if (file != IntPtr.Zero)
                        {
                            NativeMethods.CloseHandle(file);
                        }

                        break;
                    case NativeMethods.ExitProcessDebugEvent:
                        outcome.Exit.ExitCode = unchecked((uint)Marshal.ReadInt32(debugEvent, 16));
                        finished = true;
                        break;
                }

                NativeMethods.ContinueDebugEvent(eventProcess, thread, status);
            }

            if (!finished)
            {
                NativeMethods.WaitForSingleObject(process, NativeMethods.Infinite);
                if (NativeMethods.GetExitCodeProcess(process, out var exitCode))
                {
                    outcome.Exit.ExitCode = exitCode;
                }
            }

            NativeMethods.WaitForSingleObject(process, 5000);
            GameProcess.FillTimes(process, outcome.Exit);
            if (outcome.Crash == null && early != null && outcome.Exit.ExitCode == ParseCode(early.ExceptionCode))
            {
                outcome.Crash = early;
                if (Move(earlyPath, dumpPath, log))
                {
                    outcome.DumpPath = dumpPath;
                }

                log.Write($"The game closed with the exception it raised in {early.Module} + {early.Offset}, the dump of that moment is the crash dump");
            }

            return outcome;
        }
        finally
        {
            Marshal.FreeHGlobal(debugEvent);
            NativeMethods.CloseHandle(process);
            if (earlyPath != null && File.Exists(earlyPath))
            {
                try
                {
                    File.Delete(earlyPath);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static uint ParseCode(string code)
    {
        var text = (code ?? string.Empty).Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(2);
        }

        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static bool Move(string source, string target, WatcherLog log)
    {
        try
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(source, target);
            return true;
        }
        catch (Exception exception)
        {
            log.Write($"Could not keep the dump of the raised exception: {exception.Message}");
            return false;
        }
    }

    public static List<CrashModule> Modules(IntPtr process)
    {
        var modules = new List<CrashModule>();
        var handles = new IntPtr[1024];
        if (!NativeMethods.EnumProcessModulesEx(process, handles, (uint)(handles.Length * IntPtr.Size), out var needed, NativeMethods.ListModulesAll))
        {
            return modules;
        }

        var count = Math.Min(handles.Length, (int)(needed / (uint)IntPtr.Size));
        var name = new StringBuilder(1024);
        for (var index = 0; index < count; index++)
        {
            if (!NativeMethods.GetModuleInformation(process, handles[index], out var info, (uint)Marshal.SizeOf(typeof(NativeMethods.ModuleInfo))))
            {
                continue;
            }

            name.Clear();
            NativeMethods.GetModuleFileNameEx(process, handles[index], name, (uint)name.Capacity);
            modules.Add(new CrashModule(name.ToString(), unchecked((ulong)info.BaseOfDll.ToInt64()), info.SizeOfImage));
        }

        return modules;
    }

    private static CrashEventInfo Describe(IntPtr process, int thread, uint exceptionCode, IntPtr debugEvent, WatcherLog log)
    {
        var address = unchecked((ulong)Marshal.ReadInt64(debugEvent, 16 + 16));
        CrashEventInfo info;
        try
        {
            info = CrashText.Locate(Modules(process), address, exceptionCode, thread);
        }
        catch (Exception exception)
        {
            info = CrashText.Locate(null, address, exceptionCode, thread);
            log.Write($"Could not list the game modules: {exception.Message}");
        }

        var parameters = Marshal.ReadInt32(debugEvent, 16 + 24);
        if (exceptionCode == 0xC0000005 && parameters >= 2)
        {
            info.Access = CrashText.AccessText(unchecked((ulong)Marshal.ReadInt64(debugEvent, 16 + 32)), unchecked((ulong)Marshal.ReadInt64(debugEvent, 16 + 40)));
        }

        return info;
    }

    public static bool TryWriteSnapshot(int processId, string path, WatcherLog log)
    {
        var access = NativeMethods.ProcessQueryInformation | NativeMethods.ProcessVmRead | NativeMethods.ProcessDuplicateHandle;
        var process = NativeMethods.OpenProcess(access, false, processId);
        if (process == IntPtr.Zero)
        {
            log.Write($"Could not open the game for a dump of the hang ({new Win32Exception().Message})");
            return false;
        }

        try
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                if (!NativeMethods.MiniDumpWriteDump(process, processId, stream.SafeFileHandle.DangerousGetHandle(), DumpType, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception();
                }
            }

            log.Write($"Memory dump of the hang written to {path}, {new FileInfo(path).Length / 1024} KiB");
            return true;
        }
        catch (Exception exception)
        {
            log.Write($"The memory dump of the hang could not be written: {exception.Message}");
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
            }

            return false;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static bool TryWriteDump(IntPtr process, int processId, int thread, IntPtr debugEvent, string path, WatcherLog log)
    {
        try
        {
            WriteDump(process, processId, thread, debugEvent, path);
            log.Write($"Memory dump written to {path}, {new FileInfo(path).Length / 1024} KiB");
            return true;
        }
        catch (Exception exception)
        {
            log.Write($"The memory dump could not be written: {exception.Message}");
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
            }

            return false;
        }
    }

    private static void WriteDump(IntPtr process, int processId, int thread, IntPtr debugEvent, string dumpPath)
    {
        var record = Marshal.AllocHGlobal(NativeMethods.ExceptionRecordSize);
        var contextMemory = Marshal.AllocHGlobal(NativeMethods.ContextSize + 16);
        var pointers = Marshal.AllocHGlobal(2 * IntPtr.Size);
        var information = Marshal.AllocHGlobal(16);
        var threadHandle = NativeMethods.OpenThread(NativeMethods.ThreadGetContext | NativeMethods.ThreadQueryInformation, false, thread);
        try
        {
            var bytes = new byte[NativeMethods.ExceptionRecordSize];
            Marshal.Copy(debugEvent + 16, bytes, 0, bytes.Length);
            Array.Clear(bytes, 8, 8);
            Marshal.Copy(bytes, 0, record, bytes.Length);

            var context = new IntPtr((contextMemory.ToInt64() + 15) & ~15L);
            Marshal.Copy(new byte[NativeMethods.ContextSize], 0, context, NativeMethods.ContextSize);
            Marshal.WriteInt32(context, NativeMethods.ContextFlagsOffset, NativeMethods.ContextAll);
            var hasContext = threadHandle != IntPtr.Zero && NativeMethods.GetThreadContext(threadHandle, context);

            Marshal.WriteIntPtr(pointers, 0, record);
            Marshal.WriteIntPtr(pointers, IntPtr.Size, hasContext ? context : IntPtr.Zero);
            Marshal.WriteInt32(information, 0, thread);
            Marshal.WriteInt64(information, 4, pointers.ToInt64());
            Marshal.WriteInt32(information, 12, 0);

            using (var stream = new FileStream(dumpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                if (!NativeMethods.MiniDumpWriteDump(process, processId, stream.SafeFileHandle.DangerousGetHandle(), DumpType,
                        hasContext ? information : IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception();
                }
            }
        }
        finally
        {
            if (threadHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(threadHandle);
            }

            Marshal.FreeHGlobal(information);
            Marshal.FreeHGlobal(pointers);
            Marshal.FreeHGlobal(contextMemory);
            Marshal.FreeHGlobal(record);
        }
    }
}
