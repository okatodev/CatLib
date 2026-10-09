using System.Collections.Generic;

namespace CatLib.Diagnostics;

internal sealed class CrashThreadStack
{
    public int ThreadId { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<string> Frames { get; } = new List<string>();

    public string Note { get; set; } = string.Empty;

    public string GameMethod { get; set; } = string.Empty;

    public string Key => string.Join("\n", Frames) + "\n" + Note;
}
