using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CatLib.Net;

public static class VersionMatcher
{
    private static readonly Regex Pattern = new(@"^\s*v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?(?:[-+].*)?\s*$", RegexOptions.CultureInvariant);

    public static bool Matches(string hostVersion, string clientVersion, VersionRule rule)
    {
        if (rule == VersionRule.Any)
        {
            return true;
        }

        if (!TryParse(hostVersion, out var hostMajor, out var hostMinor) || !TryParse(clientVersion, out var clientMajor, out var clientMinor))
        {
            return string.Equals((hostVersion ?? string.Empty).Trim(), (clientVersion ?? string.Empty).Trim(), StringComparison.Ordinal);
        }

        return rule switch
        {
            VersionRule.SameMajor => hostMajor == clientMajor,
            VersionRule.SameMinor => hostMajor == clientMajor && hostMinor == clientMinor,
            _ => string.Equals(hostVersion.Trim(), clientVersion.Trim(), StringComparison.Ordinal)
        };
    }

    public static bool TryParse(string version, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var match = Pattern.Match(version);
        if (!match.Success)
        {
            return false;
        }

        major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return true;
    }
}
