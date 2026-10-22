using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.Voice;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoicePacketTest : TestCase
{
    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 250 };
        var bytes = VoicePacket.Write(76561198000000001UL, 65534, data, 5);
        Assert.Equal(VoicePacket.HeaderSize + 5, bytes.Length, "Header and the five bytes asked for");
        Assert.True(VoicePacket.TryRead(bytes, out var packet), "The packet reads back");
        Assert.Equal(76561198000000001UL, packet.Speaker, "Speaker");
        Assert.Equal((ushort)65534, packet.Sequence, "Sequence");
        Assert.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }, packet.Data, "Voice data");

        var relayed = VoicePacket.WithSpeaker(bytes, 76561198000000002UL);
        Assert.True(VoicePacket.TryRead(relayed, out var other), "A relayed packet reads back");
        Assert.Equal(76561198000000002UL, other.Speaker, "The host puts the real sender in");
        Assert.Equal((ushort)65534, other.Sequence, "and keeps the sequence");
        Assert.SequenceEqual(packet.Data, other.Data, "and the data");

        var wrongMagic = (byte[])bytes.Clone();
        wrongMagic[0] = 0;
        Assert.False(VoicePacket.TryRead(wrongMagic, out _), "Another kind of packet is not voice");
        var newer = (byte[])bytes.Clone();
        newer[1] = VoicePacket.CurrentVersion + 1;
        Assert.False(VoicePacket.TryRead(newer, out _), "A packet of another version is skipped");
        Assert.False(VoicePacket.TryRead(new byte[VoicePacket.HeaderSize - 1], out _), "A cut packet is skipped");
        Assert.False(VoicePacket.TryRead(null, out _), "No bytes, no packet");
        Assert.True(VoicePacket.TryRead(VoicePacket.Write(1, 0, new byte[0], 0), out var empty) && empty.Data.Length == 0, "An empty packet is fine");

        Assert.True(VoicePacket.IsNewer(1, 0), "1 comes after 0");
        Assert.True(VoicePacket.IsNewer(2, 65535), "2 comes after 65535, the counter wrapped");
        Assert.False(VoicePacket.IsNewer(65535, 2), "65535 is older than 2 after a wrap");
        Assert.False(VoicePacket.IsNewer(7, 7), "The same packet twice is not newer");
        Assert.Throws<System.ArgumentOutOfRangeException>(() => VoicePacket.Write(1, 0, new byte[VoicePacket.MaxDataBytes + 1], VoicePacket.MaxDataBytes + 1), "Too much data");
        yield break;
    }
}
