using System;
using System.Collections.Concurrent;
using CatLib.Logging;

namespace CatLib.Threading;

public static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new();
    private static int _managedThreadId = -1;

    public static bool IsInitialized => _managedThreadId != -1;

    public static int ManagedThreadId => _managedThreadId;

    public static bool IsCurrent => Environment.CurrentManagedThreadId == _managedThreadId;

    public static int PendingCount => Queue.Count;

    public static void Post(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Queue.Enqueue(action);
    }

    public static void RunOrPost(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (IsCurrent)
        {
            action();
            return;
        }

        Queue.Enqueue(action);
    }

    internal static void Initialize()
    {
        _managedThreadId = Environment.CurrentManagedThreadId;
    }

    internal static void Drain(CatLogger log)
    {
        var budget = Queue.Count;
        while (budget-- > 0 && Queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                log.Error("Main thread action failed", exception);
            }
        }
    }
}
