using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionRoleTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(SessionRole.Offline, CatNetwork.ResolveRole(false, false), "No session is offline");
        Assert.Equal(SessionRole.Host, CatNetwork.ResolveRole(true, false), "Hosting, including single player");
        Assert.Equal(SessionRole.Client, CatNetwork.ResolveRole(false, true), "Joined another player");
        Assert.Equal(SessionRole.Client, CatNetwork.ResolveRole(true, true), "A joined session wins over a stale host session");
        context.Note($"Current role: {CatNetwork.Role}, authority: {CatNetwork.IsAuthority}");
        Assert.True(CatNetwork.Role != SessionRole.Client || !CatNetwork.IsAuthority, "A client never has authority");
        Assert.True(CatNetwork.Role == SessionRole.Client || CatNetwork.IsAuthority, "Offline and host players have authority");
        yield break;
    }
}
