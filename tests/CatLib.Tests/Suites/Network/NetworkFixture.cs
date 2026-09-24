using System;
using System.Collections.Generic;
using CatLib.Net;

namespace CatLib.Tests.Suites.Network;

internal sealed class NetworkFixture
{
    public const ulong HostId = 76561190000000001UL;
    public const ulong ClientId = 76561190000000002UL;
    public const string GameVersion = "CMC 1.01.00.1763.9722.17497";
    public const double HandshakeTimeout = 10;

    public NetworkFixture(LocalIdentity host, LocalIdentity client, Func<IReadOnlyList<SessionSettingValue>> hostSettings = null, ISessionSettingsSink clientSink = null)
    {
        Network = new LoopbackNetwork();
        Sink = clientSink ?? new RecordingSink();
        HostTransport = Network.Join(HostId, (from, data) => Host.OnReceived(from, data));
        ClientTransport = Network.Join(ClientId, (from, data) => Client.OnReceived(from, data));
        Host = new HostSession(HostTransport, host, hostSettings ?? (() => Array.Empty<SessionSettingValue>()), () => Network.Now, HandshakeTimeout, null);
        Client = client == null ? null : new ClientSession(ClientTransport, client, HostId, Sink, () => Network.Now, HandshakeTimeout, null);
    }

    public LoopbackNetwork Network { get; }

    public LoopbackTransport HostTransport { get; }

    public LoopbackTransport ClientTransport { get; }

    public HostSession Host { get; }

    public ClientSession Client { get; }

    public ISessionSettingsSink Sink { get; }

    public static ModInfo Mod(string id, string version, SessionPolicy policy, VersionRule rule = VersionRule.SameMinor) => new(id, id, version, policy, rule);

    public static LocalIdentity Identity(params ModInfo[] mods) => new("0.4.0", GameVersion, mods);

    public void Connect()
    {
        Host.OnPeerConnected(ClientId);
        Client?.Start();
        Network.Pump();
    }
}

internal sealed class RecordingSink : ISessionSettingsSink
{
    public List<SessionSettingValue> Applied { get; } = new();

    public int Clears { get; private set; }

    public SessionApplyResult Apply(IReadOnlyList<SessionSettingValue> values)
    {
        Applied.AddRange(values);
        return new SessionApplyResult(values.Count, new string[0], new string[0], new string[0]);
    }

    public int Clear()
    {
        Clears++;
        return 0;
    }
}
