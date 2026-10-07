using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionAcceptTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var host = SessionNetwork.PeersToAccept(new ulong[] { 11, 12 }, 0, false, null);
        Assert.Equal("11,12", string.Join(",", host), "The host keeps accepting every player, also after the handshake");

        var client = SessionNetwork.PeersToAccept(null, 7, false, new ulong[] { 3, 4 });
        Assert.Equal("7", string.Join(",", client), "A client keeps accepting its host after the handshake, Steam closes idle sessions and the host opens a new one");

        var waiting = SessionNetwork.PeersToAccept(null, 0, true, new ulong[] { 3, 4 });
        Assert.Equal("3,4", string.Join(",", waiting), "A client that does not know its host yet accepts every candidate");

        Assert.Equal(0, SessionNetwork.PeersToAccept(null, 0, false, new ulong[] { 3 }).Count, "Without a session nobody is accepted");
        Assert.True(SessionNetwork.PeersToAccept(new ulong[] { 5, 5, 0 }, 0, false, null).SequenceEqual(new ulong[] { 5 }), "Every player once, never the empty id");
        Assert.Equal(TimeSpan.FromSeconds(4), SessionNetwork.ShutdownDelay(TimeSpan.FromSeconds(1)), "Steam shuts down 5 s after the session with a player who left was closed");
        Assert.Equal(TimeSpan.Zero, SessionNetwork.ShutdownDelay(TimeSpan.FromSeconds(6)), "No wait when the player left long ago");
        Assert.Equal(TimeSpan.Zero, SessionNetwork.ShutdownDelay(TimeSpan.MaxValue), "No wait when nobody left");
        Assert.Equal(TimeSpan.Zero, SessionNetwork.ShutdownDelay(TimeSpan.FromSeconds(-1)), "No wait for a clock that went back");
        yield break;
    }
}
