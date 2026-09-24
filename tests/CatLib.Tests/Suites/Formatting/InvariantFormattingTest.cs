using System;
using System.Collections.Generic;
using System.Globalization;
using CatLib.Tests.Framework;
using CatLib.Tests.Timeline;

namespace CatLib.Tests.Suites.Formatting;

public sealed class InvariantFormattingTest : TestCase
{
    private const string CommaDecimalCulture = "de-DE";

    public override string Suite => "Formatting";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var original = CultureInfo.CurrentCulture;
        string timelineLine;
        string resultLine;
        string seconds;
        string timestamp;
        string assertMessage;
        string cultureDecimal;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(CommaDecimalCulture);
            cultureDecimal = (1.5).ToString(CultureInfo.CurrentCulture);
            timelineLine = TimelineRecorder.FormatLine(3.187, 21, "Test.Event", "value=1");
            resultLine = InvariantFormat.ResultLine(new TestResult("Suite", "Name", TestStatus.Passed, TimeSpan.FromMilliseconds(12.4), 3, null, Array.Empty<string>()));
            seconds = InvariantFormat.Seconds(TimeSpan.FromMilliseconds(990));
            timestamp = InvariantFormat.Timestamp(new DateTime(2026, 9, 25, 1, 34, 18));
            assertMessage = Assert.Throws<AssertionException>(() => Assert.Equal(1.5, 2.5, "Value"), "Assert.Equal on doubles").Message;
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        context.Note($"Culture {CommaDecimalCulture} formats 1.5 as \"{cultureDecimal}\"");
        context.Note($"Timeline line: \"{timelineLine}\"");

        Assert.Equal("1,5", cultureDecimal, "Precondition: the test culture must use a decimal comma");
        Assert.Equal("        3.187       21  Test.Event value=1", timelineLine, "Timeline line");
        Assert.Equal("PASSED   Suite/Name (12 ms, 3 frames)", resultLine, "Result line");
        Assert.Equal("0.99 s", seconds, "Seconds");
        Assert.Equal("2026-09-25 01:34:18", timestamp, "Timestamp");
        Assert.Equal("Value: expected <1.5>, actual <2.5>", assertMessage, "Assertion message");
        yield break;
    }
}
