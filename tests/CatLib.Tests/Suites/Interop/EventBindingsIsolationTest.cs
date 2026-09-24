using System;
using System.Collections.Generic;
using CatLib.Il2Cpp;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Scheduling;

namespace CatLib.Tests.Suites.Interop;

public sealed class EventBindingsIsolationTest : TestCase
{
    public override string Suite => "Interop";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var added = new List<string>();
        var removed = new List<string>();
        var bindings = new Il2CppEventBindings();

        var failingResult = bindings.Add<BootstrapManager.LoadHandler>("CatLib.Tests.Failing", new Action(() => { }),
            _ => throw new InvalidOperationException(MainThreadExceptionIsolationTest.ExpectedExceptionMessage),
            _ => removed.Add("failing"));
        var healthyResult = bindings.Add<BootstrapManager.LoadHandler>("CatLib.Tests.Healthy", new Action(() => { }),
            handler => added.Add(handler == null ? "null" : "healthy"),
            _ => removed.Add("healthy"));

        Assert.False(failingResult, "Result of a binding whose add accessor throws");
        Assert.True(healthyResult, "Result of a healthy binding added after a failing one");
        Assert.SequenceEqual(new[] { "healthy" }, added, "Handlers passed to add accessors");
        Assert.Equal(1, bindings.Count, "Active bindings");
        Assert.Equal(1, bindings.Failures.Count, "Recorded failures");
        Assert.True(bindings.Failures[0].Contains(MainThreadExceptionIsolationTest.ExpectedExceptionMessage), "Failure text must contain the original exception");

        bindings.Clear(context.Log);

        Assert.SequenceEqual(new[] { "healthy" }, removed, "Remove accessors called by Clear");
        Assert.Equal(0, bindings.Count, "Active bindings after Clear");
        Assert.Equal(0, bindings.Failures.Count, "Recorded failures after Clear");
        yield break;
    }
}
