using System;
using System.Globalization;

namespace CatLib.Game;

public sealed class GameBuild
{
    public const int MaxDays = 40000;
    public const int HalfSecondsPerDay = 43200;
    public static readonly DateTime StampEpoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private GameBuild(string version, int steamBuild, int days, int halfSeconds, bool hasStamp)
    {
        Version = version ?? string.Empty;
        SteamBuild = steamBuild > 0 ? steamBuild : 0;
        Days = days;
        HalfSeconds = halfSeconds;
        HasStamp = hasStamp;
    }

    public string Version { get; }

    public int SteamBuild { get; }

    public int Days { get; }

    public int HalfSeconds { get; }

    public bool HasStamp { get; }

    public bool HasSteamBuild => SteamBuild > 0;

    public DateTime? BuiltAt => HasStamp ? StampEpoch.AddDays(Days).AddSeconds(HalfSeconds * 2) : null;

    public string BuiltOn => HasStamp ? BuiltAt.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;

    public static GameBuild Parse(string version, int steamBuild = 0)
    {
        var text = (version ?? string.Empty).Trim();
        var space = text.LastIndexOf(' ');
        var numbers = (space < 0 ? text : text.Substring(space + 1)).Split('.');
        if (numbers.Length >= 2
            && int.TryParse(numbers[numbers.Length - 2], NumberStyles.None, CultureInfo.InvariantCulture, out var days)
            && int.TryParse(numbers[numbers.Length - 1], NumberStyles.None, CultureInfo.InvariantCulture, out var halfSeconds)
            && days > 0 && days < MaxDays && halfSeconds < HalfSecondsPerDay)
        {
            return new GameBuild(text, steamBuild, days, halfSeconds, true);
        }

        return new GameBuild(text, steamBuild, 0, 0, false);
    }

    public int CompareStamp(GameBuild other) =>
        Days != other.Days ? Days.CompareTo(other.Days) : HalfSeconds.CompareTo(other.HalfSeconds);

    public string Describe()
    {
        var parts = string.IsNullOrEmpty(Version) ? "unknown version" : Version;
        if (HasStamp)
        {
            parts += ", built " + BuiltAt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return HasSteamBuild ? parts + ", Steam build " + SteamBuild.ToString(CultureInfo.InvariantCulture) : parts;
    }

    public override string ToString() => Describe();
}
