using System;
using System.Collections.Generic;
using System.Globalization;

namespace CatLib.Diagnostics;

internal sealed class CrashStrings
{
    public const string CatalogPrefix = "crash.";
    public const string PhrasePrefix = "phrase.";
    public const int PhraseCount = 14;

    public static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["title"] = "Cat Mail Co: crash report",
        ["openFolder"] = "Open the report folder",
        ["copy"] = "Copy the report",
        ["showDetails"] = "Details",
        ["hideDetails"] = "Hide details",
        ["footer"] = "The report and logs are saved in {0}",
        ["help"] = "If this keeps happening, send the report folder to the mod authors.",
        ["copied"] = "The report is copied",
        ["summary"] = "The game closed unexpectedly: {0} ({1}).",
        ["where"] = "Where: {0}.",
        ["ran"] = "The game ran for {0}.",
        ["quitting"] = "It happened while the game was quitting.",
        ["hang"] = "The game stopped responding while quitting and was closed after {0}.",
        ["dumpSaved"] = "A memory dump is saved with the report.",
        ["hoursMinutes"] = "{0} h {1} min",
        ["minutes"] = "{0} min",
        ["seconds"] = "{0} s",
        ["detailTime"] = "Time",
        ["detailExitCode"] = "Exit code",
        ["detailModule"] = "Module",
        ["detailException"] = "exception",
        ["detailThread"] = "Thread",
        ["gameThread"] = "the game thread",
        ["otherThread"] = "another thread",
        ["detailGame"] = "Game",
        ["detailMods"] = "Mods",
        ["detailEvents"] = "Last events",
        ["exit.normal"] = "normal exit",
        ["exit.closedOutside"] = "the game was closed from outside, for example from the Task Manager",
        ["exit.accessViolation"] = "access to an invalid memory address (access violation)",
        ["exit.heapCorruption"] = "the memory heap is damaged (heap corruption)",
        ["exit.failFast"] = "fail fast or a stack buffer overrun",
        ["exit.stackOverflow"] = "stack overflow",
        ["exit.illegalInstruction"] = "illegal instruction",
        ["exit.divideByZero"] = "integer division by zero",
        ["exit.consoleClosed"] = "the game was closed through its console window",
        ["exit.dotnetException"] = "an unhandled .NET exception",
        ["exit.dotnetFailFast"] = ".NET fail fast",
        ["exit.unknown"] = "unknown reason",
        ["phrase.1"] = "Your game got meowed :(",
        ["phrase.2"] = "Cats don't like water, and the game didn't like this",
        ["phrase.3"] = "Someone knocked the game off the table",
        ["phrase.4"] = "A parcel fell off the top shelf",
        ["phrase.5"] = "A cat sat on the keyboard",
        ["phrase.6"] = "The game curled up and fell asleep",
        ["phrase.7"] = "The boat sailed off without the game",
        ["phrase.8"] = "Nine lives, and one of them is gone",
        ["phrase.9"] = "The yarn got all tangled up",
        ["phrase.10"] = "Hiss! Something went wrong",
        ["phrase.11"] = "The game chased a laser dot and got lost",
        ["phrase.12"] = "Oops, the mail got wet",
        ["phrase.13"] = "The game is hiding under the sofa",
        ["phrase.14"] = "Somebody stepped on a tail"
    };

    private readonly IDictionary<string, string> _texts;

    public CrashStrings(IDictionary<string, string> texts)
    {
        _texts = texts ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static CrashStrings EnglishOnly { get; } = new CrashStrings(null);

    public static IEnumerable<string> Keys => English.Keys;

    public string Get(string key)
    {
        if (_texts.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return English.TryGetValue(key, out var english) ? english : key;
    }

    public string Format(string key, params object[] arguments)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), arguments);
        }
        catch (FormatException)
        {
            return English.TryGetValue(key, out var english) ? string.Format(CultureInfo.InvariantCulture, english, arguments) : key;
        }
    }

    public string Phrase(Random random) => Get(PhrasePrefix + (random.Next(PhraseCount) + 1).ToString(CultureInfo.InvariantCulture));
}
