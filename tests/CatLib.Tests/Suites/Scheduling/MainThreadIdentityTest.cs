using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Threading;

namespace CatLib.Tests.Suites.Scheduling;

public sealed class MainThreadIdentityTest : TestCase
{
    public override string Suite => "Scheduling";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        context.Note($"Main managed thread id: {MainThread.ManagedThreadId}");

        Assert.True(MainThread.IsInitialized, "MainThread must be initialized");
        Assert.Equal(MainThread.ManagedThreadId, Environment.CurrentManagedThreadId, "Tests must run on the main thread");
        Assert.True(MainThread.IsCurrent, "MainThread.IsCurrent inside a test");
        yield break;
    }
}
