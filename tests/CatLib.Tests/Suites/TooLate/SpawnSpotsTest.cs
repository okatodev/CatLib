using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class SpawnSpotsTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var game = new List<(float X, float Y, float Z)> { (10.67f, 0.36f, 9f), (10.69f, 0.18f, 6.5f), (10.66f, 0.23f, 4f), (10.51f, 0.42f, 1.5f) };
        var extra = SpawnSpots.Extra(game, PlayerLimit.MostPlayers);
        Assert.Equal(4, extra.Count, "Players 5 to 8 get four spots of their own");
        Assert.True(Near(extra[0].Z, 7.75f) && Near(extra[0].X, 10.68f), "Player 5 starts between players 1 and 2");
        Assert.True(Near(extra[1].Z, 5.25f), "Player 6 between players 2 and 3");
        Assert.True(Near(extra[2].Z, 2.75f), "Player 7 between players 3 and 4");
        Assert.True(Near(extra[3].Z, 0.25f) && Near(extra[3].Y, 0.42f), "Player 8 half a step past player 4, at its height");
        Assert.Equal(0, extra[0].Nearest, "A new spot faces like the game's spot next to it");

        Assert.Equal(0, SpawnSpots.Extra(game, 4).Count, "Four players need nothing new");
        Assert.Equal(0, SpawnSpots.Extra(new List<(float, float, float)> { (0f, 0f, 0f) }, 8).Count, "One spot of the game gives no direction");

        var two = SpawnSpots.Extra(new List<(float, float, float)> { (0f, 0f, 0f), (0f, 0f, 2f) }, 6);
        Assert.Equal(4, two.Count, "Two spots of the game still give the missing ones");
        Assert.True(Near(two[0].Z, 1f) && Near(two[1].Z, 3f) && Near(two[2].Z, -1f) && Near(two[3].Z, 4f), "between them, then outward on both ends");
        yield break;
    }

    private static bool Near(float value, float expected) => Math.Abs(value - expected) < 0.01f;
}
