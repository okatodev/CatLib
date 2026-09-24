using System;
using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class ManagerPresenceTest : TestCase
{
    public override string Suite => "Platform";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var probes = new (string Name, Func<bool> IsPresent)[]
        {
            (nameof(BootstrapManager), Singleton<BootstrapManager>.HasInstance),
            (nameof(GameManager), Singleton<GameManager>.HasInstance),
            (nameof(PlayerManager), Singleton<PlayerManager>.HasInstance),
            (nameof(NetworkManager), Singleton<NetworkManager>.HasInstance),
            (nameof(SettingsManager), Singleton<SettingsManager>.HasInstance),
            (nameof(SaveManager), Singleton<SaveManager>.HasInstance),
            (nameof(SteamManager), () => SteamManager.Instance != null)
        };

        foreach (var (name, isPresent) in probes)
        {
            string state;
            try
            {
                state = isPresent() ? "present" : "absent";
            }
            catch (Exception exception)
            {
                state = "error: " + exception.Message;
            }

            context.Note($"{name}: {state}");
        }

        Assert.True(Singleton<BootstrapManager>.HasInstance(), "BootstrapManager must exist once the chainloader has started");
        yield break;
    }
}
