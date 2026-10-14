using System;
using System.Collections.Generic;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TooLate.Logic;
using UnityEngine;

namespace TooLate.Scene;

public static class SpawnPoints
{
    private static CatLogger _log;
    private static IntPtr _extended;

    public static void Initialize(CatLogger log) => _log = log;

    public static void Ensure(PlayerManager manager)
    {
        if (manager == null || manager.Pointer == _extended)
        {
            return;
        }

        var points = manager._spawnPoints;
        if (points == null || points.Length < 2)
        {
            return;
        }

        if (points.Length >= PlayerLimit.MostPlayers)
        {
            _extended = manager.Pointer;
            return;
        }

        var game = new List<(float X, float Y, float Z)>();
        foreach (var point in points)
        {
            if (point == null)
            {
                return;
            }

            var position = point.position;
            game.Add((position.x, position.y, position.z));
        }

        var extra = SpawnSpots.Extra(game, PlayerLimit.MostPlayers);
        var all = new Il2CppReferenceArray<Transform>(game.Count + extra.Count);
        for (var index = 0; index < points.Length; index++)
        {
            all[index] = points[index];
        }

        var parent = points[0].parent;
        var described = new List<string>();
        for (var index = 0; index < extra.Count; index++)
        {
            var spot = extra[index];
            var near = points[spot.Nearest];
            var made = new GameObject($"TooLate spawn point {game.Count + index + 1}").transform;
            made.SetParent(parent, false);
            made.position = new Vector3(spot.X, spot.Y, spot.Z);
            made.rotation = near.rotation;
            all[game.Count + index] = made;
            described.Add($"{game.Count + index + 1} at {made.position}");
        }

        manager._spawnPoints = all;
        _extended = manager.Pointer;
        _log?.Info($"Spawn points for players {game.Count + 1} to {all.Length} added between the game's {game.Count}: {string.Join(", ", described)}");
    }
}
