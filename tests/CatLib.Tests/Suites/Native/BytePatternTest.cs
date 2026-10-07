using System;
using System.Collections.Generic;
using CatLib.Patching;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Native;

public sealed class BytePatternTest : TestCase
{
    public override string Suite => "Native";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var pattern = BytePattern.Parse("E8 ?? ?? ?? ?? 83 F8 05 0F 8D");
        Assert.Equal(10, pattern.Length, "Every byte and wildcard counts");
        Assert.Equal("E8 ?? ?? ?? ?? 83 F8 05 0F 8D", pattern.ToString(), "The pattern prints back");

        var code = new byte[] { 0x90, 0xE8, 0xF7, 0xF8, 0xA5, 0x00, 0x83, 0xF8, 0x05, 0x0F, 0x8D, 0xBE, 0x05, 0x00, 0x00 };
        Assert.SequenceEqual(new[] { 1 }, pattern.FindAll(code), "Found once, wildcards match any byte");
        Assert.True(CodePatch.TryFindSite(code, pattern, 7, 1, out var site, out _), "A single match is a site");
        Assert.Equal(8, site, "The site is the byte to change inside the match");

        var twice = new byte[code.Length * 2];
        code.CopyTo(twice, 0);
        code.CopyTo(twice, code.Length);
        Assert.False(CodePatch.TryFindSite(twice, pattern, 7, 1, out _, out var ambiguous), "Two matches are not certain");
        context.Note(ambiguous);
        Assert.True(ambiguous.Contains("2 times"), "The problem names how often the code was found");

        code[8] = 0x09;
        Assert.False(CodePatch.TryFindSite(code, pattern, 7, 1, out _, out var missing), "A changed byte no longer matches");
        Assert.True(missing.Contains("not found"), "The problem says the code was not found");
        Assert.False(CodePatch.TryFindSite(code, pattern, 9, 3, out _, out _), "The bytes to change must lie inside the pattern");

        var threw = false;
        try
        {
            BytePattern.Parse("E8 ZZ");
        }
        catch (FormatException)
        {
            threw = true;
        }

        Assert.True(threw, "A wrong byte is reported");
        threw = false;
        try
        {
            BytePattern.Parse("?? ??");
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        Assert.True(threw, "A pattern of wildcards only is refused");
        yield break;
    }
}
