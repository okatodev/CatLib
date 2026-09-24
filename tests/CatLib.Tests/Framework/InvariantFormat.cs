using System;
using System.Globalization;

namespace CatLib.Tests.Framework;

public static class InvariantFormat
{
    public static string Seconds(TimeSpan duration) => duration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";

    public static string ShortSeconds(TimeSpan duration) => duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";

    public static string Milliseconds(TimeSpan duration) => duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";

    public static string Timestamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string FileStamp(DateTime time) => time.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

    public static string Value(object value) => value switch
    {
        null => "null",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    public static string ResultLine(TestResult result) =>
        $"{result.Status.ToString().ToUpperInvariant(),-8} {result.FullName} ({Milliseconds(result.Duration)}, {result.Frames.ToString(CultureInfo.InvariantCulture)} frames)";
}
