using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class ReloadLogOrderTest : TestCase
{
    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("LogOrder");
        var fileName = Path.GetFileName(sandbox.FilePath);
        var order = new List<string>();
        var listener = new CapturingListener(message =>
        {
            if (message.Contains("Reloaded " + fileName))
            {
                order.Add("summary");
            }
            else if (message.Contains(sandbox.OwnerId + ": value"))
            {
                order.Add("warning");
            }
        });

        var setting = sandbox.Settings.Local("General", "Limit", 3, "Limit.", new AcceptableValueRange<int>(1, 10));
        setting.Apply(value => order.Add("apply " + value));
        order.Clear();

        BepInEx.Logging.Logger.Listeners.Add(listener);
        try
        {
            sandbox.WriteValue("General", "Limit", "50");
            yield return Wait.Until(() => sandbox.Reports.Count >= 1, 5, "the edit to be reloaded");
        }
        finally
        {
            BepInEx.Logging.Logger.Listeners.Remove(listener);
        }

        context.Note("Order: " + string.Join(" -> ", order));
        Assert.SequenceEqual(new[] { "summary", "warning", "apply 10" }, order, "Order of the reload summary, the value warning and the applier");
    }

    private sealed class CapturingListener : ILogListener
    {
        private readonly Action<string> _capture;

        public CapturingListener(Action<string> capture)
        {
            _capture = capture;
        }

        public LogLevel LogLevelFilter => LogLevel.All;

        public void LogEvent(object sender, LogEventArgs eventArgs) => _capture(eventArgs.Data?.ToString() ?? string.Empty);

        public void Dispose()
        {
        }
    }
}
