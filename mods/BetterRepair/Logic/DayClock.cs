namespace BetterRepair.Logic;

public sealed class DayClock
{
    public const int DayPeriod = 0;
    public const int DawnRecapPeriod = 5;

    private int? _last;

    public int? Last => _last;

    public bool Observe(int period, out bool changed)
    {
        changed = _last.HasValue && _last.Value != period;
        var newDay = changed && (period == DawnRecapPeriod || (period == DayPeriod && _last.Value != DawnRecapPeriod));
        _last = period;
        return newDay;
    }

    public void Reset() => _last = null;
}
