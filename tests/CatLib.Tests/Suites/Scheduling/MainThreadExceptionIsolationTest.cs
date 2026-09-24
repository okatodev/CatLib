using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Threading;

namespace CatLib.Tests.Suites.Scheduling;

public sealed class MainThreadExceptionIsolationTest : TestCase
{
    public const string ExpectedExceptionMessage = "Expected exception thrown by CatLib.Tests";

    public override string Suite => "Scheduling";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var executedAfterFailure = false;

        MainThread.Post(() => throw new InvalidOperationException(ExpectedExceptionMessage));
        MainThread.Post(() => executedAfterFailure = true);

        yield return Wait.Until(() => executedAfterFailure, 5, "the action queued after a throwing action");

        context.Note($"One error log line containing \"{ExpectedExceptionMessage}\" is expected");
    }
}
