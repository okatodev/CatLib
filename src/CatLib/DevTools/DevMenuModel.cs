using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Logging;

namespace CatLib.DevTools;

public sealed class DevMenuModel
{
    public const int DigitItems = 9;

    private readonly object _sync = new();
    private readonly List<DevItem> _items = new();
    public DevMenuModel(CatLogger log = null)
    {
        Log = log;
    }

    public CatLogger Log { get; set; }

    public int GroupIndex { get; private set; }

    public int Selected { get; private set; }

    public string Status { get; private set; }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _items.Count;
            }
        }
    }

    public IReadOnlyList<string> Groups
    {
        get
        {
            lock (_sync)
            {
                return _items.Select(item => item.Group).Distinct(StringComparer.Ordinal).ToList();
            }
        }
    }

    public string CurrentGroup
    {
        get
        {
            var groups = Groups;
            return groups.Count == 0 ? null : groups[Math.Clamp(GroupIndex, 0, groups.Count - 1)];
        }
    }

    public IReadOnlyList<DevItem> Current
    {
        get
        {
            var group = CurrentGroup;
            lock (_sync)
            {
                return _items.Where(item => item.Group == group).ToList();
            }
        }
    }

    public DevItem SelectedItem
    {
        get
        {
            var items = Current;
            return items.Count == 0 ? null : items[Math.Clamp(Selected, 0, items.Count - 1)];
        }
    }

    public DevItem Add(DevItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        lock (_sync)
        {
            _items.Add(item);
        }

        return item;
    }

    public bool Remove(DevItem item)
    {
        bool removed;
        lock (_sync)
        {
            removed = _items.Remove(item);
        }

        Clamp();
        return removed;
    }

    public void Press(DevKey key)
    {
        var items = Current;
        var groups = Groups;
        switch (key)
        {
            case DevKey.Up when items.Count > 0:
                Selected = (Selected - 1 + items.Count) % items.Count;
                break;
            case DevKey.Down when items.Count > 0:
                Selected = (Selected + 1) % items.Count;
                break;
            case DevKey.Left when groups.Count > 0:
                GroupIndex = (GroupIndex - 1 + groups.Count) % groups.Count;
                Selected = 0;
                break;
            case DevKey.Right when groups.Count > 0:
                GroupIndex = (GroupIndex + 1) % groups.Count;
                Selected = 0;
                break;
            case DevKey.Enter:
                Run(SelectedItem);
                break;
            case >= DevKey.Digit1 and <= DevKey.Digit9:
                var index = key - DevKey.Digit1;
                if (index < items.Count)
                {
                    Selected = index;
                    Run(items[index]);
                }

                break;
        }
    }

    public string Run(DevItem item)
    {
        if (item == null)
        {
            return null;
        }

        try
        {
            var result = item.Run();
            Status = item.Label + ": " + (string.IsNullOrWhiteSpace(result) ? "done" : result);
            Log?.Info("Developer menu: " + Status);
        }
        catch (Exception exception)
        {
            Status = item.Label + ": failed, " + exception.GetType().Name + ": " + exception.Message;
            Log?.Error("Developer menu command " + item.Label + " failed", exception);
        }

        return Status;
    }

    private void Clamp()
    {
        var groups = Groups;
        GroupIndex = groups.Count == 0 ? 0 : Math.Clamp(GroupIndex, 0, groups.Count - 1);
        var items = Current;
        Selected = items.Count == 0 ? 0 : Math.Clamp(Selected, 0, items.Count - 1);
    }
}
