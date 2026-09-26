using System;
using System.Globalization;
using CatLib.Logging;
using CatLib.Saves;

namespace CatLib.Tests.Diagnostics;

internal sealed class SaveProbe
{
    public const string ModId = "catlib.tests.saveprobe";
    public const string CountKey = "gameSaves";
    public const string LastKey = "lastGameSave";

    private readonly CatLogger _log;
    private readonly ModSave _save;

    public SaveProbe(string version, CatLogger log)
    {
        _log = log;
        _save = CatSaves.For(ModId, version);
        _save.Loaded += OnLoaded;
        _save.Saving += OnSaving;
        CatSaves.Committed += OnCommitted;
    }

    private void OnLoaded()
    {
        _log.Message($"Save probe attached to {_save.SaveName}: read={_save.LastRead} state={_save.State} saves so far={_save.Get(CountKey, 0)} last={_save.Get(LastKey, "never")} folder={CatSaves.Root}");
        if (_save.LastReadDetail != null)
        {
            _log.Message("Save probe read detail: " + _save.LastReadDetail);
        }
    }

    private void OnSaving()
    {
        var count = _save.Get(CountKey, 0) + 1;
        _save.Set(CountKey, count);
        _save.Set(LastKey, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private void OnCommitted(SaveCommitReport report)
    {
        var outcome = report.Skipped ? "skipped: " + report.SkippedReason : $"written=[{string.Join(", ", report.Written)}] failed=[{string.Join(", ", report.Failed)}]";
        _log.Message($"Save probe: game saved {report.SaveName}, {outcome}, probe count={_save.Get(CountKey, 0)}{(report.ArchivedTo != null ? ", archived old data to " + report.ArchivedTo : string.Empty)}");
    }
}
