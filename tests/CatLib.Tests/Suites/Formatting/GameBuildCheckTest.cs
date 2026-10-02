using System;
using System.Collections.Generic;
using CatLib.Diagnostics;
using CatLib.Game;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Formatting;

public sealed class GameBuildCheckTest : TestCase
{
    public const string October = "CMC 1.01.00.1737.9770.21112";
    public const string August = "CMC 1.01.00.1700.9722.30000";
    public const string Later = "CMC 1.02.00.1800.9800.100";
    public const int OctoberSteam = 25651540;

    public override string Suite => "Formatting";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var october = GameBuild.Parse(October, OctoberSteam);
        Assert.True(october.HasStamp, "The build stamp is read from the last two numbers of the game version");
        Assert.Equal(9770, october.Days, "Build stamp days");
        Assert.Equal(21112, october.HalfSeconds, "Build stamp half seconds");
        Assert.Equal(new DateTime(2026, 10, 1, 11, 43, 44), october.BuiltAt.Value, "The October build is built on 2026-10-01 at 11:43:44");
        Assert.Equal("2026-10-01", october.BuiltOn, "Build date text");
        Assert.Equal("CMC 1.01.00.1737.9770.21112, built 2026-10-01 11:43, Steam build 25651540", october.Describe(), "Build description for the log");
        Assert.Equal("2026-08-14", GameBuild.Parse(August).BuiltOn, "The August build date");
        Assert.False(GameBuild.Parse("CMC demo").HasStamp, "A version without numbers has no stamp");
        Assert.False(GameBuild.Parse(null).HasStamp, "No version has no stamp");
        Assert.False(GameBuild.Parse("CMC 1.01.00.1737.9770.99999").HasStamp, "Half seconds past the day are not a stamp");
        Assert.Equal(0, GameBuild.Parse(October, -5).SteamBuild, "A negative Steam build is unknown");

        Assert.Equal(1, GameCompatibility.Supported.Count, "This CatLib supports one game build");
        Assert.Equal(October, GameCompatibility.Target.Version, "The supported game version");
        Assert.Equal(OctoberSteam, GameCompatibility.Target.SteamBuild, "The supported Steam build");

        var supported = GameCompatibility.Supported;
        Assert.Equal(GameBuildStatus.Supported, GameCompatibility.Compare(GameBuild.Parse(October), supported).Status, "The same build is supported");
        Assert.Equal(GameBuildStatus.Supported, GameCompatibility.Compare(GameBuild.Parse(October, 1), supported).Status, "The version decides over a different Steam build number, as under an emulator");
        Assert.Equal(GameBuildStatus.GameOlder, GameCompatibility.Compare(GameBuild.Parse(August), supported).Status, "An older build asks to update the game");
        Assert.Equal(GameBuildStatus.GameNewer, GameCompatibility.Compare(GameBuild.Parse(Later), supported).Status, "A newer build asks to update CatLib");
        Assert.Equal(GameBuildStatus.GameNewer, GameCompatibility.Compare(GameBuild.Parse("CMC 1.01.00.1737.9770.21114"), supported).Status, "Two seconds later is already another build");
        Assert.Equal(GameBuildStatus.GameOlder, GameCompatibility.Compare(GameBuild.Parse("CMC demo", 25000000), supported).Status, "Without a version stamp the Steam build decides: older");
        Assert.Equal(GameBuildStatus.GameNewer, GameCompatibility.Compare(GameBuild.Parse("CMC demo", 26000000), supported).Status, "Without a version stamp the Steam build decides: newer");
        Assert.Equal(GameBuildStatus.Supported, GameCompatibility.Compare(GameBuild.Parse("CMC demo", OctoberSteam), supported).Status, "Without a version stamp the same Steam build is supported");
        Assert.Equal(GameBuildStatus.Unknown, GameCompatibility.Compare(GameBuild.Parse("CMC demo"), supported).Status, "Without a stamp and a Steam build the status is unknown");
        Assert.Equal(GameBuildStatus.Unknown, GameCompatibility.Compare(GameBuild.Parse(October), Array.Empty<GameBuild>()).Status, "No supported builds means unknown");

