using System.Collections.Generic;
using System.Linq;
using CatLib.Patching;
using CatLib.Tests.Framework;
using TooLate.Logic;

namespace CatLib.Tests.Suites.TooLate;

public sealed class PlayerLimitTest : TestCase
{
    public override string Suite => "TooLate";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(4, PlayerLimit.Clamp(1), "Never fewer than the game's four");
        Assert.Equal(8, PlayerLimit.Clamp(20), "Never more than eight");
        Assert.Equal((byte)5, PlayerLimit.ConnectionLimit(4), "Four players is the game's own number 5: the host and the new player count too");
        Assert.Equal((byte)9, PlayerLimit.ConnectionLimit(8), "Eight players");

        var connect = new byte[]
        {
            0x48, 0x8B, 0x4D, 0x38, 0xE8, 0xF7, 0xF8, 0xA5, 0x00, 0x83, 0xF8, 0x05, 0x0F, 0x8D, 0xBE, 0x05, 0x00, 0x00,
            0x48, 0x8B, 0x0D, 0x3F, 0xA5, 0xA2, 0x02, 0x44, 0x38, 0xA1, 0x90, 0x00, 0x00, 0x00, 0x0F, 0x85, 0x08, 0x04, 0x00, 0x00, 0x48, 0x8B
        };
        Assert.True(CodePatch.TryFindSite(connect, BytePattern.Parse(PlayerLimit.CountPattern), PlayerLimit.CountOffset, 1, out var count, out _),
            "The player count check is found in the game's code");
        Assert.Equal((byte)5, connect[count], "It compares with 5");
        Assert.True(CodePatch.TryFindSite(connect, BytePattern.Parse(PlayerLimit.LoadingPattern), PlayerLimit.LoadingOffset, PlayerLimit.LoadingLength,
            out var loading, out _), "The check for a level that already started is found");
        Assert.SequenceEqual(new byte[] { 0x0F, 0x85, 0x08, 0x04, 0x00, 0x00 }, connect.Skip(loading).Take(PlayerLimit.LoadingLength),
            "The jump that turns the player away is what gets replaced");
        Assert.Equal(PlayerLimit.LoadingLength, PlayerLimit.SkipJump.Length, "It is replaced by an instruction of the same length");

        var scene = new[] { new EntityPlace(3, 1, 0, 0), new EntityPlace(5, 2, 0, 0), new EntityPlace(0, 9, 9, 9), new EntityPlace(3, 7, 0, 0) };
        var saved = new[] { new EntityPlace(5, 4, 1, 0), new EntityPlace(8, 6, 1, 0), new EntityPlace(8, 6, 1, 0) };
        var merged = EntityPlaces.Merge(scene, saved);
        Assert.SequenceEqual(new uint[] { 3, 5, 8 }, merged.Select(place => place.Id), "Scene entities first, saved ones last, each once");
        Assert.Equal(4f, merged.First(place => place.Id == 5).X, "A saved entity uses its place in the snapshot, not where it began");
        Assert.Equal(1f, merged.First(place => place.Id == 3).X, "A scene entity keeps its first place");
        yield break;
    }
}
