using System;

namespace CatLib.Diagnostics;

public static class CrashTrail
{
    public static IDisposable During(string operation) => new Step(CrashWatch.BeginOperation(operation));

    public static void Run(string operation, Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        using (During(operation))
        {
            action();
        }
    }

    public static T Run<T>(string operation, Func<T> function)
    {
        if (function == null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        using (During(operation))
        {
            return function();
        }
    }

    private sealed class Step : IDisposable
    {
        private long _id;

        public Step(long id)
        {
            _id = id;
        }

        public void Dispose()
        {
            var id = _id;
            _id = 0;
            CrashWatch.EndOperation(id);
        }
    }
}