        var two = new[] { GameBuild.Parse(August), GameBuild.Parse(October, OctoberSteam) };
        Assert.Equal(GameBuildStatus.Supported, GameCompatibility.Compare(GameBuild.Parse(August), two).Status, "Any listed build is supported");
        Assert.Equal(August, GameCompatibility.Compare(GameBuild.Parse(August), two).Target.Version, "The target of a supported build is that build");
        var between = GameCompatibility.Compare(GameBuild.Parse("CMC 1.01.00.1720.9750.0"), two);
        Assert.Equal(GameBuildStatus.GameOlder, between.Status, "A build between two listed ones is older than the newest");
        Assert.Equal(October, between.Target.Version, "The target is the newest listed build");

        var newer = GameCompatibility.Compare(GameBuild.Parse(Later), supported);
        var older = GameCompatibility.Compare(GameBuild.Parse(August), supported);
        var same = GameCompatibility.Compare(GameBuild.Parse(October, OctoberSteam), supported);
        var unknown = GameCompatibility.Compare(GameBuild.Parse("CMC demo"), supported);
        Assert.True(newer.IsWarning && older.IsWarning, "Newer and older builds warn");
        Assert.False(same.IsWarning || unknown.IsWarning, "Supported and unknown builds do not warn");

        Assert.Equal("The game was updated, look for a CatLib update", VersionBadgeText.Warning(newer, "en"), "Newer warning");
        Assert.Equal("Your game is older, update it in Steam", VersionBadgeText.Warning(older, "en"), "Older warning");
        Assert.Null(VersionBadgeText.Warning(same, "en"), "No warning for the supported build");
        Assert.Equal("Made for 2026-10-01 (build 25651540) · yours: 2026-08-14", VersionBadgeText.Detail(older, 3, "en"), "Detail of an older game");
        Assert.Equal("Made for your game: 2026-10-01 (build 25651540) · 3 mods", VersionBadgeText.Detail(same, 3, "en"), "Detail of the supported game");
        Assert.Equal("The game build is unknown · 1 mod", VersionBadgeText.Detail(unknown, 1, "en"), "Detail of an unknown game");
        Assert.Equal("Сделан для 2026-10-01 (билд 25651540) · у вас: 2026-08-14", VersionBadgeText.Detail(older, 3, "ru"), "Russian detail");
        Assert.Equal("Игра обновилась, поищите обновление CatLib", VersionBadgeText.Warning(newer, "ru"), "Russian warning");
        Assert.Equal("CatLib " + PluginMeta.Version, VersionBadgeText.Title, "Badge title");

        var calm = VersionBadgeText.Compose(same, false, 3, "en");
        Assert.Equal(VersionBadgeText.Title, calm, "A supported game shows only the CatLib version");
        var warned = VersionBadgeText.Compose(newer, false, 3, "en");
        Assert.True(warned.Contains(VersionBadgeText.MarkColor) && warned.Contains(VersionBadgeText.Title) && warned.Contains(VersionBadgeText.Warning(newer, "en")),
            "A newer game shows the mark, the version and one warning line");
        Assert.Equal(2, warned.Split('\n').Length, "The warning takes one line under the version");
        Assert.Equal(3, VersionBadgeText.Compose(newer, true, 3, "en").Split('\n').Length, "Pointing at the badge adds the detail line");
        Assert.Equal(2, VersionBadgeText.Compose(same, true, 3, "en").Split('\n').Length, "Pointing at a calm badge adds the detail line");

        Assert.Equal("supported", CrashWatch.DescribeCheck(same), "Crash report text of a supported build");
        Assert.True(CrashWatch.DescribeCheck(newer).StartsWith("the game is newer than the build CatLib is made for (CMC 1.01.00.1737.9770.21112"), "Crash report text of a newer build");
        var session = CrashSession.Parse(new[] { CrashSession.Line(CrashSession.GameBuildKey, CrashWatch.DescribeCheck(older)) });
        Assert.Equal(CrashWatch.DescribeCheck(older), session.GameBuild, "The build check is read back from the session file");
        context.Note("Supported: " + GameCompatibility.Target.Describe());
        yield break;
    }
}
