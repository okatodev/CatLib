using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CatLib.Core;
using CatLib.Events;

namespace CatLib.Game.Events;

public static class GameEventStream
{
    private static readonly ConcurrentDictionary<string, long> Counters = new();

    public static event Action<GameEventRecord> Raised;

    public static bool HasListeners => Raised != null;

    public static long CountOf(string eventName) => Counters.TryGetValue(eventName, out var count) ? count : 0;

    public static IReadOnlyList<KeyValuePair<string, long>> SnapshotCounters() =>
        Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();

    internal static void Publish(string eventName) => Publish(eventName, string.Empty);

    internal static void Publish(string eventName, string arguments)
    {
        Counters.AddOrUpdate(eventName, 1, (_, count) => count + 1);

        var handlers = Raised;
        if (handlers == null)
        {
            return;
        }

        var record = new GameEventRecord(eventName, arguments ?? string.Empty, FrameLoop.FrameCount, FrameLoop.Realtime);
        SafeInvoker.Invoke(handlers, record, "GameEventStream.Raised", CatLibRuntime.Log);
    }
}
