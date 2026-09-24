using System;
using System.Collections.Generic;
using CatLib.Events;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Scheduling;

namespace CatLib.Tests.Suites.Events;

public sealed class SafeInvokerIsolationTest : TestCase
{
    public override string Suite => "Events";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var calls = new List<string>();
        Action handlers = () => calls.Add("first");
        handlers += () => throw new InvalidOperationException(MainThreadExceptionIsolationTest.ExpectedExceptionMessage);
        handlers += () => calls.Add("third");

        var failures = SafeInvoker.Invoke(handlers, "CatLib.Tests.SafeInvoker", context.Log);

        context.Note($"One error log line containing \"{MainThreadExceptionIsolationTest.ExpectedExceptionMessage}\" is expected");

        Assert.Equal(1, failures, "Reported failures");
        Assert.SequenceEqual(new[] { "first", "third" }, calls, "Handlers that ran");
        yield break;
    }
}
