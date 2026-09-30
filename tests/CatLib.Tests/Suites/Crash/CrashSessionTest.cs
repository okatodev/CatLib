using System.Collections.Generic;
using System.Linq;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class CrashSessionTest : TestCase
{
    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var session = new CrashSession
        {
            CatLibVersion = "0.6.0",
            GameVersion = "1.0.4",
            Started = "2026-09-27 12:00:00",
            ProcessId = 43356,
            Language = "ru",
            PlayerLog = "C:\\Users\\cat\\Player.log",
            BepInExLog = "D:\\Game\\BepInEx\\LogOutput.log",
            ReportsDirectory = "D:\\Game\\BepInEx\\CatLib\\Crashes",
            MainThreadId = 7788
        };
        session.Mods.Add("Boat Tweaks 0.2.0 (catlib.boattweaks)");

        var lines = session.HeaderLines().ToList();
        lines.Add(CrashSession.Line(CrashSession.EventKey, "12:01:00 Level.LevelLoadStarted"));
        lines.Add(CrashSession.Line(CrashSession.EventKey, "12:02:00 two\r\nlines"));
        lines.Add(CrashSession.Line(CrashSession.TextPrefix + "copy", "Скопировать отчёт"));
        lines.Add(CrashSession.Line(CrashSession.LanguageKey, "de"));
        lines.Add(CrashSession.Line(CrashSession.TextPrefix + "copy", "Bericht kopieren"));
        lines.Add(CrashSession.Line(CrashSession.TextPrefix + "footer", "Gespeichert in {0}"));
        lines.Add("not a line");
        lines.Add("=no key");
        lines.Add("unknown=value");
        lines.Add(CrashSession.Line(CrashSession.CleanKey, "2026-09-27 12:03:00"));

        var parsed = CrashSession.Parse(lines);
        Assert.Equal("0.6.0", parsed.CatLibVersion, "CatLib version");
        Assert.Equal("1.0.4", parsed.GameVersion, "Game version");
        Assert.Equal(43356, parsed.ProcessId, "Process id");
        Assert.Equal("de", parsed.Language, "The last language line wins");
        Assert.Equal(7788, parsed.MainThreadId, "Game thread id");
        Assert.Equal("Bericht kopieren", parsed.Strings.Get("copy"), "The texts written after a language change win");
        Assert.Equal("Gespeichert in D:\\x", parsed.Strings.Format("footer", "D:\\x"), "Texts with a value");
        Assert.Equal(CrashStrings.English["title"], parsed.Strings.Get("title"), "A text the game did not write is English");
        Assert.Equal("D:\\Game\\BepInEx\\LogOutput.log", parsed.BepInExLog, "Paths with colons and backslashes survive");
        Assert.SequenceEqual(new[] { "Boat Tweaks 0.2.0 (catlib.boattweaks)" }, parsed.Mods, "Mods");
        Assert.Equal(2, parsed.Events.Count, "Events");
        Assert.Equal("12:02:00 two  lines", parsed.Events[1], "Line breaks are flattened so one value stays on one line");
        Assert.Equal("2026-09-27 12:03:00", parsed.CleanExit, "Clean exit mark");

        foreach (var language in new[] { "ja", "de", "pt-br" })
        {
            var localized = CrashSession.Parse(CrashWatch.TextLines(language)).Strings;
            Assert.Equal(CatLib.UI.UiText.Catalog.Find("crash.title", language), localized.Get("title"), $"CatLib writes the {language} window title for the watcher");
            Assert.NotEqual(CrashStrings.English["copy"], localized.Get("copy"), $"The {language} copy button is translated");
        }

        var empty = CrashSession.Parse(new string[0]);
        Assert.Null(empty.CleanExit, "No clean exit mark in an empty session");
        Assert.Equal(0, empty.ProcessId, "No process id in an empty session");
        Assert.Equal("Copy the report", empty.Strings.Get("copy"), "English by default");
        Assert.Equal("Save {0", new CrashStrings(new Dictionary<string, string> { ["footer"] = "Save {0" }).Get("footer"), "A broken text is kept as it is");
        Assert.Equal("The report and logs are saved in D:", new CrashStrings(new Dictionary<string, string> { ["footer"] = "Save {0" }).Format("footer", "D:"), "A text that cannot be filled in falls back to English");
        yield break;
    }
}
