using System;
using System.Collections.Generic;
using System.IO;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher.Stacks;

internal sealed class ModuleSet
{
    private readonly List<CrashModule> _modules;

    public ModuleSet(IEnumerable<CrashModule> modules)
    {
        _modules = new List<CrashModule>(modules ?? new List<CrashModule>());
        _modules.Sort((left, right) => left.Start.CompareTo(right.Start));
    }

    public int Count => _modules.Count;

    public CrashModule Find(ulong address)
    {
        var low = 0;
        var high = _modules.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var module = _modules[middle];
            if (address < module.Start)
            {
                high = middle - 1;
            }
            else if (address - module.Start >= module.Size)
            {
                low = middle + 1;
            }
            else
            {
                return module;
            }
        }

        return null;
    }

    public CrashModule Named(string fileName)
    {
        foreach (var module in _modules)
        {
            if (string.Equals(Path.GetFileName(module.Path), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return module;
            }
        }

        return null;
    }
}
