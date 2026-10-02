using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class WatcherPackageTest : TestCase
{
    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var root = Path.Combine(Path.GetTempPath(), "CatLibWatcherPackage_" + Guid.NewGuid().ToString("N"));
        try
        {
            var plugins = Path.Combine(root, "plugins");
            var own = Path.Combine(plugins, "CatLib-CatLib");
            var package = Path.Combine(plugins, "CatLib-CrashWatcher");
            Directory.CreateDirectory(own);
            Directory.CreateDirectory(Path.Combine(plugins, "Someone-OtherMod", "deep"));

            Assert.Null(WatcherLocator.Find(own, plugins, CrashWatch.WatcherFileName), "No watcher anywhere");
            Assert.Null(WatcherLocator.Find(own, Path.Combine(root, "missing"), CrashWatch.WatcherFileName), "A missing plugins folder finds nothing");

            Directory.CreateDirectory(package);
            var packaged = Path.Combine(package, CrashWatch.WatcherFileName);
            File.WriteAllText(packaged, "watcher");
            Assert.Equal(packaged, WatcherLocator.Find(own, plugins, CrashWatch.WatcherFileName), "The watcher of its own package is found from CatLib's folder");

            var tooDeep = Path.Combine(plugins, "a", "b", "c", "d", "e", "f");
            Directory.CreateDirectory(tooDeep);
            Assert.Equal(packaged, WatcherLocator.Find(null, plugins, CrashWatch.WatcherFileName), "Without CatLib's folder the plugins folder is searched");

            var beside = Path.Combine(own, CrashWatch.WatcherFileName);
            File.WriteAllText(beside, "watcher");
            Assert.Equal(beside, WatcherLocator.Find(own, plugins, CrashWatch.WatcherFileName), "A watcher next to CatLib.dll wins, as in a manual install");

            File.Delete(beside);
            File.Delete(packaged);
            var hidden = Path.Combine(tooDeep, CrashWatch.WatcherFileName);
            File.WriteAllText(hidden, "watcher");
            Assert.Null(WatcherLocator.Find(own, plugins, CrashWatch.WatcherFileName), "The search stops at a few levels below the plugins folder");
            context.Note("Unknown version text: " + WatcherLocator.VersionText(hidden));
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (Exception)
            {
            }
        }

        var current = new CrashSession { Format = CrashSession.CurrentFormat, CatLibVersion = "0.6.2" };
        var lines = current.HeaderLines().ToList();
        Assert.Equal(CrashSession.Line(CrashSession.FormatKey, CrashSession.CurrentFormat.ToString()), lines[0], "The session file starts with its format");
        var parsed = CrashSession.Parse(lines);
        Assert.Equal(CrashSession.CurrentFormat, parsed.Format, "The format is read back");
        Assert.False(parsed.IsNewerFormat, "The current format is understood");
        Assert.Equal(0, CrashSession.Parse(new[] { CrashSession.Line(CrashSession.CatLibKey, "0.6.1") }).Format, "A session of CatLib 0.6.1 has no format line");

        var newer = CrashSession.Parse(new[] { CrashSession.Line(CrashSession.FormatKey, (CrashSession.CurrentFormat + 1).ToString()), CrashSession.Line(CrashSession.CatLibKey, "0.9.0") });
        Assert.True(newer.IsNewerFormat, "A newer format is noticed");
        newer.WatcherVersion = "1.0.0";
        var report = CrashText.Report(newer, 0xC0000005, new CrashEventInfo(), DateTime.Now, TimeSpan.FromMinutes(3), new string[0]);
        Assert.True(report.Contains("crash watcher 1.0.0"), "The report names the watcher version");
        Assert.True(report.Contains("update CatLib Crash Watcher"), "The report asks to update an old watcher");
        Assert.False(CrashText.Report(parsed, 0xC0000005, new CrashEventInfo(), DateTime.Now, TimeSpan.FromMinutes(3), new string[0]).Contains("update CatLib Crash Watcher"),
            "A matching watcher asks for nothing");
        yield break;
    }
}
