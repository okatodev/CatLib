using System.Collections.Generic;
using CatLib.Tests.Framework;
using Purr.Logic;

namespace CatLib.Tests.Suites.Purr;

public sealed class MicLogicTest : TestCase
{
    public override string Suite => "Purr";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.False(MicLogic.Transmits(true, MicMode.PushToTalk, false, false, false), "Push to talk is quiet without the key");
        Assert.True(MicLogic.Transmits(true, MicMode.PushToTalk, true, false, false), "and talks while the key is held");
        Assert.True(MicLogic.Transmits(true, MicMode.OpenMic, false, false, false), "An open microphone talks without the key");
        Assert.False(MicLogic.Transmits(true, MicMode.OpenMic, true, true, false), "A muted microphone never talks");
        Assert.False(MicLogic.Transmits(true, MicMode.OpenMic, true, false, true), "Headphones off turn the microphone off too");
        Assert.False(MicLogic.Transmits(false, MicMode.OpenMic, true, false, false), "Voice chat off never talks");
        Assert.True(MicLogic.Hears(true, false), "You hear others");
        Assert.False(MicLogic.Hears(true, true), "but not with the headphones off");
        Assert.False(MicLogic.Hears(false, false), "nor with voice chat off");
        Assert.Equal(MicNotice.MicrophoneOff, MicLogic.MuteNotice(true), "Muting says the microphone is off");
        Assert.Equal(MicNotice.HeadphonesOn, MicLogic.DeafenNotice(false), "Turning headphones on says so");

        var names = new Dictionary<ulong, string> { [2] = "zoe", [3] = "Anna", [4] = "" };
        var list = MicLogic.SpeakerNames(new ulong[] { 3, 1, 2, 4, 2 }, 1, id => names[id], "You");
        Assert.SequenceEqual(new[] { "You", "?", "Anna", "zoe" }, list, "You first, then the others by name, once each");
        Assert.Equal(0, MicLogic.SpeakerNames(null, 1, id => "", "You").Count, "Nobody speaking, no names");
        yield break;
    }
}
