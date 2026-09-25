using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using CatLib.Core;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Tests.Diagnostics;
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
    private ConfigEntry<KeyboardShortcut> _dumpUiHotkey;
    private ConfigEntry<KeyboardShortcut> _stressHotkey;
    private ConfigEntry<KeyboardShortcut> _probeHotkey;
    private ConfigEntry<KeyboardShortcut> _selfCheckHotkey;
    private ConfigEntry<KeyboardShortcut> _toastPreviewHotkey;
    private ConfigEntry<KeyboardShortcut> _entityDumpHotkey;
    private ConfigEntry<string> _entityDumpTypes;
    private CatLib.Tests.Diagnostics.Inspection.EntityInspector _entityInspector;
    private NetworkProbes _probes;
    private UiHierarchyDumper _uiDumper;
    private StressMods _stressMods;
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
        _dumpUiHotkey = Config.Bind("Diagnostics", "DumpUiHotkey", new KeyboardShortcut(KeyCode.F9),
            "Write the hierarchy of the open settings menu to BepInEx/CatLib.Tests/Dumps.");
        _stressHotkey = Config.Bind("Diagnostics", "StressModsHotkey", new KeyboardShortcut(KeyCode.F8),
            "Create or remove a set of stress mods with many settings to check the Mods tab layout.");
        _probeHotkey = Config.Bind("Diagnostics", "NetworkProbeHotkey", new KeyboardShortcut(KeyCode.F7),
            "Send the next network probe to the other players.");
        _entityDumpHotkey = Config.Bind("Diagnostics", "EntityDumpHotkey", new KeyboardShortcut(KeyCode.F4),
            "Write what the camera looks at and the game objects of the focus types to BepInEx/CatLib.Tests/Dumps.");
        _entityDumpTypes = Config.Bind("Diagnostics", "EntityDumpTypes", CatLib.Tests.Diagnostics.Inspection.EntityInspector.DefaultFocusTypes,
            "Comma separated game types to list in every entity dump.");
        _toastPreviewHotkey = Config.Bind("Diagnostics", "NotificationPreviewHotkey", new KeyboardShortcut(KeyCode.F5),
            "Post a sample network message, to check how a game notification looks in a level.");
        _selfCheckHotkey = Config.Bind("Diagnostics", "SteamSelfCheckHotkey", new KeyboardShortcut(KeyCode.F6),
            "Send a message to yourself through the CatLib Steam channel to check the Steam calls without another player.");

        var outputDirectory = Path.Combine(Paths.BepInExRootPath, "CatLib.Tests");
        ConfigSandbox.RootDirectory = Path.Combine(outputDirectory, "Sandbox");
        ResetSandbox();
        _timeline = new TimelineRecorder(Path.Combine(outputDirectory, "Timelines"), _log.Scope("Timeline"));
        _timeline.Start();

        _uiDumper = new UiHierarchyDumper(Path.Combine(outputDirectory, "Dumps"), _log.Scope("UiDump"));
        _stressMods = new StressMods(Path.Combine(outputDirectory, "Stress"), _log.Scope("Stress"));
        _probes = new NetworkProbes(_log.Scope("Probe"));
        _entityInspector = new CatLib.Tests.Diagnostics.Inspection.EntityInspector(Path.Combine(outputDirectory, "Dumps"), () => _entityDumpTypes.Value, _log.Scope("Inspect"));
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

        settings.Local("Demo", "NetworkModVersion", "1.0.0",
                "Version of the demo network mod that every player needs. Change it on one side to test a mismatch.")
            .Apply(value =>
            {
                CatNetwork.Declare("catlib.tests.demo-network", "CatLib Demo Network Mod", value, SessionPolicy.RequiredOnAll);
                _log.Message($"Demo network mod declared as version {value}");
            });

        settings.Session("Demo", "SharedLimit", 3,
                "A session setting. In multiplayer the host's value applies to everyone.", new AcceptableValueRange<int>(1, 10))
            .Apply(value => _log.Message($"Demo.SharedLimit is now {value}"));
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

        if (_probeHotkey.Value.IsDown())
        {
            _probes.SendNext();
        }

        if (_entityDumpHotkey.Value.IsDown())
        {
            try
            {
                _entityInspector.Dump();
            }
            catch (System.Exception exception)
            {
                _log.Error("Entity inspection failed", exception);
            }
        }

        if (_toastPreviewHotkey.Value.IsDown())
        {
            try
            {
                var language = CatLib.UI.UiText.LanguageCode;
                CatLib.UI.PlayerMessages.Post(
                    CatLib.UI.UiText.Format(CatLib.UI.UiText.NetPlayerDisconnected, language, "RENTAI", "CatLib Demo Network Mod 1.0.0 / 2.0.0"),
                    CatLib.UI.UiText.Format(CatLib.UI.UiText.NetPlayerDisconnectedBrief, language, "RENTAI", "CatLib Demo Network Mod"));
                _log.Message("Posted a sample network message, it shows as a game notification in a level");
            }
            catch (System.Exception exception)
            {
                _log.Error("Posting a sample message failed", exception);
            }
        }

        if (_selfCheckHotkey.Value.IsDown())
        {
            try
            {
                SessionNetwork.RunSelfCheck();
            }
            catch (System.Exception exception)
            {
                _log.Error("Steam self check failed", exception);
            }
        }

        if (_stressHotkey.Value.IsDown())
        {
            try
            {
                _stressMods.Toggle();
            }
            catch (System.Exception exception)
            {
                _log.Error("Toggling the stress mods failed", exception);
            }
        }

        if (_dumpUiHotkey.Value.IsDown())
        {
            try
            {
                _uiDumper.DumpSettingsMenus();
            }
            catch (System.Exception exception)
            {
                _log.Error("UI dump failed", exception);
            }
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
