using System;

namespace CatLib.Tests.Suites.Presentation;

public enum SampleMode
{
    FastMode,
    SlowMode,
    HTTPMode
}

[Flags]
public enum SampleFlags
{
    None = 0,
    First = 1,
    Second = 2
}
