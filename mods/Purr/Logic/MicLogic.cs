using System.Collections.Generic;
using System.Linq;

namespace Purr.Logic;

public enum MicMode
{
    PushToTalk,
    OpenMic
}

public enum SpeakersCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum MicNotice
{
    None,
    MicrophoneOff,
    MicrophoneOn,
    HeadphonesOff,
    HeadphonesOn
}

public static class MicLogic
{
    public static bool Transmits(bool enabled, MicMode mode, bool pushToTalkHeld, bool muted, bool deafened) =>
        enabled && !muted && !deafened && (mode == MicMode.OpenMic || pushToTalkHeld);

    public static bool Hears(bool enabled, bool deafened) => enabled && !deafened;

    public static MicNotice MuteNotice(bool muted) => muted ? MicNotice.MicrophoneOff : MicNotice.MicrophoneOn;

    public static MicNotice DeafenNotice(bool deafened) => deafened ? MicNotice.HeadphonesOff : MicNotice.HeadphonesOn;

    public static IReadOnlyList<string> SpeakerNames(IEnumerable<ulong> speakers, ulong local, System.Func<ulong, string> name, string you)
    {
        var list = speakers?.Distinct().ToList() ?? new List<ulong>();
        var result = new List<string>();
        if (list.Contains(local))
        {
            result.Add(you);
        }

        result.AddRange(list.Where(id => id != local)
            .Select(id => name(id))
            .Select(text => string.IsNullOrWhiteSpace(text) ? "?" : text)
            .OrderBy(text => text, System.StringComparer.CurrentCultureIgnoreCase));
        return result;
    }
}
