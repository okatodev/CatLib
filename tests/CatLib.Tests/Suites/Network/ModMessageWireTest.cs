using System;
using System.Collections.Generic;
using System.Text;
using CatLib.Net;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Network;

public sealed class ModMessageWireTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var data = Encoding.UTF8.GetBytes("390/1=4");
        var decoded = MessageCodec.Decode(MessageCodec.Encode(new ModMessageData("catlib.labels", "set", data)));
        Assert.Equal(MessageType.Mod, decoded.Type, "Type");
        var message = (ModMessageData)decoded.Payload;
        Assert.Equal("catlib.labels", message.ModId, "Mod id");
        Assert.Equal("set", message.Name, "Name");
        Assert.Equal("390/1=4", Encoding.UTF8.GetString(message.Data), "Data");

        var empty = (ModMessageData)MessageCodec.Decode(MessageCodec.Encode(new ModMessageData("m", "ping", null))).Payload;
        Assert.Equal(0, empty.Data.Length, "Missing data becomes empty");

        var largest = new byte[MessageCodec.MaxModDataBytes];
        largest[^1] = 7;
        var big = (ModMessageData)MessageCodec.Decode(MessageCodec.Encode(new ModMessageData("m", "bulk", largest))).Payload;
        Assert.Equal(7, (int)big.Data[^1], "The largest allowed message survives");
        Assert.Throws<WireFormatException>(() => MessageCodec.Encode(new ModMessageData("m", "bulk", new byte[MessageCodec.MaxModDataBytes + 1])), "Too much data is refused when writing");

        var longName = MessageCodec.Encode(new ModMessageData("m", new string('n', MessageCodec.MaxModNameBytes + 1), data));
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(longName), "A name over the limit is refused when reading");
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(MessageCodec.Encode(new ModMessageData("", "set", data))), "A message without a mod id is refused");

        var cut = MessageCodec.Encode(new ModMessageData("m", "set", data));
        Array.Resize(ref cut, cut.Length - 2);
        Assert.Throws<WireFormatException>(() => MessageCodec.Decode(cut), "A cut message is refused");

        var verdict = (VerdictMessage)MessageCodec.Decode(MessageCodec.Encode(new VerdictMessage(true, Array.Empty<CompatibilityProblem>(), Array.Empty<SessionSettingValue>(), false, new[] { "a", "b" }))).Payload;
        Assert.SequenceEqual(new[] { "a", "b" }, verdict.SharedModIds, "Shared mods travel with the verdict");
        var bare = (VerdictMessage)MessageCodec.Decode(MessageCodec.Encode(new VerdictMessage(true, Array.Empty<CompatibilityProblem>(), Array.Empty<SessionSettingValue>()))).Payload;
        Assert.Equal(0, bare.SharedModIds.Count, "No shared mods");

        var channel = new ModMessenger(new OfflineLink(), () => 0, null).Channel("m");
        Assert.Throws<ArgumentException>(() => channel.SendToHost("", data), "An empty name is a programming error");
        Assert.Throws<ArgumentException>(() => channel.SendToHost("set", new byte[MessageCodec.MaxModDataBytes + 1]), "Too much data is a programming error");
        yield break;
    }
}

internal sealed class OfflineLink : IModMessageLink
{
    public SessionRole Role { get; set; } = SessionRole.Offline;

    public ulong LocalId => 0;

    public bool HostShares(string modId) => false;

    public bool SendToHost(ModMessageData message) => false;

    public bool SendToPeer(ulong peer, ModMessageData message) => false;

    public IReadOnlyList<ulong> PeersSharing(string modId) => Array.Empty<ulong>();
}
