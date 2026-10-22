using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.Voice;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoiceRoutesTest : TestCase
{
    private const ulong Host = 76561198000000001UL;
    private const ulong Anna = 76561198000000002UL;
    private const ulong Boris = 76561198000000003UL;
    private const ulong Without = 76561198000000004UL;

    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var sharing = new List<ulong> { Anna, Boris };
        Assert.SequenceEqual(new[] { Anna, Boris }, VoiceRoutes.FromLocal(SessionRole.Host, 0, false, sharing), "The host talks to every player with the mod");
        Assert.SequenceEqual(new[] { Host }, VoiceRoutes.FromLocal(SessionRole.Client, Host, true, null), "A player talks to the host");
        Assert.Equal(0, VoiceRoutes.FromLocal(SessionRole.Client, Host, false, null).Count, "Not to a host without the mod");
        Assert.Equal(0, VoiceRoutes.FromLocal(SessionRole.Client, 0, true, null).Count, "Not before the host is known");
        Assert.Equal(0, VoiceRoutes.FromLocal(SessionRole.Offline, 0, false, sharing).Count, "Nobody to talk to offline");

        var fromAnna = new VoicePacket(Boris, 1, new byte[] { 1 });
        Assert.True(VoiceRoutes.Accept(SessionRole.Host, Anna, 0, Host, sharing, fromAnna, out var speaker), "The host takes voice from a player with the mod");
        Assert.Equal(Anna, speaker, "The speaker is who sent it, whatever the packet says");
        Assert.False(VoiceRoutes.Accept(SessionRole.Host, Without, 0, Host, sharing, fromAnna, out _), "The host ignores a player without the mod");
        Assert.SequenceEqual(new[] { Boris }, VoiceRoutes.Forward(SessionRole.Host, Anna, sharing), "The host passes Anna's voice on to Boris only");
        Assert.Equal(0, VoiceRoutes.Forward(SessionRole.Client, Anna, sharing).Count, "Players never pass voice on");

        var relayed = new VoicePacket(Anna, 2, new byte[] { 1 });
        Assert.True(VoiceRoutes.Accept(SessionRole.Client, Host, Host, Boris, null, relayed, out speaker), "A player takes voice the host relays");
        Assert.Equal(Anna, speaker, "and plays it as Anna");
        Assert.False(VoiceRoutes.Accept(SessionRole.Client, Anna, Host, Boris, null, relayed, out _), "A player ignores voice that does not come from the host");
        Assert.False(VoiceRoutes.Accept(SessionRole.Client, Host, Host, Anna, null, relayed, out _), "A player never plays their own voice back");
        Assert.False(VoiceRoutes.Accept(SessionRole.Offline, Host, Host, Boris, null, relayed, out _), "Nothing is taken offline");

        var rate = new VoiceRate(3);
        Assert.True(rate.Allow(Anna, 0) && rate.Allow(Anna, 0.1) && rate.Allow(Anna, 0.2), "Three packets in a second pass");
        Assert.False(rate.Allow(Anna, 0.3), "The fourth is dropped");
        Assert.True(rate.Allow(Boris, 0.3), "Another player has a limit of their own");
        Assert.True(rate.Allow(Anna, 1.1), "A new second lets voice through again");
        Assert.Equal(1, rate.Dropped, "One packet dropped");
        yield break;
    }
}
