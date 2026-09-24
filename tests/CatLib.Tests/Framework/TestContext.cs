using System.Collections.Generic;
using CatLib.Logging;

namespace CatLib.Tests.Framework;

public sealed class TestContext
{
    private readonly List<string> _notes = new();

    internal TestContext(CatLogger log, string trigger)
    {
        Log = log;
        Trigger = trigger;
    }

    public CatLogger Log { get; }

    public string Trigger { get; }

    public IReadOnlyList<string> Notes => _notes;

    public void Note(string note)
    {
        _notes.Add(note);
    }
}
