using System;
using BetterRepair.Logic;
using CatLib.Logging;
using CatLib.Saves;

namespace BetterRepair.Data;

public sealed class StockStore
{
    public const string StockKey = "cardboard";

    private readonly ModSave _save;
    private readonly Func<int> _current;
    private readonly CatLogger _log;

    public StockStore(ModSave save, Func<int> current, CatLogger log)
    {
        _save = save;
        _current = current;
        _log = log;
        _save.Loaded += OnLoaded;
        _save.Saving += Store;
    }

    public event Action<int?> Loaded;

    public void Store()
    {
        if (!_save.IsReady)
        {
            return;
        }

        var current = _current();
        if (!_save.Has(StockKey) || _save.Get(StockKey, -1) != current)
        {
            _save.Set(StockKey, current);
        }
    }

    private void OnLoaded()
    {
        int? stored = null;
        if (_save.Has(StockKey))
        {
            var value = _save.Get(StockKey, -1);
            if (value >= 0 && value <= CardboardStock.MaximumStock)
            {
                stored = value;
            }
            else
            {
                _log.Warning($"The stored cardboard stock {value} is out of range and is ignored");
            }
        }

        _log.Info($"Cardboard stock of {_save.SaveName}: {(stored.HasValue ? StockText.Format(stored.Value) : "not stored, the table starts full")} ({_save.State}, {_save.LastRead})");
        Loaded?.Invoke(stored);
    }
}
