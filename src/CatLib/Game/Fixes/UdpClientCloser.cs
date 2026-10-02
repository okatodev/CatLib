using System;
using System.Collections.Generic;
using CatLib.Core;
using CatLib.Logging;

namespace CatLib.Game.Fixes;

internal static class UdpClientCloser
{
    public const int CheckEveryFrames = 30;

    private static readonly Dictionary<IntPtr, Client> Clients = new();
    private static CatLogger _log;
    private static bool _initialized;

    public static int Tracked => Clients.Count;

    internal static void Initialize(CatLogger log)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _log = log;
        FrameLoop.Update += OnUpdate;
    }

    internal static void CloseAll(string reason)
    {
        foreach (var client in new List<Client>(Clients.Values))
        {
            Close(client, reason);
        }

        Clients.Clear();
    }

    private static void OnUpdate()
    {
        if (FrameLoop.FrameCount % CheckEveryFrames != 0)
        {
            return;
        }

        var current = Current();
        var currentPointer = current?.Pointer ?? IntPtr.Zero;
        if (current != null && !Clients.ContainsKey(currentPointer))
        {
            Clients[currentPointer] = current;
        }

        if (Clients.Count <= (current == null ? 0 : 1))
        {
            return;
        }

        foreach (var pair in new List<KeyValuePair<IntPtr, Client>>(Clients))
        {
            if (pair.Key == currentPointer)
            {
                continue;
            }

            Close(pair.Value, "the game replaced it");
            Clients.Remove(pair.Key);
        }
    }

    private static Client Current()
    {
        try
        {
            return Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance._client : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Close(Client client, string reason)
    {
        try
        {
            if (client == null || client.WasCollected)
            {
                return;
            }

            var udp = client._udpClient;
            if (udp == null)
            {
                return;
            }

            client._cancelToken?.Cancel();
            udp.Close();
            _log?.Info($"Closed the UDP socket of a game network client ({reason}), so its listening thread can end and the game can quit");
        }
        catch (Exception exception)
        {
            _log?.Warning($"Closing the UDP socket of a game network client failed: {exception.Message}");
        }
    }
}
