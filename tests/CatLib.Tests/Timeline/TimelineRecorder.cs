using System;
using System.IO;
using BepInEx;
using CatLib.Game.Events;
using CatLib.Logging;

namespace CatLib.Tests.Timeline;

public sealed class TimelineRecorder
{
    private readonly string _directory;
    private readonly CatLogger _log;
    private StreamWriter _writer;
    private long _pendingTicks;
    private string _lastTickArguments;

    public TimelineRecorder(string directory, CatLogger log)
    {
        _directory = directory;
        _log = log;
    }

    public string FilePath { get; private set; }

    public void Start()
    {
        if (_writer != null)
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        FilePath = Path.Combine(_directory, $"timeline_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(FilePath, false) { AutoFlush = true };
        _writer.WriteLine($"CatLib timeline started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _writer.WriteLine($"CatLib {CatLib.PluginMeta.Version}, BepInEx {Paths.BepInExVersion}");
        _writer.WriteLine("     realtime    frame  event");
        GameEventStream.Raised += OnRaised;
        _log.Info($"Recording game event timeline to {FilePath}");
    }

    private void OnRaised(GameEventRecord record)
    {
        if (record.Name == NetworkEvents.NetworkTickName)
        {
            _pendingTicks++;
            _lastTickArguments = record.Arguments;
            return;
        }

        FlushTicks(record);
        Write(record.Realtime, record.Frame, record.Name, record.Arguments);
    }

    private void FlushTicks(GameEventRecord next)
    {
        if (_pendingTicks == 0)
        {
            return;
        }

        Write(next.Realtime, next.Frame, NetworkEvents.NetworkTickName, $"x{_pendingTicks} last {_lastTickArguments}");
        _pendingTicks = 0;
    }

    private void Write(double realtime, long frame, string name, string arguments)
    {
        var line = $"{realtime,13:0.000} {frame,8}  {name}{(string.IsNullOrEmpty(arguments) ? string.Empty : " " + arguments)}";
        try
        {
            _writer.WriteLine(line);
        }
        catch (Exception exception)
        {
            _log.Warning($"Failed to write timeline entry: {exception.Message}");
        }

        _log.Info(line.TrimStart());
    }
}
