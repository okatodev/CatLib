using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using CatLib.Events;
using CatLib.Logging;
using CatLib.Threading;

namespace CatLib.Config;

public sealed class Setting<T> : ISettingNode
{
    private readonly List<Applier> _appliers = new();
    private readonly CatLogger _log;
    private T _value;
    private bool _restartReported;
    private T _reportedPendingValue;
    private bool _hasOverride;
    private T _override;

    internal Setting(CatSettings owner, ConfigEntry<T> entry, SettingScope scope, CatLogger log)
    {
        Owner = owner;
        Entry = entry;
        Scope = scope;
        _log = log;
        _value = entry.Value;
        entry.SettingChanged += OnEntryChanged;
    }

    public event Action<T, T> Changed;

    public CatSettings Owner { get; }

    public ConfigEntry<T> Entry { get; }

    public ConfigEntryBase EntryBase => Entry;

    public string Section => Entry.Definition.Section;

    public string Key => Entry.Definition.Key;

    public string Id => Owner.OwnerId + "/" + Section + "/" + Key;

    public SettingScope Scope { get; }

    public bool IsRestartRequired { get; private set; }

    public string MenuLabel { get; private set; }

    public bool IsHiddenInMenu { get; private set; }

    public bool IsOverridden => _hasOverride;

    public object BoxedOverride => _hasOverride ? _override : null;

    public bool IsRestartPending => IsRestartRequired && !EqualityComparer<T>.Default.Equals(_value, Entry.Value);

    public T Value => _value;

    public T LocalValue
    {
        get => Entry.Value;
        set => Entry.Value = value;
    }

    public Type ValueType => typeof(T);

    public object BoxedValue => _value;

    public object BoxedLocalValue => Entry.Value;

    public Setting<T> RequiresRestart()
    {
        IsRestartRequired = true;
        return this;
    }

    public Setting<T> Label(string label)
    {
        MenuLabel = string.IsNullOrWhiteSpace(label) ? null : label;
        return this;
    }

    public Setting<T> HiddenInMenu()
    {
        IsHiddenInMenu = true;
        return this;
    }

    public IDisposable Apply(Action<T> apply)
    {
        if (apply == null)
        {
            throw new ArgumentNullException(nameof(apply));
        }

        var applier = new Applier(this, apply);
        lock (_appliers)
        {
            _appliers.Add(applier);
        }

        MainThread.RunOrPost(() =>
        {
            if (!applier.IsDisposed)
            {
                Invoke(applier, _value);
            }
        });

        return applier;
    }

    void ISettingNode.Refresh() => Refresh();

    void ISettingNode.Detach() => Entry.SettingChanged -= OnEntryChanged;

    bool ISettingNode.SetOverride(object value)
    {
        if (value is not T typed)
        {
            return false;
        }

        _hasOverride = true;
        _override = typed;
        Refresh();
        return true;
    }

    bool ISettingNode.ClearOverride()
    {
        if (!_hasOverride)
        {
            return false;
        }

        _hasOverride = false;
        _override = default;
        Refresh();
        return true;
    }

    private void OnEntryChanged(object sender, EventArgs args)
    {
        if (CatConfig.IsApplyingFile)
        {
            return;
        }

        MainThread.RunOrPost(Refresh);
    }

    private void Refresh()
    {
        var local = Entry.Value;
        var comparer = EqualityComparer<T>.Default;

        if (IsRestartRequired)
        {
            if (comparer.Equals(local, _value))
            {
                if (_restartReported)
                {
                    _restartReported = false;
                    _log.Info($"{Id} was restored to its active value, no restart needed");
                }

                return;
            }

            if (_restartReported && comparer.Equals(local, _reportedPendingValue))
            {
                return;
            }

            _restartReported = true;
            _reportedPendingValue = local;
            CatConfig.RaiseRestartRequired(this);
            return;
        }

        var desired = _hasOverride ? _override : local;
        if (comparer.Equals(_value, desired))
        {
            return;
        }

        var previous = _value;
        _value = desired;

        Applier[] snapshot;
        lock (_appliers)
        {
            snapshot = _appliers.ToArray();
        }

        foreach (var applier in snapshot)
        {
            if (!applier.IsDisposed)
            {
                Invoke(applier, desired);
            }
        }

        SafeInvoker.Invoke(Changed, previous, desired, Id + ".Changed", _log);
        CatConfig.RaiseEffectiveValueChanged(this);
    }

    private void Invoke(Applier applier, T value)
    {
        try
        {
            applier.Action(value);
        }
        catch (Exception exception)
        {
            _log.Error($"Applying {Id} failed", exception);
        }
    }

    private void Remove(Applier applier)
    {
        lock (_appliers)
        {
            _appliers.Remove(applier);
        }
    }

    private sealed class Applier : IDisposable
    {
        private readonly Setting<T> _owner;

        public Applier(Setting<T> owner, Action<T> action)
        {
            _owner = owner;
            Action = action;
        }

        public Action<T> Action { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _owner.Remove(this);
        }
    }
}
