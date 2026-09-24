using System;
using System.Collections.Generic;

namespace CatLib.Tests.Framework;

public sealed record TestResult(string Suite, string Name, TestStatus Status, TimeSpan Duration, long Frames, string Message, IReadOnlyList<string> Notes)
{
    public string FullName => Suite + "/" + Name;
}
