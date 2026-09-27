using System;
using System.ComponentModel;
using System.Text;

namespace CatLib.CrashWatcher;

internal sealed class GameExit
{
    public uint ExitCode { get; set; }

    public DateTime Started { get; set; }

    public DateTime Exited { get; set; }

    public string ImagePath { get; set; } = string.Empty;

    public TimeSpan Played => Exited > Started ? Exited - Started : TimeSpan.Zero;
}

internal static class GameProcess
{
    public static GameExit WaitForExit(int processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.Synchronize | NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            var exit = new GameExit { ImagePath = ImagePath(handle) };
            NativeMethods.WaitForSingleObject(handle, NativeMethods.Infinite);
            if (!NativeMethods.GetExitCodeProcess(handle, out var code))
            {
                throw new Win32Exception();
            }

            exit.ExitCode = code;
            if (NativeMethods.GetProcessTimes(handle, out var created, out var exited, out _, out _))
            {
                exit.Started = DateTime.FromFileTimeUtc(created).ToLocalTime();
                exit.Exited = DateTime.FromFileTimeUtc(exited).ToLocalTime();
            }
            else
            {
                exit.Exited = DateTime.Now;
            }

            return exit;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static string ImagePath(IntPtr handle)
    {
        var size = 1024u;
        var builder = new StringBuilder((int)size);
        return NativeMethods.QueryFullProcessImageName(handle, 0, builder, ref size) ? builder.ToString() : string.Empty;
    }
}
