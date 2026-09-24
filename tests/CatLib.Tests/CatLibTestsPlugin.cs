using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using CatLib.Core;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;
using CatLib.Tests.Timeline;
using UnityEngine;

namespace CatLib.Tests;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency(CatLib.PluginMeta.Guid)]
public sealed class CatLibTestsPlugin : BasePlugin
{
    private ConfigEntry<bool> _runOnMainMenu;
    private ConfigEntry<float> _fallbackDelaySeconds;
    private ConfigEntry<KeyboardShortcut> _runHotkey;
    private CatLogger _log;
    private TestRunner _runner;
    private TestReportWriter _reportWriter;
    private TimelineRecorder _timeline;
    private bool _autoRunHandled;

    public override void Load()
    {
        _log = CatLogger.From(Log);

        _runOnMainMenu = Config.Bind("Run", "OnMainMenu", true,
            "Run all tests automatically the first time the main menu is loaded.");
        _fallbackDelaySeconds = Config.Bind("Run", "FallbackDelaySeconds", 45f,
            "Run all tests after this many seconds if the main menu event was never observed. Set to 0 to disable.");
        _runHotkey = Config.Bind("Run", "Hotkey", new KeyboardShortcut(KeyCode.F10),
            "Run all tests on demand.");

        var outputDirectory = Path.Combine(Paths.BepInExRootPath, "CatLib.Tests");
        ConfigSandbox.RootDirectory = Path.Combine(outputDirectory, "Sandbox");
        ResetSandbox();
        _timeline = new TimelineRecorder(Path.Combine(outputDirectory, "Timelines"), _log.Scope("Timeline"));
        _timeline.Start();

        _reportWriter = new TestReportWriter(Path.Combine(outputDirectory, "Reports"));
        _runner = new TestRunner(TestRegistry.Discover(typeof(CatLibTestsPlugin).Assembly), _log.Scope("Runner"));
        _runner.Completed += OnRunCompleted;

        DeclareDemoSettings();

        BootstrapEvents.MainMenuLoaded += OnMainMenuLoaded;
        FrameLoop.Update += OnUpdate;

        _log.Info($"CatLib.Tests {PluginMeta.Version} loaded with {_runner.TestCount} tests. Press {_runHotkey.Value} to run them.");
    }

    private void DeclareDemoSettings()
    {
        var settings = CatSettings.For(this);

        settings.Local("Demo", "Message", "Hello from CatLib",
                "Edit while the game is running to check live reload.")
            .Apply(value => _log.Message($"Demo.Message is now \"{value}\""));

        settings.Local("Demo", "Volume", 50,
                "Edit while the game is running. Values outside 0-100 are adjusted.", new AcceptableValueRange<int>(0, 100))
            .Apply(value => _log.Message($"Demo.Volume is now {value}"));

        settings.Local("Demo", "FastMode", false,
                "Changes to this value only apply after a restart.")
            .RequiresRestart()
            .Apply(value => _log.Message($"Demo.FastMode is {value}"));
    }

    private void ResetSandbox()
    {
        try
        {
            if (Directory.Exists(ConfigSandbox.RootDirectory))
            {
                Directory.Delete(ConfigSandbox.RootDirectory, true);
            }
        }
        catch (System.Exception exception)
        {
            _log.Warning($"Could not clean {ConfigSandbox.RootDirectory}: {exception.Message}");
        }
    }

    private void OnMainMenuLoaded()
    {
        if (_autoRunHandled || !_runOnMainMenu.Value)
        {
            return;
        }

        _autoRunHandled = true;
        _runner.Start("MainMenuLoaded");
    }

    private void OnUpdate()
    {
        if (!_autoRunHandled && _fallbackDelaySeconds.Value > 0 && FrameLoop.Realtime >= _fallbackDelaySeconds.Value)
        {
            _autoRunHandled = true;
            _log.Warning($"Main menu event was not observed within {_fallbackDelaySeconds.Value:0} s, running tests anyway");
            _runner.Start("FallbackTimeout");
        }

        if (_runHotkey.Value.IsDown())
        {
            _runner.Start("Hotkey");
        }

        _runner.Update();
    }

    private void OnRunCompleted(TestRunSummary summary)
    {
        try
        {
            var path = _reportWriter.Write(summary);
            _log.Message($"Test report written to {path}");
        }
        catch (System.Exception exception)
        {
            _log.Error("Failed to write test report", exception);
        }
    }
}
