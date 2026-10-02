using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Logging;

namespace CatLib.Game;

public enum GameBuildStatus
{
    Unknown,
    Supported,
    GameOlder,
    GameNewer
}

public sealed record GameBuildCheck(GameBuildStatus Status, GameBuild Running, GameBuild Target)
{
    public bool IsWarning => Status == GameBuildStatus.GameOlder || Status == GameBuildStatus.GameNewer;
}

public static class GameCompatibility
{
    public static readonly IReadOnlyList<GameBuild> Supported = new[]
    {
        GameBuild.Parse("CMC 1.01.00.1737.9770.21112", 25651540)
    };

    private static CatLogger _log;
    private static GameBuildCheck _current;

    public static GameBuildCheck Current
    {
        get
        {
            if (_current == null)
            {
                TryCheck();
            }

            return _current;
        }
    }

    public static GameBuildStatus Status => Current?.Status ?? GameBuildStatus.Unknown;

    public static GameBuild Target => Newest(Supported);

    internal static void Initialize(CatLogger log) => _log = log;

    public static GameBuildCheck Compare(GameBuild running, IReadOnlyList<GameBuild> supported)
    {
        var target = Newest(supported);
        if (running == null || target == null)
        {
            return new GameBuildCheck(GameBuildStatus.Unknown, running, target);
        }

        var stamped = supported.Where(build => build.HasStamp).ToList();
        if (running.HasStamp && stamped.Count > 0)
        {
            if (stamped.Any(build => build.CompareStamp(running) == 0))
            {
                return new GameBuildCheck(GameBuildStatus.Supported, running, Match(supported, running));
            }

            var newest = stamped.OrderBy(build => build.Days).ThenBy(build => build.HalfSeconds).Last();
            return new GameBuildCheck(running.CompareStamp(newest) > 0 ? GameBuildStatus.GameNewer : GameBuildStatus.GameOlder, running, newest);
        }

        var numbered = supported.Where(build => build.HasSteamBuild).ToList();
        if (running.HasSteamBuild && numbered.Count > 0)
        {
            var same = numbered.FirstOrDefault(build => build.SteamBuild == running.SteamBuild);
            if (same != null)
            {
                return new GameBuildCheck(GameBuildStatus.Supported, running, same);
            }

            var newest = numbered.OrderBy(build => build.SteamBuild).Last();
            return new GameBuildCheck(running.SteamBuild > newest.SteamBuild ? GameBuildStatus.GameNewer : GameBuildStatus.GameOlder, running, newest);
        }

        return new GameBuildCheck(GameBuildStatus.Unknown, running, target);
    }

    internal static GameBuildCheck TryCheck()
    {
        if (_current != null)
        {
            return _current;
        }

        var version = GameInfo.GameVersion;
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        _current = Compare(GameBuild.Parse(version, ReadSteamBuild()), Supported);
        Report(_current);
        return _current;
    }

    internal static void Reset() => _current = null;

    private static GameBuild Match(IReadOnlyList<GameBuild> supported, GameBuild running) =>
        supported.FirstOrDefault(build => build.HasStamp && build.CompareStamp(running) == 0) ?? Newest(supported);

    private static GameBuild Newest(IReadOnlyList<GameBuild> supported)
    {
        if (supported == null || supported.Count == 0)
        {
            return null;
        }

        return supported.OrderBy(build => build.HasStamp ? 1 : 0).ThenBy(build => build.Days).ThenBy(build => build.HalfSeconds).ThenBy(build => build.SteamBuild).Last();
    }

    private static int ReadSteamBuild()
    {
        try
        {
            return Steamworks.SteamApps.GetAppBuildId();
        }
        catch (Exception exception)
        {
            _log?.Debug($"Reading the Steam build id failed: {exception.Message}");
            return 0;
        }
    }

    private static void Report(GameBuildCheck check)
    {
        if (_log == null)
        {
            return;
        }

        var running = check.Running.Describe();
        var target = check.Target?.Describe() ?? "no build";
        switch (check.Status)
        {
            case GameBuildStatus.Supported:
                _log.Info($"The game ({running}) is a build CatLib {PluginMeta.Version} is made for");
                break;
            case GameBuildStatus.GameNewer:
                _log.Warning($"The game ({running}) is newer than the build CatLib {PluginMeta.Version} is made for ({target}), a CatLib update may be needed");
                break;
            case GameBuildStatus.GameOlder:
                _log.Warning($"The game ({running}) is older than the build CatLib {PluginMeta.Version} is made for ({target}), updating the game is advised");
                break;
            default:
                _log.Warning($"The game build could not be read from \"{check.Running.Version}\", CatLib {PluginMeta.Version} is made for {target}");
                break;
        }
    }
}
