using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Tests.Framework;

public sealed record TestRunSummary(string Trigger, DateTime StartedAt, TimeSpan Duration, IReadOnlyList<TestResult> Results)
{
    public int Count(TestStatus status) => Results.Count(result => result.Status == status);

    public bool IsSuccessful => Results.All(result => result.Status == TestStatus.Passed);
}
