using System;
using System.Collections.Generic;
using System.Linq;
using BetterRepair.Logic;
using BetterRepair.Scene;
using BetterRepair.Settings;
using BetterRepair.Sync;
using CatLib.Core;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.UI;

namespace BetterRepair;

public sealed class BetterRepairController
{
    public const double ScanIntervalSeconds = 2;

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly List<WorkstationView> _tables = new();
    private readonly DayClock _clock = new();
    private double _nextScan;
    private bool _hostStockKnown;
    private bool _reported;
    private bool _paused;

    public BetterRepairController(CatLogger log, TextCatalog texts)
    {
        _log = log;
        _texts = texts;
        Stock = new CardboardStock(CardboardStock.SheetsOnTable);
    }

    public RepairSettings Settings { get; set; }

    public StockSync Sync { get; set; }

    public CardboardStock Stock { get; }

    public int CurrentStock => Stock.Current;

    public void Initialize()
    {
        ReadSettings();
        Stock.SetCurrent(Stock.Maximum);
        Sync.StockReceived += OnStockReceived;
    }

    public void OnSettingsChanged()
    {
        var before = Stock.Current;
        ReadSettings();
        _log.Info($"Cardboard settings: {(Stock.Unlimited ? "unlimited" : $"stock {Stock.Maximum}")}, refill {Describe(Settings.Refill.Value, Settings.RefillAmount.Value)}, now {Stock.Describe()}");
        if (CatNetwork.IsAuthority && before != Stock.Current)
        {
            Sync.Broadcast(Stock.Current);
        }

        ApplyAll();
    }

    public void OnStoreLoaded(int? stored)
    {
        ReadSettings();
        Stock.SetCurrent(stored ?? Stock.Maximum);
        ApplyAll();
    }

    public void OnMainMenuLoaded()
    {
        Forget();
        ReadSettings();
        Stock.SetCurrent(Stock.Maximum);
        _hostStockKnown = false;
    }

    public void Forget()
    {
        _tables.Clear();
        _clock.Reset();
        _reported = false;
        _nextScan = 0;
    }

    public void Update()
    {
        if (!CatNetwork.IsActive(PluginMeta.Guid))
        {
            if (!_paused)
            {
                _paused = true;
                Forget();
                _log.Info("Better Repair is paused in this session because not every player has it, the repair table works like in the game");
            }

            return;
        }

        if (_paused)
        {
            _paused = false;
            _log.Info("Better Repair is active again in this session");
        }

        try
        {
            Track();
            if (_tables.Count == 0)
            {
                return;
            }

            WatchDay();
            WatchTables();
        }
        catch (Exception exception)
        {
            if (!_reported)
            {
                _reported = true;
                _log.Error("Updating the repair table failed, the mod stops touching it until the next level", exception);
            }

            _tables.Clear();
        }
    }

    public string Describe()
    {
        var parts = new List<string> { $"cardboard {Stock.Describe()}", $"role {CatNetwork.Role}", _paused ? "paused in this session" : "active" };
        if (!CatNetwork.IsAuthority)
        {
            parts.Add(_hostStockKnown ? "stock from the host" : "waiting for the host");
        }

        parts.Add($"period {(_clock.Last.HasValue ? Period(_clock.Last.Value) : "unknown")}");
        foreach (var table in _tables.Where(table => table.IsAlive))
        {
            parts.Add($"{table.Name}: game index {table.GameIndex}, {table.DescribeButtons()}");
        }

        if (_tables.Count == 0)
        {
            parts.Add("no repair table found");
        }

        var text = string.Join("; ", parts);
        _log.Info("Repair tables: " + text);
        return $"{Stock.Describe()}, {_tables.Count} table(s), see the log";
    }

    public string RefillNow()
    {
        if (!CatNetwork.IsAuthority)
        {
            return "only the host refills";
        }

        Refill("the developer menu");
        return Stock.Describe();
    }

    private void ReadSettings()
    {
        if (Settings == null)
        {
            return;
        }

        Stock.Unlimited = Settings.Unlimited.Value;
        Stock.SetMaximum(Settings.Stock.Value);
    }

