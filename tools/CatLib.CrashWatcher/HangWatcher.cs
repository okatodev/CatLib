using System;
using System.IO;
using System.Threading;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal sealed class HangWatcher
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    private readonly int _processId;
    private readonly string _sessionFile;
    private readonly string _dumpPath;
    private readonly WatcherLog _log;
    private readonly ManualResetEvent _stop = new ManualResetEvent(false);
    private Thread _thread;
    private long _seenLength = -1;

    public HangWatcher(int processId, string sessionFile, string dumpPath, WatcherLog log)
    {
        _processId = processId;
        _sessionFile = sessionFile;
        _dumpPath = dumpPath;
        _log = log;
    }

    public DateTime? QuitStarted { get; private set; }

    public bool Hung { get; private set; }

    public string DumpPath { get; private set; }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "CatLib hang watch" };
        _thread.Start();
    }

    public void Stop()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromMinutes(2));
    }

    public int HungSeconds(DateTime exited) =>
        Hung && QuitStarted.HasValue ? Math.Max((int)Limit.TotalSeconds, (int)Math.Round((exited - QuitStarted.Value).TotalSeconds)) : 0;

    private void Run()
    {
        try
        {
            while (!_stop.WaitOne(Poll))
            {
                if (!QuitStarted.HasValue)
                {
                    if (IsQuitting())
                    {
                        QuitStarted = DateTime.Now;
                        _log.Write($"The game (process {_processId}) began to quit, it is reported as hung if it still runs in {Limit.TotalSeconds} s");
                    }

                    continue;
                }

                if (DateTime.Now - QuitStarted.Value < Limit)
                {
                    continue;
                }

                Hung = true;
                _log.Write($"The game (process {_processId}) still runs {Limit.TotalSeconds} s after it began to quit, it looks hung");
                if (_dumpPath != null && GameDebugger.TryWriteSnapshot(_processId, _dumpPath, _log))
                {
                    DumpPath = _dumpPath;
                }

                return;
            }
        }
        catch (Exception exception)
        {
            _log.Write($"Watching the game for a hang failed: {exception.Message}");
        }
    }

    private bool IsQuitting()
    {
        try
        {
            var info = new FileInfo(_sessionFile);
            if (!info.Exists || info.Length == _seenLength)
            {
                return false;
            }

            _seenLength = info.Length;
            var prefix = CrashSession.CleanKey + "=";
            foreach (var line in ReportWriter.ReadLines(_sessionFile))
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
        }

        return false;
    }
}
