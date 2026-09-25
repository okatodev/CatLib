using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using static CatLib.Tests.Suites.Network.NetworkFixture;

namespace CatLib.Tests.Suites.Network;

public sealed class WireFormatTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var identity = Identity(Mod("gameplay", "1.2.0", SessionPolicy.RequiredOnAll), Mod("visual", "0.1.0-beta", SessionPolicy.ClientOnly, VersionRule.Any));
        var hello = (HelloMessage)MessageCodec.Decode(MessageCodec.Encode(new HelloMessage(identity))).Payload;
        Assert.Equal(identity.GameVersion, hello.Identity.GameVersion, "Game version round trip");
        Assert.Equal(identity.CatLibVersion, hello.Identity.CatLibVersion, "CatLib version round trip");
        Assert.SequenceEqual(identity.Mods, hello.Identity.Mods, "Mods round trip");

        var settings = new[] { new SessionSettingValue("mod", "Gameplay", "Limit", "3"), new SessionSettingValue("mod", "Text", "Name", "Caf\u00e9 \U0001F431") };
        var problems = new[] { new CompatibilityProblem(ProblemKind.MissingOnClient, "gameplay", "1.2.0", null) };
        var verdict = (VerdictMessage)MessageCodec.Decode(MessageCodec.Encode(new VerdictMessage(false, problems, settings))).Payload;
        Assert.False(verdict.Accepted, "Verdict flag round trip");
        Assert.SequenceEqual(problems, verdict.Problems, "Problems round trip, including a null value");
        Assert.SequenceEqual(settings, verdict.Settings, "Settings round trip, including non-ASCII text");

        var kick = (VerdictMessage)MessageCodec.Decode(MessageCodec.Encode(new VerdictMessage(false, problems, new SessionSettingValue[0], true))).Payload;
        Assert.True(kick.Disconnecting, "Disconnect flag round trip");
        Assert.False(verdict.Disconnecting, "Disconnect flag defaults to false");

        var update = (SettingsUpdateMessage)MessageCodec.Decode(MessageCodec.Encode(new SettingsUpdateMessage(settings))).Payload;
        Assert.SequenceEqual(settings, update.Settings, "Settings update round trip");

        var announce = MessageCodec.Decode(MessageCodec.Encode(new AnnounceMessage()));
        Assert.True(announce.Payload is AnnounceMessage, "Announce round trip");
        Assert.Equal(MessageCodec.HeaderBytes, MessageCodec.Encode(new AnnounceMessage()).Length, "Announce is a bare header");

        var valid = MessageCodec.Encode(new HelloMessage(identity));
        var badMagic = (byte[])valid.Clone();
        badMagic[0] ^= 0xFF;
        var truncated = valid.Take(valid.Length - 3).ToArray();
        var trailing = valid.Concat(new byte[] { 0 }).ToArray();
        var badType = (byte[])valid.Clone();
        badType[6] = 99;
        var hugeCount = MessageCodec.Encode(new SettingsUpdateMessage(Array.Empty<SessionSettingValue>()));
        hugeCount[MessageCodec.HeaderBytes] = 0xFF;
        hugeCount[MessageCodec.HeaderBytes + 1] = 0xFF;

        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(badMagic), "Wrong magic");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(truncated), "Truncated message");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(trailing), "Trailing bytes");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(badType), "Unknown message type");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(hugeCount), "Item count over the limit");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(new byte[MessageCodec.MaxMessageBytes + 1]), "Oversized message");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(new byte[2]), "Too short for a header");
        Assert.Throws<WireFormatException>(() => MessageCodec.Encode(new HelloMessage(Identity(Mod(new string('x', 2000), "1", SessionPolicy.ClientOnly)))), "String over the limit");

        var future = (byte[])valid.Clone();
        future[4] = (byte)(MessageCodec.ProtocolVersion + 1);
        var decoded = MessageCodec.Decode(future);
        Assert.True(decoded.IsProtocolMismatch, "A newer protocol must be detected from the header alone");
        Assert.Null(decoded.Payload, "The payload of another protocol must not be parsed");
        yield break;
    }
}
