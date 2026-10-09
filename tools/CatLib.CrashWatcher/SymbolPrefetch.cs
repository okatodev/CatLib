using System;
using System.Collections.Generic;
using System.Threading;
using CatLib.CrashWatcher.Symbols;

namespace CatLib.CrashWatcher;

internal static class SymbolPrefetch
{
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    public static void Start(int processId, SymbolStore store, CodeNames names, WatcherLog log)
    {
        var thread = new Thread(() => Run(processId, store, names, log)) { IsBackground = true, Name = "CatLib symbol check", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    private static void Run(int processId, SymbolStore store, CodeNames names, WatcherLog log)
    {
        var process = NativeMethods.OpenProcess(NativeMethods.Synchronize | NativeMethods.ProcessQueryInformation | NativeMethods.ProcessVmRead, false, processId);
        if (process == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (NativeMethods.WaitForSingleObject(process, (uint)Delay.TotalMilliseconds) == 0)
            {
                return;
            }

            var wanted = new List<KeyValuePair<string, string>>();
            foreach (var module in GameDebugger.Modules(process))
            {
                var image = names.Image(module.Path);
                if (image != null && image.TryGetCodeView(out var pdbName, out var key) && SymbolStore.ServerFor(pdbName) != null)
                {
                    wanted.Add(new KeyValuePair<string, string>(pdbName, key));
                }
            }

            store.Prefetch(wanted);
        }
        catch (Exception exception)
        {
            log.Write($"Checking the symbols of the game modules failed: {exception.Message}");
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
