using System;
using System.Linq;
using System.Reflection;
using CatLib.Logging;

namespace CatLib.Patching;

public sealed class CodePatch
{
    public const int DefaultSearchLength = 0x800;

    private readonly CatLogger _log;
    private IntPtr _site;
    private byte[] _original;
    private byte[] _written;

    private CodePatch(string ownerId, string name, CatLogger log)
    {
        OwnerId = ownerId;
        Name = name;
        _log = log;
    }

    public string OwnerId { get; }

    public string Name { get; }

    public bool IsFound => _site != IntPtr.Zero;

    public string Problem { get; private set; }

    public bool IsChanged => _written != null;

    public int Length => _original?.Length ?? 0;

    public byte[] Original => _original == null ? Array.Empty<byte>() : (byte[])_original.Clone();

    public static CodePatch Locate(string ownerId, string name, CatLogger log, MethodBase method, string pattern, int offset, int length,
        int searchLength = DefaultSearchLength)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("A code patch needs the id of its mod", nameof(ownerId));
        }

        var patch = new CodePatch(ownerId, string.IsNullOrWhiteSpace(name) ? "code" : name, log ?? throw new ArgumentNullException(nameof(log)));
        try
        {
            var parsed = BytePattern.Parse(pattern);
            if (!NativeCode.TryGetMethodPointer(method, out var start, out var problem))
            {
                patch.Fail(problem);
                return patch;
            }

            var code = NativeCode.Read(start, Math.Max(searchLength, parsed.Length));
            if (!TryFindSite(code, parsed, offset, length, out var site, out problem))
            {
                patch.Fail($"{method.DeclaringType?.Name}.{method.Name}: {problem}");
                return patch;
            }

            patch._site = start + site;
            patch._original = code.Skip(site).Take(length).ToArray();
            log.Info($"[CodePatch] {ownerId}: {patch.Name} found in {method.DeclaringType?.Name}.{method.Name} at +0x{site:X}: {Hex(patch._original)}");
        }
        catch (Exception exception)
        {
            patch.Fail($"{exception.GetType().Name}: {exception.Message}");
        }

        return patch;
    }

    public static bool TryFindSite(byte[] code, BytePattern pattern, int offset, int length, out int site, out string problem)
    {
        site = -1;
        problem = null;
        if (pattern == null || length <= 0 || offset < 0 || offset + length > pattern.Length)
        {
            problem = "the bytes to change must lie inside the pattern";
            return false;
        }

        var found = pattern.FindAll(code);
        if (found.Count == 0)
        {
            problem = $"the code \"{pattern}\" was not found, the game version may differ from the one the mod was made for";
            return false;
        }

        if (found.Count > 1)
        {
            problem = $"the code \"{pattern}\" was found {found.Count} times, the place to change is not certain";
            return false;
        }

        site = found[0] + offset;
        return true;
    }

    public bool Write(byte[] bytes)
    {
        if (!IsFound)
        {
            return false;
        }

        if (bytes == null || bytes.Length != _original.Length)
        {
            throw new ArgumentException($"{Name} changes exactly {_original.Length} byte(s)", nameof(bytes));
        }

        if (_written != null && _written.SequenceEqual(bytes))
        {
            return true;
        }

        return Replace(bytes, bytes.SequenceEqual(_original) ? null : (byte[])bytes.Clone());
    }

    public bool Restore() => !IsFound || _written == null || Replace(_original, null);

    private bool Replace(byte[] bytes, byte[] written)
    {
        try
        {
            var current = NativeCode.Read(_site, _original.Length);
            var expected = _written ?? _original;
            if (!current.SequenceEqual(expected))
            {
                Fail($"{Name} holds {Hex(current)} instead of {Hex(expected)}, something else changed this code; it is left alone");
                _site = IntPtr.Zero;
                return false;
            }

            NativeCode.Write(_site, bytes);
            _written = written;
            _log.Info($"[CodePatch] {OwnerId}: {Name} is now {Hex(bytes)}{(written == null ? " (as in the game)" : string.Empty)}");
            return true;
        }
        catch (Exception exception)
        {
            Fail($"changing {Name} failed, it is not tried again: {exception.Message}");
            _site = IntPtr.Zero;
            return false;
        }
    }

    private void Fail(string problem)
    {
        Problem = problem;
        _log.Error($"[CodePatch] {OwnerId}: {problem}");
    }

    private static string Hex(byte[] bytes) => string.Join(" ", bytes.Select(value => value.ToString("X2")));
}
