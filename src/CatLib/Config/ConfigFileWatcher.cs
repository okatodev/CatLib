using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CatLib.Logging;

namespace CatLib.Config;

internal sealed class ConfigFileWatcher
{
    private readonly ConcurrentDictionary<string, byte> _tracked = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly CatLogger _log;

    public ConfigFileWatcher(CatLogger log)
    {
        _log = log;
    }

    public static string Normalize(string path) => Path.GetFullPath(path);

    public void Track(string path)
    {
        var normalized = Normalize(path);
        _tracked[normalized] = 0;

        var directory = Path.GetDirectoryName(normalized);
        if (string.IsNullOrEmpty(directory) || _watchers.ContainsKey(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024
            };
            watcher.Changed += (_, args) => Mark(args.FullPath);
            watcher.Created += (_, args) => Mark(args.FullPath);
            watcher.Renamed += (_, args) => Mark(args.FullPath);
            watcher.Error += (_, args) => OnError(directory, args.GetException());
            watcher.EnableRaisingEvents = true;
            _watchers[directory] = watcher;
            _log.Debug($"Watching {directory}");
        }
        catch (Exception exception)
        {
            _log.Error($"Could not watch {directory}, live reload is unavailable for files in it", exception);
        }
    }

    public void Untrack(string path)
    {
        var normalized = Normalize(path);
        _tracked.TryRemove(normalized, out _);
        _pending.TryRemove(normalized, out _);
    }

    public void Mark(string path)
    {
        string normalized;
        try
        {
            normalized = Normalize(path);
        }
        catch (Exception)
        {
            return;
        }

        if (_tracked.ContainsKey(normalized))
        {
            _pending[normalized] = Stopwatch.GetTimestamp();
        }
    }

    public List<string> TakeDue(TimeSpan debounce)
    {
        var due = new List<string>();
        if (_pending.IsEmpty)
        {
            return due;
        }

        var threshold = Stopwatch.GetTimestamp() - (long)(debounce.TotalSeconds * Stopwatch.Frequency);
        foreach (var pair in _pending)
        {
            if (pair.Value <= threshold && _pending.TryRemove(pair))
            {
                due.Add(pair.Key);
            }
        }

        return due;
    }

    private void OnError(string directory, Exception exception)
    {
        _log.Warning($"File watcher error in {directory}: {exception?.Message}. Rechecking all files in it");
        foreach (var path in _tracked.Keys.Where(path => string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase)))
        {
            _pending[path] = Stopwatch.GetTimestamp();
        }
    }
}
