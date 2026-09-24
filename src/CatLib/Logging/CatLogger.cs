using System;
using System.Collections.Concurrent;
using BepInEx.Logging;

namespace CatLib.Logging;

public sealed class CatLogger
{
    private static readonly ConcurrentDictionary<string, ManualLogSource> Sources = new();

    private readonly ManualLogSource _source;
    private readonly string _prefix;

    private CatLogger(ManualLogSource source, string prefix)
    {
        _source = source;
        _prefix = prefix;
    }

    public string SourceName => _source.SourceName;

    public static CatLogger Create(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("Source name must not be empty.", nameof(sourceName));
        }

        return new CatLogger(Sources.GetOrAdd(sourceName, BepInEx.Logging.Logger.CreateLogSource), string.Empty);
    }

    public static CatLogger From(ManualLogSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new CatLogger(source, string.Empty);
    }

    public CatLogger Scope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return this;
        }

        return new CatLogger(_source, _prefix + "[" + scope + "] ");
    }

    public void Debug(string message) => Write(LogLevel.Debug, message);

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Message(string message) => Write(LogLevel.Message, message);

    public void Warning(string message) => Write(LogLevel.Warning, message);

    public void Error(string message) => Write(LogLevel.Error, message);

    public void Error(string message, Exception exception) => Write(LogLevel.Error, message + Environment.NewLine + exception);

    private void Write(LogLevel level, string message) => _source.Log(level, _prefix + message);
}
