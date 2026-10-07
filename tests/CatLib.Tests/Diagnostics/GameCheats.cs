using System;
using CatLib.Core;
using CatLib.Logging;

namespace CatLib.Tests.Diagnostics;

public sealed class GameCheats
{
    public const int MostSteps = 6;

    private readonly CatLogger _log;
    private int _stepsLeft;
    private bool _toRecap;

    public GameCheats(CatLogger log)
    {
        _log = log;
        FrameLoop.Update += Update;
    }

    public bool IsSkipping => _stepsLeft > 0;

    public static bool IsRecap(GameTimePeriod period) => period is GameTimePeriod.EveningRecap or GameTimePeriod.DawnRecap;

    public string ServeCustomers()
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        var served = 0;
        var waiting = 0;
        foreach (var counter in UnityEngine.Object.FindObjectsOfType<CustomerCounter>())
        {
            if (counter == null || !counter._isCustomerRequesting || counter._currentCustomer == null)
            {
                continue;
            }

            waiting++;
            try
            {
                counter.ValidateRequest();
                served++;
            }
            catch (Exception exception)
            {
                _log.Warning($"Serving the customer at counter {counter.CounterIndex + 1} failed: {exception.Message}");
            }
        }

        _log.Info($"Served {served} of {waiting} waiting customer(s) at once");
        return waiting == 0 ? "no customer waits at a counter" : $"{served} customer(s) served";
    }

    public string NextPeriod()
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        var time = Singleton<GameTimeManager>.Instance;
        var from = time.CurrentTimePeriod;
        try
        {
            time.CHEAT_CycleTimePeriod();
        }
        finally
        {
            _log.Info($"Time of day changed from {from} to {time.CurrentTimePeriod} by the developer menu");
        }

        return $"{from} -> {time.CurrentTimePeriod}";
    }

    public string SkipToResults()
    {
        var problem = HostProblem();
        if (problem != null)
        {
            return problem;
        }

        var period = Singleton<GameTimeManager>.Instance.CurrentTimePeriod;
        if (IsRecap(period))
        {
            return "the day's results are already shown";
        }

        _toRecap = true;
        _stepsLeft = MostSteps;
        return $"from {period} to the next results, one part of the day per frame";
    }

    private void Update()
    {
        if (_stepsLeft <= 0)
        {
            return;
        }

        try
        {
            if (HostProblem() != null)
            {
                _stepsLeft = 0;
                return;
            }

            var time = Singleton<GameTimeManager>.Instance;
            if (_toRecap && IsRecap(time.CurrentTimePeriod))
            {
                _stepsLeft = 0;
                _log.Info($"The day's results are shown ({time.CurrentTimePeriod})");
                return;
            }

            _stepsLeft--;
            var before = time.CurrentTimePeriod;
            try
            {
                NextPeriod();
            }
            catch (Exception exception)
            {
                if (time.CurrentTimePeriod == before)
                {
                    throw;
                }

                _log.Warning($"The game threw while moving from {before} to {time.CurrentTimePeriod}, skipping goes on: {exception.Message}");
            }
        }
        catch (Exception exception)
        {
            _stepsLeft = 0;
            _log.Error("Skipping to the day's results failed", exception);
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
        if (boot == null || !boot.LoadFinalized || !Singleton<GameTimeManager>.HasInstance())
        {
            return "only in a level";
        }

        return null;
    }
}
