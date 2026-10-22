using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using CatLib.Voice;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoiceStreamTest : TestCase
{
    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var stream = new VoiceStream(1000, 10, 20, 4, 3, 8, 5);
        Assert.Equal(200, stream.ClipSamples, "Clip of 20 blocks of 10 samples");
        Assert.Equal(VoiceCommand.None, stream.Update(0).Command, "Nothing plays without voice");

        stream.Push(Ramp(1, 25), 25);
        var waiting = stream.Update(0);
        Assert.Equal(VoiceCommand.None, waiting.Command, "25 samples are less than the 30 to prefill");
        Assert.Equal(0, waiting.Writes.Count, "Nothing is written before the prefill");

        stream.Push(Ramp(26, 15), 15);
        var start = stream.Update(0);
        Assert.Equal(VoiceCommand.Play, start.Command, "40 samples start the playback");
        Assert.Equal(4, start.Writes.Count, "The lead of 4 blocks is written ahead");
        Assert.SequenceEqual(new[] { 0, 10, 20, 30 }, start.Writes.Select(write => write.Offset), "Blocks from the start of the clip");
        Assert.Equal(1f, start.Writes[0].Samples[0], "The first block starts with the first sample");
        Assert.Equal(40f, start.Writes[3].Samples[9], "The fourth block ends with the 40th sample");
        Assert.True(stream.IsPlaying, "Playing");

        var steady = stream.Update(12);
        Assert.Equal(1, steady.Writes.Count, "One block played, one more written");
        Assert.True(steady.Writes[0].IsSilence, "Nothing more queued, so silence");
        Assert.Equal(40, steady.Writes[0].Offset, "Written after the lead");

        stream.Push(Ramp(100, 10), 10);
        var more = stream.Update(25);
        Assert.Equal(1, more.Writes.Count, "Another block played");
        Assert.False(more.Writes[0].IsSilence, "The new voice goes in right away");
        Assert.Equal(100f, more.Writes[0].Samples[0], "New voice from its first sample");

        var position = 25;
        var stopped = false;
        for (var frame = 0; frame < 20 && !stopped; frame++)
        {
            position = (position + 10) % stream.ClipSamples;
            stopped = stream.Update(position).Command == VoiceCommand.Stop;
        }

        Assert.True(stopped, "After 5 blocks of silence the source stops");
        Assert.False(stream.IsPlaying, "Not playing after the stop");

        var wrap = new VoiceStream(1000, 10, 20, 4, 1, 8, 50);
        wrap.Push(Ramp(1, 10), 10);
        wrap.Update(0);
        var offsets = new List<int>();
        var at = 0;
        for (var frame = 0; frame < 30; frame++)
        {
            at = (at + 10) % wrap.ClipSamples;
            offsets.AddRange(wrap.Update(at).Writes.Select(write => write.Offset));
        }

        Assert.True(offsets.All(offset => offset >= 0 && offset < wrap.ClipSamples && offset % 10 == 0), "Writes stay inside the clip and on block borders");
        Assert.True(offsets.Contains(0) && offsets.Contains(190), "The writes wrap around the end of the clip");

        var hitch = new VoiceStream(1000, 10, 20, 4, 1, 8, 50);
        hitch.Push(Ramp(1, 10), 10);
        hitch.Update(0);
        var late = hitch.Update(95);
        Assert.Equal(1, hitch.Hitches, "A long frame that played past the written part counts as a hitch");
        Assert.True(late.Writes.All(write => write.Offset >= 100 || write.Offset < 20), "After a hitch the writes start past what plays now");

        var flood = new VoiceStream(1000, 10, 20, 4, 3, 8, 5);
        flood.Push(Ramp(1, 120), 120);
        Assert.Equal(30, flood.Queued, "Too much voice queued drops down to the prefill");
        Assert.Equal(90, flood.Dropped, "The oldest samples are dropped");

        var tail = new VoiceStream(1000, 10, 20, 4, 1, 8, 50);
        tail.Push(Ramp(1, 14), 14);
        var first = tail.Update(0);
        Assert.False(first.Writes[0].IsSilence, "A full block plays");
        Assert.True(first.Writes[1].IsSilence, "Four samples wait one block for more");
        Assert.False(first.Writes[2].IsSilence, "then they play padded with silence");
        Assert.Equal(14f, first.Writes[2].Samples[3], "The last sample of the tail");
        Assert.Equal(0f, first.Writes[2].Samples[4], "and silence after it");
        yield break;
    }

    private static float[] Ramp(int from, int count) => Enumerable.Range(from, count).Select(value => (float)value).ToArray();
}