    private void Track()
    {
        _tables.RemoveAll(table => !table.IsAlive);
        if (_tables.Count > 0 || FrameLoop.Realtime < _nextScan)
        {
            return;
        }

        _nextScan = FrameLoop.Realtime + ScanIntervalSeconds;
        foreach (var workstation in WorkstationView.FindAll())
        {
            var view = new WorkstationView(workstation);
            if (_tables.All(table => table.Pointer != view.Pointer))
            {
                _tables.Add(view);
                _log.Info($"Found the repair table {view.Name}: game index {view.GameIndex}, {view.Capacity} sheet(s) on the table, cardboard {Stock.Describe()}");
                Apply(view);
            }
        }
    }

    private void WatchDay()
    {
        if (!Singleton<GameTimeManager>.HasInstance())
        {
            return;
        }

        var period = (int)Singleton<GameTimeManager>.Instance.CurrentTimePeriod;
        var newDay = _clock.Observe(period, out var changed);
        if (!changed)
        {
            return;
        }

        _log.Info($"Time period is now {Period(period)}, game index {string.Join(", ", _tables.Select(table => table.GameIndex))}, cardboard {Stock.Describe()}");
        if (newDay && CatNetwork.IsAuthority)
        {
            Refill("a new day");
        }
        else
        {
            ApplyAll();
        }
    }

    private void WatchTables()
    {
        foreach (var table in _tables)
        {
            var index = table.GameIndex;
            if (table.AppliedIndex < 0 || index == table.AppliedIndex)
            {
                continue;
            }

            if (index > table.AppliedIndex)
            {
                OnSheetsUsed(table, index - table.AppliedIndex);
            }
            else
            {
                _log.Info($"The game set the repair table {table.Name} to index {index} (the mod had {table.AppliedIndex}), cardboard {Stock.Describe()}");
            }

            ApplyAll();
        }
    }

    private void OnSheetsUsed(WorkstationView table, int sheets)
    {
        if (!CatNetwork.IsAuthority)
        {
            _log.Info($"A repair on {table.Name} used {sheets} sheet(s), waiting for the host's stock");
            return;
        }

        var used = Stock.Consume(sheets);
        _log.Info($"A repair on {table.Name} used {sheets} sheet(s), cardboard {Stock.Describe()}");
        if (used > 0)
        {
            Sync.Broadcast(Stock.Current);
        }

        ShowLeft();
    }

    private void OnStockReceived(int current)
    {
        if (CatNetwork.IsAuthority)
        {
            return;
        }

        var before = Stock.Current;
        var known = _hostStockKnown;
        _hostStockKnown = true;
        Stock.SetCurrent(current);
        _log.Info($"Cardboard from the host: {Stock.Describe()}");
        ApplyAll();
        if (!known)
        {
            return;
        }

        if (Stock.Current < before)
        {
            ShowLeft();
        }
        else if (Stock.Current > before)
        {
            ShowRefilled();
        }
    }

    private void Refill(string reason)
    {
        ReadSettings();
        var added = Settings == null ? Stock.Refill(RefillMode.Full, 0) : Stock.Refill(Settings.Refill.Value, Settings.RefillAmount.Value);
        _log.Info($"Cardboard refilled by {reason}: +{added}, now {Stock.Describe()}");
        Sync.Broadcast(Stock.Current);
        ApplyAll();
        if (added > 0)
        {
            ShowRefilled();
        }
    }

    private void ApplyAll()
    {
        foreach (var table in _tables)
        {
            if (table.IsAlive)
            {
                Apply(table);
            }
        }
    }

    private void Apply(WorkstationView table)
    {
        if (!CatNetwork.IsAuthority && !_hostStockKnown)
        {
            return;
        }

        table.Apply(Stock.GameIndex(table.Capacity));
    }

    private void ShowLeft()
    {
        if (Stock.Unlimited || Settings == null || !Settings.Messages.Value)
        {
            return;
        }

        Notifications.Show(Stock.IsEmpty ? _texts.Get("message.empty") : _texts.Format("message.left", Stock.Current, Stock.Maximum));
    }

    private void ShowRefilled()
    {
        if (Stock.Unlimited || Settings == null || !Settings.Messages.Value)
        {
            return;
        }

        Notifications.Show(_texts.Format("message.refilled", Stock.Current, Stock.Maximum));
    }

    private static string Describe(RefillMode mode, int amount) => mode == RefillMode.Full ? "to full" : $"by {amount}";

    private static string Period(int period) => Enum.IsDefined(typeof(GameTimePeriod), period) ? ((GameTimePeriod)period).ToString() : period.ToString();
}
