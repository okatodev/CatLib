using System;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Threading;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal static class CrashEvents
{
    public const string Query = "*[System[(EventID=1000 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= 600000]]]";
    public const int MaxEvents = 30;
    public const int WaitMilliseconds = 6000;
    public const int PollMilliseconds = 500;

    public static CrashEventInfo Collect(int processId, DateTime exitTimeUtc, bool expectFault, WatcherLog log)
    {
        var info = new CrashEventInfo();
        var waited = 0;
        while (true)
        {
            try
            {
                info = CrashText.ParseEvents(Read(), processId, exitTimeUtc);
            }
            catch (Exception exception)
            {
                log.Write($"Reading the Windows event log failed: {exception.Message}");
                return info;
            }

            if (info.HasFault || !expectFault || waited >= WaitMilliseconds)
            {
                return info;
            }

            Thread.Sleep(PollMilliseconds);
            waited += PollMilliseconds;
        }
    }

    public static bool LooksLikeFault(uint exitCode) => exitCode >= 0xC0000000 || exitCode == 0xE0434352 || exitCode == 0x80131623;

    private static string Read()
    {
        var builder = new StringBuilder();
        var query = new EventLogQuery("Application", PathType.LogName, Query) { ReverseDirection = true };
        using (var reader = new EventLogReader(query))
        {
            for (var count = 0; count < MaxEvents; count++)
            {
                using (var record = reader.ReadEvent())
                {
                    if (record == null)
                    {
                        break;
                    }

                    builder.Append(record.ToXml());
                }
            }
        }

        return builder.ToString();
    }
}
