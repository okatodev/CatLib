using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CatLib.Logging;
using CatLib.Patching;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Patching;

public sealed class CatPatchesTest : TestCase
{
    private static CatPatches _group;
    private static int _seen;

    public override string Suite => "Patching";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var log = CatLogger.Create("CatLib.Tests.Patches");
        var missing = new CatPatches("catlib.tests.patches.missing", log)
            .Postfix(typeof(Target), nameof(Target.Twice), new[] { typeof(int) }, typeof(CatPatchesTest), nameof(TwicePostfix))
            .Postfix(typeof(Target), "NoSuchMethod", new[] { typeof(int) }, typeof(CatPatchesTest), nameof(TwicePostfix));
        Assert.False(missing.Apply(), "A group with a missing method is not applied");
        Assert.False(missing.IsApplied, "Nothing of it is patched");
        Assert.Equal(1, missing.Problems.Count, "The missing method is named");
        Assert.Equal(8, new Target().Twice(4), "The present method stays as it was");

        _seen = 0;
        _group = new CatPatches("catlib.tests.patches", log)
            .Postfix(typeof(Target), nameof(Target.Twice), new[] { typeof(int) }, typeof(CatPatchesTest), nameof(TwicePostfix));
        try
        {
            Assert.True(_group.Apply(), "A complete group is applied");
            Assert.Equal(9, new Target().Twice(4), "The postfix changes the result");
            Assert.Equal(1, _seen, "The postfix ran once");

            context.Note($"{CatPatches.LoggedErrorsPerHandler} error log lines about TwicePostfix are expected");
            for (var index = 0; index < CatPatches.ErrorsBeforeTurningOff - 1; index++)
            {
                Assert.Equal(-1, _group.Run(nameof(TwicePostfix), () => Fail(), -1), "A failing handler gives the fallback");
            }

            Assert.True(_group.IsActive, "A few errors leave the patches on");
            var turnedOff = 0;
            _group.TurnedOff += () => turnedOff++;
            _group.Run(nameof(TwicePostfix), () => Fail());
            Assert.False(_group.IsActive, "Too many errors turn the patches off");
            Assert.Equal(1, turnedOff, "The mod is told once");
            Assert.Equal(8, new Target().Twice(4), "A turned off group gives the game's own result");
        }
        finally
        {
            _group.Remove();
        }

        Assert.Equal(8, new Target().Twice(4), "Removing the patches restores the method");
        yield break;
    }

    private static int Fail() => throw new InvalidOperationException("Expected exception thrown by CatLib.Tests");

    private static void TwicePostfix(ref int __result)
    {
        if (_group == null || !_group.IsActive)
        {
            return;
        }

        _seen++;
        __result += 1;
    }

    private sealed class Target
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Twice(int value) => value * 2;
    }
}
