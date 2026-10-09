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
    public const double RequiredAgreement = 0.8;
    public const string InteropPlaceholderPrefix = "Method_";

    private readonly object _gate = new object();
    private readonly Dictionary<string, PeImage> _images = new Dictionary<string, PeImage>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PdbPublics> _pdbs = new Dictionary<string, PdbPublics>(StringComparer.OrdinalIgnoreCase);
    private readonly string _interopDirectory;
    private readonly SymbolStore _symbols;
    private readonly WatcherLog _log;
    private GameMethodMap _map;
    private bool _mapTried;
    private Il2CppCodeNames _il2cpp;
    private bool _il2cppTried;

    public CodeNames(string interopDirectory, WatcherLog log, SymbolStore symbols = null)
    {
        _interopDirectory = interopDirectory;
        _symbols = symbols;
        _log = log;
    }

    public Func<string, string> Undecorate { get; set; } = Readable;

    public bool HasGameMethods => _map != null || _il2cpp != null;

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

    public void WaitForSymbols(TimeSpan timeout) => _symbols?.WaitIdle(timeout);

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
                return IsGameAssembly(modulePath)
                    ? NameInGameAssembly(image, (uint)offset, returnAddress, out gameMethod)
                    : ModuleName(image, (uint)offset, returnAddress);
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

    private string NameInGameAssembly(PeImage image, uint rva, bool returnAddress, out bool gameMethod)
    {
        gameMethod = true;
        var lookup = returnAddress && rva > 0 ? rva - 1 : rva;
        var inFunction = image.TryFindFunction(lookup, out var function);
        var start = inFunction ? image.PrimaryStart(function) : 0;
        var map = Map(image);
        var il2cpp = Il2Cpp(image);
        if (inFunction)
        {
            var shared = 0;
            var name = map != null ? map.NameAt(start, out shared) : null;
            if (name == null && il2cpp != null)
            {
                name = il2cpp.GenericNameAt(start, out shared);
            }

            if (name == null && map == null && il2cpp != null)
            {
                name = il2cpp.MethodNameAt(start);
            }

            if (name != null)
            {
                var interop = InteropAlias(ref name, il2cpp, start);
                return Format(name, rva - start, shared) + interop;
            }
        }
        else
        {
            if (map != null && map.TryFloor(lookup, out var leaf) && IsLeaf(image, leaf, lookup))
            {
                var name = map.NameAt(leaf, out var shared);
                if (name != null)
                {
                    var interop = InteropAlias(ref name, il2cpp, leaf);
                    return Format(name, rva - leaf, shared) + interop;
                }
            }

            if (il2cpp != null && il2cpp.TryFloorGeneric(lookup, out leaf) && IsLeaf(image, leaf, lookup))
            {
                var name = il2cpp.GenericNameAt(leaf, out var shared);
                if (name != null)
                {
                    return Format(name, rva - leaf, shared);
                }
            }

            if (map == null && il2cpp != null && il2cpp.TryFloorMethod(lookup, out leaf) && IsLeaf(image, leaf, lookup))
            {
                var name = il2cpp.MethodNameAt(leaf);
                if (name != null)
                {
                    return Format(name, rva - leaf, 0);
                }
            }
        }

        gameMethod = false;
        var export = ExportName(image, rva, lookup, inFunction, start);
        if (export != null)
        {
            return export;
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

    private string ModuleName(PeImage image, uint rva, bool returnAddress)
    {
        var lookup = returnAddress && rva > 0 ? rva - 1 : rva;
        var inFunction = image.TryFindFunction(lookup, out var function);
        var start = inFunction ? image.PrimaryStart(function) : 0;
        var pdb = Pdb(image);
        if (pdb != null)
        {
            if (inFunction)
            {
                var name = pdb.NameAt(start);
                if (name != null)
                {
                    return Undecorate(name) + " + " + Offset(rva - start);
                }
            }
            else if (pdb.TryFloor(lookup, out var leaf, out var name) && IsLeaf(image, leaf, lookup))
            {
                return Undecorate(name) + " + " + Offset(rva - leaf);
            }
        }

        return ExportName(image, rva, lookup, inFunction, start);
    }

    private static bool IsLeaf(PeImage image, uint start, uint lookup) => lookup - start < MaxLeafSize && !image.HasFunctionStartBetween(start, lookup);

    private string ExportName(PeImage image, uint rva, uint lookup, bool inFunction, uint start)
    {
        if (!image.TryFindExport(lookup, out var export, out var exportStart))
        {
            return null;
        }

        if (inFunction && exportStart == start)
        {
            return Undecorate(export) + " + " + Offset(rva - start);
        }

        if (!inFunction && IsLeaf(image, exportStart, lookup))
        {
            return Undecorate(export) + " + " + Offset(rva - exportStart);
        }

        return null;
    }

    private static string Format(string name, uint offset, int shared) =>
        name + " + " + Offset(offset) + (shared <= 0 ? string.Empty : shared == 1 ? ", 1 more method has the same code" : $", {shared} more methods have the same code");

    private PdbPublics Pdb(PeImage image)
    {
        if (_pdbs.TryGetValue(image.Path, out var cached))
        {
            return cached;
        }

        PdbPublics publics = null;
        if (image.TryGetCodeView(out var pdbName, out var key))
        {
            foreach (var candidate in PdbCandidates(image.Path, pdbName, key))
            {
                publics = PdbPublics.Read(candidate, image, key, out var problem);
                if (publics != null)
                {
                    _log?.Write($"Names of {Path.GetFileName(image.Path)} come from {candidate}, {publics.Count} public symbols");
                    break;
                }

                _log?.Write($"Symbols {candidate} are not used: {problem}");
            }
        }

        _pdbs[image.Path] = publics;
        return publics;
    }

    private IEnumerable<string> PdbCandidates(string modulePath, string pdbName, string key)
    {
        var folder = Path.GetDirectoryName(modulePath) ?? string.Empty;
        var beside = Path.Combine(folder, pdbName);
        if (File.Exists(beside))
        {
            yield return beside;
        }

        var named = Path.ChangeExtension(modulePath, ".pdb");
        if (!string.Equals(named, beside, StringComparison.OrdinalIgnoreCase) && File.Exists(named))
        {
            yield return named;
        }

        var cached = _symbols?.CachedPath(pdbName, key);
        if (cached != null)
        {
            yield return cached;
        }
    }

    private Il2CppCodeNames Il2Cpp(PeImage gameAssembly)
    {
        if (_il2cppTried)
        {
            return _il2cpp;
        }

        _il2cppTried = true;
        var metadataPath = Il2CppCodeNames.FindMetadata(gameAssembly.Path);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var names = Il2CppCodeNames.Open(gameAssembly, metadataPath, out var problem);
        if (names == null)
        {
            _log?.Write($"Generic method names are not available: {problem ?? "global-metadata.dat was not found"}");
            return null;
        }

        var map = Map(gameAssembly);
        if (map != null)
        {
            var compared = 0;
            var agreed = 0;
            var step = Math.Max(1, map.Count / CheckedSamples);
            for (var index = 0; index < map.Count; index += step)
            {
                var rva = map.Sample(index);
                var known = map.NameAt(rva, out var shared);
                var read = names.MethodNameAt(rva);
                if (known == null || read == null || shared > 0)
                {
                    continue;
                }

                compared++;
                agreed += SameMethod(known, read) ? 1 : 0;
            }

            if (compared == 0 || agreed < compared * RequiredAgreement)
            {
                _log?.Write($"Generic method names are not used: the IL2CPP metadata agrees with the BepInEx method map for {agreed} of {compared} methods");
                return null;
            }

            _log?.Write($"IL2CPP metadata version {names.Version} read in {clock.ElapsedMilliseconds} ms: {names.GenericMethodCount} generic method addresses, " +
                        $"{names.MethodCount} methods, {agreed} of {compared} checked methods agree with the BepInEx method map");
        }
        else
        {
            _log?.Write($"IL2CPP metadata version {names.Version} read in {clock.ElapsedMilliseconds} ms: {names.GenericMethodCount} generic method addresses, {names.MethodCount} methods");
        }

        _il2cpp = names;
        return names;
    }

    public static bool SameMethod(string known, string read)
    {
        var left = Signature(known);
        var right = Signature(read);
        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        if (string.Equals(Letters(left), Letters(right), StringComparison.Ordinal))
        {
            return true;
        }

        return IsInteropPlaceholder(known) && string.Equals(Letters(DeclaringType(left)), Letters(DeclaringType(right)), StringComparison.Ordinal);
    }

    public static bool IsInteropPlaceholder(string name)
    {
        var signature = Signature(name);
        var method = signature.Substring(signature.LastIndexOf('.') + 1);
        var last = method.LastIndexOf('_');
        if (!method.StartsWith(InteropPlaceholderPrefix, StringComparison.Ordinal) || last < 0 || last == method.Length - 1)
        {
            return false;
        }

        for (var index = last + 1; index < method.Length; index++)
        {
            if (!char.IsDigit(method[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string InteropAlias(ref string name, Il2CppCodeNames il2cpp, uint start)
    {
        if (il2cpp == null || !IsInteropPlaceholder(name))
        {
            return string.Empty;
        }

        var original = il2cpp.MethodNameAt(start);
        if (original == null || !SameMethod(name, original))
        {
            return string.Empty;
        }

        var signature = Signature(name);
        var alias = signature.Substring(signature.LastIndexOf('.') + 1);
        name = original;
        return ", " + alias + " in the interop assemblies";
    }

    private static string Signature(string name)
    {
        var open = name?.IndexOf('(') ?? -1;
        return open > 0 ? name.Substring(0, open) : name ?? string.Empty;
    }

    private static string DeclaringType(string signature)
    {
        var dot = signature.LastIndexOf('.');
        return dot > 0 ? signature.Substring(0, dot) : string.Empty;
    }

    private static string Letters(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

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
