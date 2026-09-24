using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CatLib.Config;

namespace CatLib.Net;

public sealed class SessionSettings : ISessionSettingsSink
{
    public static SessionSettings Instance { get; } = new();

    public static IReadOnlyList<SessionSettingValue> Snapshot() =>
        CatConfig.All
            .Where(settings => !settings.IsDisposed)
            .SelectMany(settings => settings.Settings)
            .Where(setting => setting.Scope == SettingScope.Session)
            .Select(ToValue)
            .ToList();

    public static SessionSettingValue ToValue(ISetting setting) =>
        new(setting.Owner.OwnerId, setting.Section, setting.Key, Serialize(setting.ValueType, setting.BoxedValue));

    public SessionApplyResult Apply(IReadOnlyList<SessionSettingValue> values)
    {
        var applied = 0;
        var unknown = new List<string>();
        var invalid = new List<string>();
        var restartBlocked = new List<string>();

        foreach (var value in values)
        {
            var node = CatConfig.FindSetting(value.OwnerId, value.Section, value.Key);
            if (node == null || node.Scope != SettingScope.Session)
            {
                unknown.Add(value.Id);
                continue;
            }

            object parsed;
            try
            {
                parsed = Deserialize(node.ValueType, value.Value);
                var acceptable = node.EntryBase.Description?.AcceptableValues;
                if (acceptable != null && !acceptable.IsValid(parsed))
                {
                    invalid.Add(value.Id);
                    continue;
                }
            }
            catch (Exception)
            {
                invalid.Add(value.Id);
                continue;
            }

            if (node.IsRestartRequired)
            {
                if (!Equals(node.BoxedValue, parsed))
                {
                    restartBlocked.Add(value.Id);
                }

                continue;
            }

            if (node.SetOverride(parsed))
            {
                applied++;
            }
            else
            {
                invalid.Add(value.Id);
            }
        }

        return new SessionApplyResult(applied, unknown, invalid, restartBlocked);
    }

    public int Clear()
    {
        var cleared = 0;
        foreach (var settings in CatConfig.All)
        {
            foreach (var setting in settings.Settings.OfType<ISettingNode>())
            {
                if (setting.ClearOverride())
                {
                    cleared++;
                }
            }
        }

        return cleared;
    }

    private static string Serialize(Type type, object value) =>
        type == typeof(string) ? value as string ?? string.Empty : TomlTypeConverter.ConvertToString(value, type);

    private static object Deserialize(Type type, string value) =>
        type == typeof(string) ? value : TomlTypeConverter.ConvertToValue(value, type);
}
