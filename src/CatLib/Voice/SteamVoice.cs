using System;
using System.Runtime.InteropServices;
using CatLib.Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Steamworks;

namespace CatLib.Voice;

internal static class VoiceResults
{
    public const int Ok = 0;
    public const int NotInitialized = 1;
    public const int NotRecording = 2;
    public const int NoData = 3;
    public const int BufferTooSmall = 4;
    public const int DataCorrupted = 5;
    public const int Restricted = 6;

    public static string Name(int result) => result switch
    {
        Ok => "ok",
        NotInitialized => "Steam voice is not initialized",
        NotRecording => "not recording",
        NoData => "no data",
        BufferTooSmall => "buffer too small",
        DataCorrupted => "data corrupted",
        Restricted => "voice is restricted for this Steam account",
        7 => "unsupported codec",
        8 => "the receiver is out of date",
        9 => "the receiver did not answer",
        _ => "result " + result
    };
}

internal interface ISteamVoice
{
    string Name { get; }

    void Start();

    void Stop();

    int Available(out uint bytes);

    int Read(byte[] buffer, out uint written);

    int Decompress(byte[] data, int count, byte[] destination, out uint written, uint sampleRate);

    uint OptimalSampleRate { get; }
}

internal sealed unsafe class FlatSteamVoice : ISteamVoice
{
    public const string LibraryName = "steam_api64";

    private static readonly string[] AccessorNames =
    {
        "SteamAPI_SteamUser_v023",
        "SteamAPI_SteamUser_v022",
        "SteamAPI_SteamUser_v021"
    };

    private readonly IntPtr _self;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _start;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _stop;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, uint*, uint*, uint, int> _available;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, byte, byte*, uint, uint*, byte, byte*, uint, uint*, uint, int> _read;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*, uint, byte*, uint, uint*, uint, int> _decompress;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, uint> _rate;

    private FlatSteamVoice(IntPtr self, IntPtr start, IntPtr stop, IntPtr available, IntPtr read, IntPtr decompress, IntPtr rate, string accessor)
    {
        _self = self;
        _start = (delegate* unmanaged[Cdecl]<IntPtr, void>)start;
        _stop = (delegate* unmanaged[Cdecl]<IntPtr, void>)stop;
        _available = (delegate* unmanaged[Cdecl]<IntPtr, uint*, uint*, uint, int>)available;
        _read = (delegate* unmanaged[Cdecl]<IntPtr, byte, byte*, uint, uint*, byte, byte*, uint, uint*, uint, int>)read;
        _decompress = (delegate* unmanaged[Cdecl]<IntPtr, byte*, uint, byte*, uint, uint*, uint, int>)decompress;
        _rate = (delegate* unmanaged[Cdecl]<IntPtr, uint>)rate;
        Name = "Flat through " + accessor;
    }

    public string Name { get; }

    public static FlatSteamVoice TryCreate(out string error)
    {
        if (!NativeLibrary.TryLoad(LibraryName, out var library))
        {
            error = $"{LibraryName} is not loaded";
            return null;
        }

        string accessorName = null;
        var accessor = IntPtr.Zero;
        foreach (var name in AccessorNames)
        {
            if (NativeLibrary.TryGetExport(library, name, out accessor))
            {
                accessorName = name;
                break;
            }
        }

        if (accessorName == null)
        {
            error = "No SteamUser accessor was found in " + LibraryName;
            return null;
        }

        if (!NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_StartVoiceRecording", out var start) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_StopVoiceRecording", out var stop) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_GetAvailableVoice", out var available) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_GetVoice", out var read) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_DecompressVoice", out var decompress) ||
            !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUser_GetVoiceOptimalSampleRate", out var rate))
        {
            error = "A SteamUser voice function is missing in " + LibraryName;
            return null;
        }

        var self = ((delegate* unmanaged[Cdecl]<IntPtr>)accessor)();
        if (self == IntPtr.Zero)
        {
            error = accessorName + " returned no interface, Steam may not be initialized";
            return null;
        }

        error = null;
        return new FlatSteamVoice(self, start, stop, available, read, decompress, rate, accessorName);
    }

    public void Start() => _start(_self);

    public void Stop() => _stop(_self);

    public int Available(out uint bytes)
    {
        uint compressed = 0;
        var result = _available(_self, &compressed, null, 0);
        bytes = compressed;
        return result;
    }

    public int Read(byte[] buffer, out uint written)
    {
        uint count = 0;
        int result;
        fixed (byte* data = buffer)
        {
            result = _read(_self, 1, data, (uint)buffer.Length, &count, 0, null, 0, null, 0);
        }

        written = count;
        return result;
    }

    public int Decompress(byte[] data, int count, byte[] destination, out uint written, uint sampleRate)
    {
        uint bytes = 0;
        int result;
        fixed (byte* input = data)
        fixed (byte* output = destination)
        {
            result = _decompress(_self, input, (uint)count, output, (uint)destination.Length, &bytes, sampleRate);
        }

        written = bytes;
        return result;
    }

    public uint OptimalSampleRate => _rate(_self);
}

internal sealed unsafe class InteropSteamVoice : ISteamVoice
{
    private Il2CppStructArray<byte> _readBuffer;
    private Il2CppStructArray<byte> _input;
    private Il2CppStructArray<byte> _output;

    public string Name => "Interop";

    public void Start() => SteamUser.StartVoiceRecording();

    public void Stop() => SteamUser.StopVoiceRecording();

    public int Available(out uint bytes) => (int)SteamUser.GetAvailableVoice(out bytes);

    public int Read(byte[] buffer, out uint written)
    {
        _readBuffer = Fit(_readBuffer, buffer.Length);
        var result = (int)SteamUser.GetVoice(true, _readBuffer, (uint)buffer.Length, out written);
        if (result == VoiceResults.Ok)
        {
            CopyOut(_readBuffer, buffer, (int)Math.Min(written, (uint)buffer.Length));
        }

        return result;
    }

    public int Decompress(byte[] data, int count, byte[] destination, out uint written, uint sampleRate)
    {
        _input = Fit(_input, Math.Max(count, 1));
        CopyIn(data, _input, count);
        _output = Fit(_output, destination.Length);
        var result = (int)SteamUser.DecompressVoice(_input, (uint)count, _output, (uint)destination.Length, out written, sampleRate);
        if (result == VoiceResults.Ok)
        {
            CopyOut(_output, destination, (int)Math.Min(written, (uint)destination.Length));
        }

        return result;
    }

    public uint OptimalSampleRate => SteamUser.GetVoiceOptimalSampleRate();

    private static Il2CppStructArray<byte> Fit(Il2CppStructArray<byte> array, int length) =>
        array != null && array.Length >= length ? array : new Il2CppStructArray<byte>(length);

    private static void CopyOut(Il2CppStructArray<byte> source, byte[] destination, int count)
    {
        if (count > 0)
        {
            Marshal.Copy(source.Pointer + Il2CppArrays.DataOffset, destination, 0, count);
        }
    }

    private static void CopyIn(byte[] source, Il2CppStructArray<byte> destination, int count)
    {
        if (count > 0)
        {
            Marshal.Copy(source, 0, destination.Pointer + Il2CppArrays.DataOffset, count);
        }
    }
}
