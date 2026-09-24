using System;
using CatLib.Logging;

namespace CatLib.Events;

public static class SafeInvoker
{
    public static int Invoke(Action handlers, string eventName, CatLogger log)
    {
        if (handlers == null)
        {
            return 0;
        }

        var failures = 0;
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                failures++;
                Report(log, eventName, handler, exception);
            }
        }

        return failures;
    }

    public static int Invoke<T1>(Action<T1> handlers, T1 arg1, string eventName, CatLogger log)
    {
        if (handlers == null)
        {
            return 0;
        }

        var failures = 0;
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1>)handler)(arg1);
            }
            catch (Exception exception)
            {
                failures++;
                Report(log, eventName, handler, exception);
            }
        }

        return failures;
    }

    public static int Invoke<T1, T2>(Action<T1, T2> handlers, T1 arg1, T2 arg2, string eventName, CatLogger log)
    {
        if (handlers == null)
        {
            return 0;
        }

        var failures = 0;
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2>)handler)(arg1, arg2);
            }
            catch (Exception exception)
            {
                failures++;
                Report(log, eventName, handler, exception);
            }
        }

        return failures;
    }

    private static void Report(CatLogger log, string eventName, Delegate handler, Exception exception)
    {
        var target = handler.Method.DeclaringType?.FullName + "." + handler.Method.Name;
        log?.Error($"Subscriber {target} of {eventName} threw an exception", exception);
    }
}
