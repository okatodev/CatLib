using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CatLib.Config;

namespace CatLib.Tests.Suites.Settings;

internal sealed class ConfigSandbox : IDisposable
{
    private readonly List<ConfigReloadReport> _reports = new();
    private readonly List<SettingValueProblem> _rejected = new();
    private readonly List<SettingValueProblem> _adjusted = new();
    private readonly List<ISetting> _restartRequired = new();

    public ConfigSandbox(string name)
    {
        if (string.IsNullOrEmpty(RootDirectory))
        {
            throw new InvalidOperationException("ConfigSandbox.RootDirectory is not set");
        }

        Directory.CreateDirectory(RootDirectory);
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        FilePath = Path.GetFullPath(Path.Combine(RootDirectory, name + "_" + suffix + ".cfg"));
        OwnerId = "catlib.tests." + name.ToLowerInvariant() + "." + suffix;
        Config = new ConfigFile(FilePath, true);
        Settings = CatSettings.For(Config, OwnerId);

        CatConfig.FileReloaded += OnFileReloaded;
        CatConfig.ValueRejected += OnValueRejected;
        CatConfig.ValueAdjusted += OnValueAdjusted;
        CatConfig.RestartRequired += OnRestartRequired;
    }

    public static string RootDirectory { get; set; }

    public string FilePath { get; }

    public string OwnerId { get; }

    public ConfigFile Config { get; }

    public CatSettings Settings { get; }

    public IReadOnlyList<ConfigReloadReport> Reports => _reports;

    public IReadOnlyList<SettingValueProblem> Rejected => _rejected;

    public IReadOnlyList<SettingValueProblem> Adjusted => _adjusted;

    public IReadOnlyList<ISetting> RestartRequired => _restartRequired;

    public static string ReplaceValue(string text, string section, string key, string rawValue)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var currentSection = string.Empty;
        var replaced = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line.Substring(1, line.Length - 2);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal) || currentSection != section)
            {
                continue;
            }

            var split = line.Split(new[] { '=' }, 2);
            if (split.Length == 2 && split[0].Trim() == key)
            {
                lines[index] = key + " = " + rawValue;
                replaced = true;
            }
        }

        if (!replaced)
        {
            throw new InvalidOperationException($"[{section}] {key} was not found in the config text");
        }

        return string.Join(newline, lines);
    }

    public string ContentWith(string section, string key, string rawValue) =>
        ReplaceValue(File.ReadAllText(FilePath), section, key, rawValue);

    public void WriteValue(string section, string key, string rawValue) =>
        File.WriteAllText(FilePath, ContentWith(section, key, rawValue));

    public void Dispose()
    {
        CatConfig.FileReloaded -= OnFileReloaded;
        CatConfig.ValueRejected -= OnValueRejected;
        CatConfig.ValueAdjusted -= OnValueAdjusted;
        CatConfig.RestartRequired -= OnRestartRequired;
        Settings.Dispose();

        try
        {
            File.Delete(FilePath);
        }
        catch (Exception)
        {
        }
    }

    private void OnFileReloaded(ConfigReloadReport report)
    {
        if (report.OwnerId == OwnerId)
        {
            _reports.Add(report);
        }
    }

    private void OnValueRejected(SettingValueProblem problem)
    {
        if (problem.OwnerId == OwnerId)
        {
            _rejected.Add(problem);
        }
    }

    private void OnValueAdjusted(SettingValueProblem problem)
    {
        if (problem.OwnerId == OwnerId)
        {
            _adjusted.Add(problem);
        }
    }

    private void OnRestartRequired(ISetting setting)
    {
        if (setting.Owner == Settings)
        {
            _restartRequired.Add(setting);
        }
    }
}
