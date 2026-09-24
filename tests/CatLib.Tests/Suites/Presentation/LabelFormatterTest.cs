using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class LabelFormatterTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var cases = new (string Key, string Expected)[]
        {
            ("ExtraFov", "Extra Fov"),
            ("carry_limit", "Carry limit"),
            ("HTTPServerPort", "HTTP Server Port"),
            ("Level2Speed", "Level 2 Speed"),
            ("fastMode", "Fast Mode"),
            ("  spaced   key ", "Spaced key"),
            ("already Pretty", "Already Pretty"),
            ("", "")
        };

        foreach (var (key, expected) in cases)
        {
            Assert.Equal(expected, LabelFormatter.Prettify(key), "Label for \"" + key + "\"");
        }

        yield break;
    }
}
