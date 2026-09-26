using System.Collections.Generic;
using System.Linq;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class MessageLengthTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var problems = new[] { new CompatibilityProblem(ProblemKind.VersionMismatch, "catlib.tests.demo-network", "1.0.0", "2.0.0") };
        string NameOf(string id) => id == "catlib.tests.demo-network" ? "CatLib Demo Network Mod" : null;

        Assert.Equal("RENTAI was disconnected:\nCatLib Demo Network Mod", ToastText.Format("RENTAI was disconnected: CatLib Demo Network Mod"), "Two lines split after the first colon");
        Assert.Equal(ToastText.MaxLineLength, ToastText.Clip("A very long line that does not fit into one notification line").Length, "Clipped line length");
        Assert.Equal("Сначала наведитесь на метку\nполки", ToastText.Format("Сначала наведитесь на метку полки", line => line.Length, 28), "A single long line wraps at a word");
        Assert.Equal("Short", ToastText.Format("Short"), "A short line stays one line");
        Assert.Equal("Mod:\none two three four", ToastText.Format("Mod: one two three four", line => line.Length, 18), "The part before the colon gets its own line");
        Assert.Equal("Mod: aaa bbb\nccc ddd eee", ToastText.Format("Mod: aaa bbb ccc ddd eee", line => line.Length, 12), "Three lines are rebalanced into two");
        Assert.Equal("CatLib.Tests: «Fast Mode»\n— после перезапуска", ToastText.Format("CatLib.Tests: «Fast Mode» — после перезапуска"), "Lines are not broken inside quotes");
        Assert.True(ToastText.Format("Wide wide wide", line => line.Count(character => character == 'W') * 10 + line.Length, 12).Split('\n').Length == 2, "Width comes from the measuring function, not from the character count");
        Assert.True(ToastText.Clip("A very long line that does not fit into one notification line").EndsWith("\u2026"), "Clipped lines end with an ellipsis");

        foreach (var language in new[] { "en", "ru" })
        {
            var mods = ProblemText.ModsOnly(problems, NameOf);
            var briefs = new[]
            {
                UiText.Format(UiText.NetPlayerDisconnectedBrief, language, "RENTAI", mods),
                UiText.Format(UiText.NetPlayerIncompatibleBrief, language, "RENTAI", mods),
                UiText.Format(UiText.NetPlayerDisconnectedBrief, language, "A player with a very long Steam name", mods),
                UiText.Format(UiText.MessageAdjusted, language, "CatLib.Tests", "Volume", "500", "100"),
                UiText.Format(UiText.MessageRestart, language, "CatLib.Tests", "Fast Mode"),
                UiText.Format(UiText.MessageReset, language, "CatLib.Tests")
            };

            foreach (var brief in briefs)
            {
                var toast = ToastText.Format(brief);
                context.Note($"{language}: {toast.Replace("\n", " | ")}");
                var lines = toast.Split('\n');
                Assert.True(lines.Length <= 2, $"\"{toast}\" must fit into two lines");
                if (!brief.Contains("very long"))
                {
                    Assert.False(toast.Contains(ToastText.Ellipsis), $"\"{toast}\" must fit without clipping");
                }

                foreach (var line in lines)
                {
                    Assert.True(line.Length <= ToastText.MaxLineLength, $"Line \"{line}\" is longer than {ToastText.MaxLineLength} characters");
                }
            }
        }

        yield break;
    }
}
