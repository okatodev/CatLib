using System;
using System.IO;
using System.Text;
using BepInEx;
using CatLib.Game;

namespace CatLib.Tests.Framework;

public sealed class TestReportWriter
{
    private readonly string _directory;

    public TestReportWriter(string directory)
    {
        _directory = directory;
    }

    public string Write(TestRunSummary summary)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "report_" + InvariantFormat.FileStamp(summary.StartedAt) + ".txt");

        var builder = new StringBuilder();
        builder.AppendLine("CatLib test report");
        builder.AppendLine("Started:        " + InvariantFormat.Timestamp(summary.StartedAt));
        builder.AppendLine($"Trigger:        {summary.Trigger}");
        builder.AppendLine("Duration:       " + InvariantFormat.Seconds(summary.Duration));
        builder.AppendLine($"CatLib:         {CatLib.PluginMeta.Version}");
        builder.AppendLine($"CatLib.Tests:   {PluginMeta.Version}");
        builder.AppendLine($"BepInEx:        {Paths.BepInExVersion}");
        builder.AppendLine($"Unity:          {Safe(() => GameInfo.UnityVersion)}");
        builder.AppendLine($"Game version:   {Safe(() => GameInfo.GameVersion)}");
        builder.AppendLine($"Result:         {summary.Count(TestStatus.Passed)} passed, {summary.Count(TestStatus.Failed)} failed, " +
                           $"{summary.Count(TestStatus.Errored)} errored, {summary.Count(TestStatus.TimedOut)} timed out");
        builder.AppendLine();

        foreach (var result in summary.Results)
        {
            builder.AppendLine(InvariantFormat.ResultLine(result));
            if (!string.IsNullOrEmpty(result.Message))
            {
                foreach (var line in result.Message.Split('\n'))
                {
                    builder.AppendLine("    ! " + line.TrimEnd('\r'));
                }
            }

            foreach (var note in result.Notes)
            {
                builder.AppendLine("    - " + note);
            }
        }

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static string Safe(Func<string> read)
    {
        try
        {
            return read() ?? "<unavailable>";
        }
        catch (Exception exception)
        {
            return "<error: " + exception.Message + ">";
        }
    }
}
