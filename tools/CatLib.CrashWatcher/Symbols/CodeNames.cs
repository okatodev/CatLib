using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class CodeNames : IDisposable
{
    public const string GameAssemblyName = "GameAssembly.dll";
    public const string RuntimeName = "IL2CPP runtime";
    public const string GeneratedName = "IL2CPP code that is not a game method";
    public const uint MaxLeafSize = 0x400;
    public const int CheckedSamples = 256;
    public const double RequiredMatch = 0.95;
    public const double RequiredStarts = 0.5;

    private readonly object _gate = new object();
    private readonly Dictionary<string, PeImage> _images = new Dictionary<string, PeImage>(StringComparer.OrdinalIgnoreCase);
    private readonly string _interopDirectory;
    private readonly WatcherLog _log;
    private GameMethodMap _map;
    private bool _mapTried;

    public CodeNames(string interopDirectory, WatcherLog log)
    {
        _interopDirectory = interopDirectory;
        _log = log;
    }

    public bool HasGameMethods => _map != null;

    public static string Offset(ulong value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);

    public PeImage Image(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        lock (_gate)
        {
            if (!_images.TryGetValue(path, out var image))
            {
                try
                {
                    image = PeImage.Open(path);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    _log?.Write($"Could not read {path} for method names: {exception.Message}");
                    image = null;
                }

                _images[path] = image;
            }

            return image;
        }
    }

    public string Name(string modulePath, ulong offset, bool returnAddress = false) => Name(modulePath, offset, returnAddress, out _);

    public string Name(string modulePath, ulong offset, bool returnAddress, out bool gameMethod)
    {
        gameMethod = false;
        if (offset > uint.MaxValue)
        {
            return null;
        }

        var image = Image(modulePath);
        if (image == null)
        {
            return null;
        }

        try
        {
            lock (_gate)
            {
                return Name(image, (uint)offset, IsGameAssembly(modulePath), returnAddress, out gameMethod);
            }
        }
        catch (Exception exception)
        {
            _log?.Write($"Could not name {Path.GetFileName(modulePath)} + {Offset(offset)}: {exception.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var image in _images.Values)
            {
                image?.Dispose();
            }

            _images.Clear();
        }
    }

    public static string Readable(string export)
    {
        if (string.IsNullOrEmpty(export) || export[0] != '?' || export.Length < 2 || export[1] == '?' || export[1] == '$')
        {
            return export;
        }

        var end = export.IndexOf("@@", StringComparison.Ordinal);
        if (end < 0)
        {
            return export;
        }

        var parts = export.Substring(1, end - 1).Split('@');
        Array.Reverse(parts);
        return string.Join("::", parts);
    }

    public static bool IsGameAssembly(string modulePath) =>
        string.Equals(Path.GetFileName(modulePath ?? string.Empty), GameAssemblyName, StringComparison.OrdinalIgnoreCase);

    private string Name(PeImage image, uint rva, bool gameAssembly, bool returnAddress, out bool gameMethod)
    {
        gameMethod = false;
        var lookup = returnAddress && rva > 0 ? rva - 1 : rva;
        var inFunction = image.TryFindFunction(lookup, out var function);
        var start = inFunction ? image.PrimaryStart(function) : 0;
        var map = gameAssembly ? Map(image) : null;
        if (map != null)
        {
            if (inFunction)
            {
                var name = map.NameAt(start, out var shared);
                if (name != null)
                {
                    gameMethod = true;
                    return Format(name, rva - start, shared);
                }
            }
            else if (map.TryFloor(lookup, out var leaf) && lookup - leaf < MaxLeafSize && !image.HasFunctionStartBetween(leaf, lookup))
            {
                var name = map.NameAt(leaf, out var shared);
                if (name != null)
                {
                    gameMethod = true;
                    return Format(name, rva - leaf, shared);
                }
            }
        }

        if (image.TryFindExport(lookup, out var export, out var exportStart))
        {
            if (inFunction && exportStart == start)
            {
                return Readable(export) + " + " + Offset(rva - start);
            }

            if (!inFunction && lookup - exportStart < MaxLeafSize && !image.HasFunctionStartBetween(exportStart, lookup))
            {
                return Readable(export) + " + " + Offset(rva - exportStart);
            }
        }

        if (!gameAssembly)
        {
            return null;
        }

        var where = inFunction ? $", in the function at {Offset(start)}" : string.Empty;
        switch (image.SectionName(lookup))
        {
            case ".text":
                return RuntimeName + where;
            case "il2cpp":
                return GeneratedName + ", such as a generic method" + where;
            default:
                return null;
        }
    }

    private static string Format(string name, uint offset, int shared) =>
        name + " + " + Offset(offset) + (shared <= 0 ? string.Empty : shared == 1 ? ", 1 more method has the same code" : $", {shared} more methods have the same code");

    private GameMethodMap Map(PeImage gameAssembly)
    {
        if (_mapTried)
        {
            return _map;
        }

        _mapTried = true;
        var map = GameMethodMap.Open(_interopDirectory, out var problem);
        if (map == null)
        {
            _log?.Write($"Game method names are not available: {problem}");
            return null;
        }

        var checkedCount = 0;
        var starts = 0;
        var leaves = 0;
        var step = Math.Max(1, map.Count / CheckedSamples);
        for (var index = 0; index < map.Count; index += step)
        {
            var rva = map.Sample(index);
            checkedCount++;
            if (gameAssembly.TryFindFunction(rva, out var function))
            {
                starts += function.Begin == rva ? 1 : 0;
            }
            else if (gameAssembly.IsExecutable(rva))
            {
                leaves++;
            }
        }

        var matching = starts + leaves;
        if (checkedCount == 0 || matching < checkedCount * RequiredMatch || starts < checkedCount * RequiredStarts)
        {
            _log?.Write($"Game method names are not used: only {matching} of {checkedCount} checked addresses of the BepInEx method map start a function in {gameAssembly.Path}, the map is for another game build");
            return null;
        }

        _log?.Write($"Game method names come from {Path.Combine(_interopDirectory, GameMethodMap.FileName)}, {map.Count} methods");
        _map = map;
        return map;
    }
}
