using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Net;
using CatLib.Tests.Framework;
using CatLib.Tests.Suites.Settings;

namespace CatLib.Tests.Suites.Network;

public sealed class SessionSettingsSyncTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("SessionSync");
        var owner = sandbox.OwnerId;
        var limit = sandbox.Settings.Session("Rules", "Limit", 3, "Limit.", new AcceptableValueRange<int>(1, 10));
        var name = sandbox.Settings.Session("Rules", "Name", "Cat", "Name.");
        sandbox.Settings.Local("Rules", "Fov", 90, "Local setting.");
        var fast = sandbox.Settings.Session("Rules", "Fast", false, "Restart only.").RequiresRestart();
        var applied = new List<int>();
        limit.Apply(applied.Add);
        var effectiveChanges = new List<string>();
        void OnEffective(ISetting setting)
        {
            if (setting.Owner == sandbox.Settings)
            {
                effectiveChanges.Add(setting.Key);
            }
        }

        CatConfig.EffectiveValueChanged += OnEffective;
        try
        {
            var snapshot = SessionSettings.Snapshot().Where(value => value.OwnerId == owner).Select(value => value.Key + "=" + value.Value).ToList();
            Assert.SequenceEqual(new[] { "Limit=3", "Name=Cat", "Fast=false" }, snapshot, "Snapshot contains only session settings");

            var result = SessionSettings.Instance.Apply(new[]
            {
                new SessionSettingValue(owner, "Rules", "Limit", "7"),
                new SessionSettingValue(owner, "Rules", "Name", "Tabby"),
                new SessionSettingValue(owner, "Rules", "Fov", "120"),
                new SessionSettingValue(owner, "Rules", "Fast", "true"),
                new SessionSettingValue(owner, "Rules", "Missing", "1"),
                new SessionSettingValue("other.mod", "Rules", "Limit", "1")
            });

            Assert.Equal(2, result.Applied, "Applied overrides");
            Assert.SequenceEqual(new[] { owner + "/Rules/Fov", owner + "/Rules/Missing", "other.mod/Rules/Limit" }, result.Unknown, "Unknown or local settings");
            Assert.SequenceEqual(new[] { owner + "/Rules/Fast" }, result.RestartBlocked, "Restart-only settings that differ");
            Assert.Equal(7, limit.Value, "Overridden value");
            Assert.Equal(3, limit.LocalValue, "Local value stays");
            Assert.True(limit.IsOverridden, "IsOverridden");
            Assert.Equal("Tabby", name.Value, "Overridden text");
            Assert.False(fast.Value, "Restart-only session settings keep their value");
            Assert.SequenceEqual(new[] { 3, 7 }, applied, "Appliers see the host value");
            Assert.SequenceEqual(new[] { "Limit", "Name" }, effectiveChanges, "Effective value change events");
            Assert.True(File.ReadAllText(sandbox.FilePath).Contains("Limit = 3"), "Overrides must never touch the file");

            var invalid = SessionSettings.Instance.Apply(new[]
            {
                new SessionSettingValue(owner, "Rules", "Limit", "99"),
                new SessionSettingValue(owner, "Rules", "Limit", "many")
            });
            Assert.Equal(2, invalid.Invalid.Count, "Out of range and unparsable host values are rejected");
            Assert.Equal(7, limit.Value, "Invalid host values keep the previous override");

            limit.Entry.Value = 5;
            Assert.Equal(7, limit.Value, "Local edits do not replace an active override");
            Assert.Equal(5, limit.LocalValue, "Local edits are still saved");

            var cleared = SessionSettings.Instance.Clear();
            Assert.True(cleared >= 2, "Clear removes the overrides");
            Assert.False(limit.IsOverridden, "IsOverridden after clear");
            Assert.Equal(5, limit.Value, "After leaving the session the local value applies");
            Assert.Equal("Cat", name.Value, "Text after clear");
            Assert.SequenceEqual(new[] { 3, 7, 5 }, applied, "Appliers see the value after the session");
        }
        finally
        {
            CatConfig.EffectiveValueChanged -= OnEffective;
            SessionSettings.Instance.Clear();
        }

        yield break;
    }
}
