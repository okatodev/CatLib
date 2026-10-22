using System.Collections.Generic;
using System.IO;
using CatLib.Tests.Framework;
using CatLib.Voice;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoiceSampleTest : TestCase
{
    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var sample = new VoiceSample();
        sample.Add(0.0, new byte[] { 1, 2 });
        sample.Add(0.02, new byte[] { 3 });
        sample.Add(0.01, new byte[] { 4 });
        sample.Add(0.5, new byte[0]);
        Assert.Equal(3, sample.Count, "Empty pieces are not kept");
        Assert.Equal(0.02, sample.Duration, "A piece never goes back in time");

        var copy = VoiceSample.FromBytes(sample.ToBytes());
        Assert.Equal(3, copy.Count, "The file reads back");
        Assert.SequenceEqual(new byte[] { 3 }, copy.Packets[1].Data, "with the data");
        Assert.Equal(1, copy.Due(0.005).Count, "Only the first piece is due at 5 ms");
        Assert.Equal(0, copy.Due(0.005).Count, "and it is given only once");
        Assert.Equal(2, copy.Due(1).Count, "The rest is due later");
        Assert.True(copy.IsFinished, "Then the sample is sent");
        copy.Rewind();
        Assert.False(copy.IsFinished, "Rewound, it can be sent again");

        Assert.Throws<InvalidDataException>(() => VoiceSample.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "Another file is not a sample");
        var cut = sample.ToBytes();
        System.Array.Resize(ref cut, cut.Length - 1);
        Assert.Throws<InvalidDataException>(() => VoiceSample.FromBytes(cut), "A cut file is damaged");
        yield break;
    }
}
