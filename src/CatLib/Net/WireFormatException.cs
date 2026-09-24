using System;

namespace CatLib.Net;

public sealed class WireFormatException : Exception
{
    public WireFormatException(string message) : base(message)
    {
    }
}
