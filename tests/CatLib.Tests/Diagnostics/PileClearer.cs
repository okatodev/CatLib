using System;
using System.Collections.Generic;
using CatLib.Core;
using CatLib.Logging;
using UnityEngine;

namespace CatLib.Tests.Diagnostics;

public sealed class PileClearer
{
    public const double StepWaitSeconds = 5;
    public const double ClearLimitSeconds = 120;

    private readonly CatLogger _log;
    private readonly Dictionary<IntPtr, double> _clearing = new();
    private double _clearStarted;

    public PileClearer(CatLogger log)
    {
        _log = log;
        FrameLoop.Update += Update;
    }

    public bool IsClearing => _clearing.Count > 0;

    public static string NameOf(PileType type) => type switch
    {
        PileType.MainPile => "main pile",
        PileType.ColdRoom => "cold room pile",
        PileType.HotRoom => "hot room pile",
        _ => "pile " + type
    };

    public static bool IsClearedStep(int index, int count) => index < 0 || index >= count;

    public static string StepText(int index, int count) => IsClearedStep(index, count) ? "cleared" : $"step {index + 1} of {count}";

    public string NextStep()
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        var results = new List<string>();
        foreach (var pile in UnityEngine.Object.FindObjectsOfType<EntityPile>())
        {
            if (pile == null)
            {
                continue;
            }

            var name = NameOf(pile.PileType);
            if (!pile._isInitialized)
            {
                results.Add($"{name} not started, its wall of parcels stands");
                continue;
            }

            if (IsCleared(pile))
            {
                results.Add($"{name} already cleared");
                continue;
            }

            if (Pending(pile) > 0)
            {
                results.Add($"{name} still gets its parcels, try again");
                continue;
            }

            results.Add($"{name} {Step(pile)}");
        }

        return results.Count == 0 ? "no pile in this level" : string.Join(", ", results);
    }

    public string Clear()
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        if (IsClearing)
        {
            return $"already clearing {_clearing.Count} pile(s)";
        }

        var waiting = new List<string>();
        var skipped = new List<string>();
        foreach (var pile in UnityEngine.Object.FindObjectsOfType<EntityPile>())
        {
            if (pile == null)
            {
                continue;
            }

            var name = NameOf(pile.PileType);
            if (!pile._isInitialized)
            {
                skipped.Add($"{name} not started");
            }
            else if (!IsCleared(pile))
            {
                _clearing[pile.Pointer] = double.NegativeInfinity;
                waiting.Add(name);
            }
        }

        _clearStarted = FrameLoop.Realtime;
        var text = waiting.Count == 0 ? "every started pile is already cleared" : $"clearing {string.Join(", ", waiting)} step by step";
        return skipped.Count == 0 ? text : $"{text}; {string.Join(", ", skipped)}, its wall of parcels stands";
    }

    private string Step(EntityPile pile)
    {
        var count = pile.PileSizes?.Length ?? 0;
        var before = pile.CurrentPileSizeIndex;
        var removed = RemoveAnchoredParcels(pile);
        pile.ParcelAmountInCurrentPile = 0f;
        pile.SwitchToNextPileSize();
        var after = pile.CurrentPileSizeIndex;
        var door = pile.LinkedDoor;
        var doorText = door != null && door.RoomDoor != null && after >= door.PileStep ? ", its door is open" : string.Empty;
        _log.Info($"{NameOf(pile.PileType)}: {StepText(before, count)} -> {StepText(after, count)}, {removed} parcel(s) of the step removed{doorText}");
        return $"{StepText(before, count)} -> {StepText(after, count)}{doorText}";
    }

    private static int RemoveAnchoredParcels(EntityPile pile)
    {
        var anchored = pile._anchoredParcels;
        if (anchored == null)
        {
            return 0;
        }

        var entities = new List<Entity>();
        var anchors = new List<Transform>();
        foreach (var pair in anchored)
        {
            entities.Add(pair.Key);
            if (pair.Value != null && pair.Value.anchor != null)
            {
                anchors.Add(pair.Value.anchor);
            }
        }

        var occupied = pile._anchorOccupiedMap;
        if (occupied != null)
        {
            foreach (var anchor in anchors)
            {
                if (occupied.ContainsKey(anchor))
                {
                    occupied[anchor] = false;
                }
            }
        }

        anchored.Clear();
        var removed = 0;
        foreach (var entity in entities)
        {
            if (entity == null || entity.IsDisposed)
            {
                continue;
            }

            entity.Dispose(false);
            removed++;
        }

        return removed;
    }

    private static bool IsCleared(EntityPile pile) => IsClearedStep(pile.CurrentPileSizeIndex, pile.PileSizes?.Length ?? 0);

    private static int Pending(EntityPile pile) => pile._requestedSpawnIndexes?.Count ?? 0;

    private void Update()
    {
        if (_clearing.Count == 0)
        {
            return;
        }

        try
        {
            if (HostProblem() != null)
            {
                _log.Warning($"Clearing the piles stopped, {_clearing.Count} pile(s) left: {HostProblem()}");
                _clearing.Clear();
                return;
            }

            if (FrameLoop.Realtime - _clearStarted > ClearLimitSeconds)
            {
                _log.Warning($"Clearing the piles stopped after {ClearLimitSeconds:0} s, {_clearing.Count} pile(s) left");
                _clearing.Clear();
                return;
            }

            var done = new List<IntPtr>();
            foreach (var pile in UnityEngine.Object.FindObjectsOfType<EntityPile>())
            {
                if (pile == null || !_clearing.TryGetValue(pile.Pointer, out var lastStep))
                {
                    continue;
                }

                if (IsCleared(pile))
                {
                    done.Add(pile.Pointer);
                    _log.Info($"{NameOf(pile.PileType)} is cleared");
                    continue;
                }

                if (Pending(pile) > 0 && FrameLoop.Realtime - lastStep < StepWaitSeconds)
                {
                    continue;
                }

                Step(pile);
                _clearing[pile.Pointer] = FrameLoop.Realtime;
            }

            foreach (var pointer in done)
            {
                _clearing.Remove(pointer);
            }

            if (_clearing.Count > 0 && done.Count == 0 && UnityEngine.Object.FindObjectsOfType<EntityPile>().Length == 0)
            {
                _clearing.Clear();
            }
        }
        catch (Exception exception)
        {
            _clearing.Clear();
            _log.Error("Clearing the piles failed", exception);
        }
    }

    private static string HostProblem()
    {
        var network = Singleton<NetworkManager>.HasInstance() ? Singleton<NetworkManager>.Instance : null;
        if (network == null || !network.IsServer)
        {
            return "only the host can do this";
        }

        var boot = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance : null;
        if (boot == null || !boot.LoadFinalized || !Singleton<GameManager>.HasInstance() || !Singleton<GameManager>.Instance.IsGameInitialized)
        {
            return "only in a level";
        }

        return null;
    }
}
