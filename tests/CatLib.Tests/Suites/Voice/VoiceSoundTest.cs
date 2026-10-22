using System.Collections.Generic;
using System.Linq;
using CatLib.Tests.Framework;
using CatLib.Voice;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoiceSoundTest : TestCase
{
    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var points = VoiceRolloff.Points(3f, 25f);
        Assert.Equal(0f, points.First().Distance, "The curve starts at the speaker");
        Assert.Equal(1f, points.First().Volume, "at full volume");
        Assert.Equal(1f, points.Last().Distance, "It ends at the hearing distance");
        Assert.Equal(0f, points.Last().Volume, "in silence");
        Assert.True(points.Zip(points.Skip(1), (a, b) => b.Distance >= a.Distance && b.Volume <= a.Volume).All(ok => ok), "Farther is never louder");
        Assert.Equal(1f, VoiceRolloff.VolumeAt(2.5f, 3f, 25f), "Full volume up to 3 m");
        Assert.Equal(0f, VoiceRolloff.VolumeAt(30f, 3f, 25f), "Silent past 25 m");
        var middle = VoiceRolloff.VolumeAt(14f, 3f, 25f);
        Assert.True(middle > 0.1f && middle < 0.5f, $"Half way it is clearly quieter: {middle}");
        Assert.True(VoiceRolloff.Points(100f, 10f).Skip(1).First().Distance < 1f, "Full volume never reaches the end of the curve");

        var bytes = new byte[] { 0x00, 0x40, 0x00, 0xC0, 0xFF, 0x7F, 0x00, 0x80 };
        var samples = new float[4];
        Assert.Equal(4, VoicePcm.Decode16(bytes, bytes.Length, samples), "Four samples of 16 bits");
        Assert.Equal(0.5f, samples[0], "Half up");
        Assert.Equal(-0.5f, samples[1], "Half down");
        Assert.True(samples[2] > 0.999f, "Almost the top");
        Assert.Equal(-1f, samples[3], "The bottom");
        Assert.Equal(2, VoicePcm.Decode16(bytes, 5, new float[4]), "An odd byte is left out");

        Assert.Equal((0.5f, 1f), VoicePcm.Split(0.5f), "Quieter goes to the source volume");
        Assert.Equal((1f, 1.5f), VoicePcm.Split(1.5f), "Louder goes to the samples");
        Assert.Equal((0f, 1f), VoicePcm.Split(float.NaN), "No volume for nonsense");
        var loud = new[] { 0.4f, -0.8f };
        VoicePcm.Amplify(loud, 2f);
        Assert.Equal(0.8f, loud[0], "Twice as loud");
        Assert.Equal(-1f, loud[1], "but never past full scale");
        Assert.Equal(48000, VoicePcm.PlaybackRate(48000), "48 kHz output stays");
        Assert.Equal(48000, VoicePcm.PlaybackRate(96000), "Steam gives at most 48 kHz");
        Assert.Equal(48000, VoicePcm.PlaybackRate(0), "Unknown output plays at 48 kHz");
        Assert.Equal(0.8f, VoicePcm.Peak(new[] { 0.1f, -0.8f, 0.3f }, 3), "The loudest sample");

        Assert.True(VoiceMixer.IsVoiceGroup("VoiceChat"), "A group named for voice is the voice group");
        Assert.False(VoiceMixer.IsVoiceGroup("Music"), "Music is not");

        var activity = new VoiceActivity(0.3);
        Assert.True(activity.Heard(5, 1.0), "First voice starts speaking");
        Assert.False(activity.Heard(5, 1.1), "More voice keeps it");
        Assert.Equal(0, activity.Expire(1.3).Count, "Still speaking within the hold");
        Assert.SequenceEqual(new ulong[] { 5 }, activity.Expire(1.5), "Quiet after the hold");
        Assert.False(activity.IsSpeaking(5), "Not speaking any more");
        activity.Heard(6, 2);
        Assert.True(activity.Forget(6), "A player who left stops speaking at once");
        yield break;
    }
}
