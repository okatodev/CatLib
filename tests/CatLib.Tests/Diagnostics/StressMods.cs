using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Logging;
using CatLib.Tests.Suites.Presentation;

namespace CatLib.Tests.Diagnostics;

public sealed class StressMods
{
    public const int SmallModCount = 12;

    private readonly string _directory;
    private readonly CatLogger _log;
    private readonly List<CatSettings> _mods = new();

    public StressMods(string directory, CatLogger log)
    {
        _directory = directory;
        _log = log;
    }

    public bool IsActive => _mods.Count > 0;

    public void Toggle()
    {
        if (IsActive)
        {
            Remove();
        }
        else
        {
            Create();
        }
    }

    private void Create()
    {
        Directory.CreateDirectory(_directory);
        var big = Open("catlib.tests.stress", "CatLib Stress", "1.0.0");
        for (var section = 1; section <= 5; section++)
        {
            var name = "Section " + section;
            big.Local(name, "Toggle" + section, section % 2 == 0, "A toggle in section " + section + ".");
            big.Local(name, "Whole" + section, section * 10, "A whole number slider.", new AcceptableValueRange<int>(0, 100));
            big.Local(name, "Fraction" + section, 0.5f * section, "A fractional slider.", new AcceptableValueRange<float>(0f, 5f));
            big.Local(name, "Mode" + section, SampleMode.FastMode, "An enum dropdown.");
            big.Session(name, "Shared" + section, "Red", "A session setting with listed values.", new AcceptableValueList<string>("Red", "Green", "Blue"));
            big.Local(name, "AVeryLongSettingKeyThatShouldBeCutOffNicely" + section, "text",
                "A deliberately long description that must wrap or be cut inside the context line without leaving the settings pane, repeated to be sure it overflows two lines of text in the game font.")
                .RequiresRestart();
        }

        for (var index = 1; index <= SmallModCount; index++)
        {
            var small = Open("catlib.tests.stress." + index.ToString("00"), "CatLib Stress " + index.ToString("00"), "0." + index + ".0");
            small.Local("General", "Enabled", true, "Small mod " + index + ".");
        }

        _log.Message($"Created {_mods.Count} stress mods for UI checks. Press the key again to remove them");
    }

    private CatSettings Open(string ownerId, string displayName, string version)
    {
        var file = new ConfigFile(Path.Combine(_directory, ownerId + ".cfg"), true);
        var settings = CatSettings.For(file, ownerId, displayName, version);
        _mods.Add(settings);
        return settings;
    }

    private void Remove()
    {
        foreach (var settings in _mods)
        {
            var path = settings.FilePath;
            settings.Dispose();
            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
            {
                _log.Warning($"Could not delete {path}: {exception.Message}");
            }
        }

        _log.Message($"Removed {_mods.Count} stress mods");
        _mods.Clear();
    }
}
