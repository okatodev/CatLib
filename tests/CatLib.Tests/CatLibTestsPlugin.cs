using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using CatLib.Core;
using CatLib.DevTools;
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
    private ConfigEntry<string> _entityDumpTypes;
    private CatLib.Tests.Diagnostics.Inspection.EntityInspector _entityInspector;
    private CatLib.Tests.Diagnostics.Inspection.LabelCloneExperiment _labelClone;
    private MessageProbe _probes;
    private SaveProbe _saveProbe;
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
        _entityDumpTypes = Config.Bind("Diagnostics", "EntityDumpTypes", CatLib.Tests.Diagnostics.Inspection.EntityInspector.DefaultFocusTypes,
            "Comma separated game types to list in every entity dump.");

        var outputDirectory = Path.Combine(Paths.BepInExRootPath, "CatLib.Tests");
        ConfigSandbox.RootDirectory = Path.Combine(outputDirectory, "Sandbox");
        ResetSandbox();
        _timeline = new TimelineRecorder(Path.Combine(outputDirectory, "Timelines"), _log.Scope("Timeline"));
        _timeline.Start();

        _uiDumper = new UiHierarchyDumper(Path.Combine(outputDirectory, "Dumps"), _log.Scope("UiDump"));
        _stressMods = new StressMods(Path.Combine(outputDirectory, "Stress"), _log.Scope("Stress"));
        _probes = new MessageProbe(_log.Scope("Probe"));
        _saveProbe = new SaveProbe(PluginMeta.Version, _log.Scope("SaveProbe"));
        _labelClone = new CatLib.Tests.Diagnostics.Inspection.LabelCloneExperiment(_log.Scope("LabelClone"));
        _entityInspector = new CatLib.Tests.Diagnostics.Inspection.EntityInspector(Path.Combine(outputDirectory, "Dumps"), () => _entityDumpTypes.Value, _log.Scope("Inspect"));
        _reportWriter = new TestReportWriter(Path.Combine(outputDirectory, "Reports"));
        _runner = new TestRunner(TestRegistry.Discover(typeof(CatLibTestsPlugin).Assembly), _log.Scope("Runner"));
        _runner.Completed += OnRunCompleted;

        DeclareDemoSettings();
        RegisterDevCommands();

        BootstrapEvents.MainMenuLoaded += OnMainMenuLoaded;
        FrameLoop.Update += OnUpdate;

        _log.Info($"CatLib.Tests {PluginMeta.Version} loaded with {_runner.TestCount} tests. Run them again from the developer menu.");
    }

    private void RegisterDevCommands()
    {
        DevMenu.Command("Tests", "Run all tests", () =>
        {
            _runner.Start("DevMenu");
            return $"{_runner.TestCount} test(s) started, the report is written when they finish";
        }, "Runs every test again. They also run by themselves when the main menu loads.");
        DevMenu.Toggle("Tests", "Run on main menu", () => _runOnMainMenu.Value, value => _runOnMainMenu.Value = value,
            "Runs all tests the first time the main menu loads. Saved as OnMainMenu in section [Run] of catlib.tests.cfg.");
        DevMenu.Command("Inspect", "Entity dump", () =>
        {
            _entityInspector.Dump();
            return "written to BepInEx/CatLib.Tests/Dumps";
        }, "Writes what the camera looks at, with components, fields and object tree, and the nearest objects of the focus types.");
        DevMenu.Command("Inspect", "Settings menu dump", () =>
        {
            _uiDumper.DumpSettingsMenus();
            return "written to BepInEx/CatLib.Tests/Dumps";
        }, "Writes the hierarchy of the open settings menu.");
        DevMenu.Command("Inspect", "Label clone experiment", () =>
        {
            _labelClone.Toggle();
            return "see the log";
        }, "Copies the shelf label the camera looks at, press again to remove the copies. Superseded by the Shelf Labels mod.");
        DevMenu.Command("Network", "Mod message probe", () =>
        {
            _probes.SendPing();
            return "see the log";
        }, "Sends a mod message to the host, which answers every player. Works alone too.");
        DevMenu.Command("Network", "Steam self check", () =>
        {
            SessionNetwork.RunSelfCheck();
            return "see the log";
        }, "Sends a message to yourself through the CatLib Steam channel.");
        DevMenu.Command("UI", "Notification preview", () =>
        {
            var language = CatLib.UI.UiText.LanguageCode;
            CatLib.UI.PlayerMessages.Post(
                CatLib.UI.UiText.Format(CatLib.UI.UiText.NetPlayerDisconnected, language, "RENTAI", "CatLib Demo Network Mod 1.0.0 / 2.0.0"),
                CatLib.UI.UiText.Format(CatLib.UI.UiText.NetPlayerDisconnectedBrief, language, "RENTAI", "CatLib Demo Network Mod"));
            return "posted, it shows as a game notification in a level";
        }, "Posts a sample network message.");
        DevMenu.Command("UI", "Stress mods on or off", () =>
        {
            _stressMods.Toggle();
            return "see the Mods tab";
        }, "Creates or removes a set of mods with many settings to check the Mods tab layout.");
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
        if (!_autoRunHandled && _runOnMainMenu.Value && _fallbackDelaySeconds.Value > 0 && FrameLoop.Realtime >= _fallbackDelaySeconds.Value)
        {
            _autoRunHandled = true;
            _log.Warning($"Main menu event was not observed within {_fallbackDelaySeconds.Value:0} s, running tests anyway");
            _runner.Start("FallbackTimeout");
        }

        _labelClone.Update();
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
