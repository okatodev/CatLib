using System;
using System.Collections.Generic;
using CatLib.Events;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Events;

public sealed class SafeInvokerArgumentsTest : TestCase
{
    public override string Suite => "Events";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        ulong receivedId = 0;
        string receivedName = null;
        Action<ulong, string> handler = (id, name) =>
        {
            receivedId = id;
            receivedName = name;
        };

        var failures = SafeInvoker.Invoke(handler, 42UL, "cat", "CatLib.Tests.SafeInvokerArguments", context.Log);
        var nullFailures = SafeInvoker.Invoke((Action)null, "CatLib.Tests.SafeInvokerNull", context.Log);

        Assert.Equal(0, failures, "Failures with a healthy handler");
        Assert.Equal(0, nullFailures, "Failures with no handlers");
        Assert.Equal(42UL, receivedId, "First argument");
        Assert.Equal("cat", receivedName, "Second argument");
        yield break;
    }
}
