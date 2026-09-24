using System;

namespace CatLib.Tests.Framework;

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message)
    {
    }
}
