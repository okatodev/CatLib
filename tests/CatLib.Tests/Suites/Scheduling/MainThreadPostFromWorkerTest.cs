using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CatLib.Tests.Framework;
using CatLib.Threading;

namespace CatLib.Tests.Suites.Scheduling;

public sealed class MainThreadPostFromWorkerTest : TestCase
{
    public override string Suite => "Scheduling";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var workerThreadId = -1;
        var executedThreadId = -1;
        var workerWasMainThread = true;
        var executed = false;

        Task.Run(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            workerWasMainThread = MainThread.IsCurrent;
            MainThread.Post(() =>
            {
                executedThreadId = Environment.CurrentManagedThreadId;
                executed = true;
            });
        });

        yield return Wait.Until(() => executed, 5, "the posted action to run on the main thread");

        context.Note($"Worker thread id: {workerThreadId}, executed on thread id: {executedThreadId}");

        Assert.False(workerWasMainThread, "MainThread.IsCurrent on a worker thread");
        Assert.NotEqual(MainThread.ManagedThreadId, workerThreadId, "Worker thread id");
        Assert.Equal(MainThread.ManagedThreadId, executedThreadId, "Thread that executed the posted action");
    }
}
