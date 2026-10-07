namespace TooLate.Logic;

public readonly struct HostState
{
    public HostState(bool hosting, bool levelReady, bool restarting, int timePeriod)
    {
        Hosting = hosting;
        LevelReady = levelReady;
        Restarting = restarting;
        TimePeriod = timePeriod;
    }

    public bool Hosting { get; }

    public bool LevelReady { get; }

    public bool Restarting { get; }

    public int TimePeriod { get; }
}

public static class JoinGate
{
    public const int EveningRecap = 2;
    public const int DawnRecap = 5;
    public const double SettleSeconds = 3.0;

    public static bool CanAccept(HostState state) => state.Hosting && state.LevelReady && !state.Restarting;

    public static bool CanStart(HostState state) => CanAccept(state) && !IsRecap(state.TimePeriod);

    public static bool ReadyToStart(HostState state, double secondsConnected) => CanStart(state) && secondsConnected >= SettleSeconds;

    public static bool IsRecap(int timePeriod) => timePeriod is EveningRecap or DawnRecap;
}
