using System.Collections.Generic;
using CatLib.Game;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class GameStateTest : TestCase
{
    public override string Suite => "Platform";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var gameVersion = GameInfo.GameVersion;
        context.Note($"Game version: {gameVersion ?? "<null>"}");
        context.Note($"Is demo: {Format(GameInfo.IsDemo)}");
        context.Note($"Is singleplayer: {Format(GameInfo.IsSingleplayer)}");
        context.Note($"Loaded level key: {GameInfo.LoadedLevelKey ?? "<null>"}");
        context.Note($"Is server: {Format(GameInfo.IsServer)}");

        Assert.NotEmpty(gameVersion, "Game version reported by BootstrapManager");
        yield break;
    }

    private static string Format(bool? value) => value.HasValue ? value.Value.ToString() : "<no instance>";
}
