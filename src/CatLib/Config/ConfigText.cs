using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace CatLib.Config;

internal static class ConfigText
{
    public static Dictionary<ConfigDefinition, string> Parse(string text)
    {
        var values = new Dictionary<ConfigDefinition, string>();
        var currentSection = string.Empty;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line.Substring(1, line.Length - 2);
                continue;
            }

            var split = line.Split(new[] { '=' }, 2);
            if (split.Length != 2)
            {
                continue;
            }

            var key = split[0].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            ConfigDefinition definition;
            try
            {
                definition = new ConfigDefinition(currentSection, key);
            }
            catch (ArgumentException)
            {
                continue;
            }

            values[definition] = split[1].Trim();
        }

        return values;
    }
}
