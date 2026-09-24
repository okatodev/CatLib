using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using Il2CppInterop.Runtime;

namespace CatLib.Tests.Suites.Interop;

public sealed class DelegateRoundTripTest : TestCase
{
    public override string Suite => "Interop";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var parameterlessCalls = 0;
        var parameterless = DelegateSupport.ConvertDelegate<BootstrapManager.LoadHandler>(new Action(() => parameterlessCalls++));
        Assert.NotNull(parameterless, "Converted parameterless handler");
        parameterless.Invoke();
        parameterless.Invoke();
        Assert.Equal(2, parameterlessCalls, "Calls through the IL2CPP parameterless delegate");

        ulong receivedId = 0;
        string receivedName = null;
        var withArguments = DelegateSupport.ConvertDelegate<PlayerManager.ClientNameChangedHandler>(new Action<ulong, string>((id, name) =>
        {
            receivedId = id;
            receivedName = name;
        }));
        Assert.NotNull(withArguments, "Converted handler with arguments");
        withArguments.Invoke(18446744073709551615UL, "Caf\u00e9 cat \U0001F431");
        Assert.Equal(18446744073709551615UL, receivedId, "ulong argument after the IL2CPP round trip");
        Assert.Equal("Caf\u00e9 cat \U0001F431", receivedName, "string argument after the IL2CPP round trip");

        var receivedAmount = -1;
        var withInt = DelegateSupport.ConvertDelegate<PlayerManager.PlayerAmountChangedHandler>(new Action<int>(amount => receivedAmount = amount));
        Assert.NotNull(withInt, "Converted handler with an int argument");
        withInt.Invoke(-7);
        Assert.Equal(-7, receivedAmount, "int argument after the IL2CPP round trip");
        yield break;
    }
}
