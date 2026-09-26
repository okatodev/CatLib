using System;

namespace CatLib.DevTools;

public sealed class DevItem
{
    private readonly Func<string> _run;
    private readonly Func<string> _state;

    internal DevItem(string group, string label, string hint, Func<string> run, Func<string> state)
    {
        Group = string.IsNullOrWhiteSpace(group) ? "General" : group;
        Label = string.IsNullOrWhiteSpace(label) ? throw new ArgumentException("Label must not be empty.", nameof(label)) : label;
        Hint = hint;
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _state = state;
    }

    public string Group { get; }

    public string Label { get; }

    public string Hint { get; }

    public bool IsToggle => _state != null;

    public string State
    {
        get
        {
            try
            {
                return _state?.Invoke();
            }
            catch (Exception exception)
            {
                return "error: " + exception.GetType().Name;
            }
        }
    }

    internal string Run() => _run();

    public static DevItem Command(string group, string label, Func<string> run, string hint = null) => new(group, label, hint, run, null);

    public static DevItem Command(string group, string label, Action run, string hint = null)
    {
        if (run == null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        return new DevItem(group, label, hint, () =>
        {
            run();
            return null;
        }, null);
    }

    public static DevItem Toggle(string group, string label, Func<bool> get, Action<bool> set, string hint = null)
    {
        if (get == null || set == null)
        {
            throw new ArgumentNullException(get == null ? nameof(get) : nameof(set));
        }

        return new DevItem(group, label, hint, () =>
        {
            set(!get());
            return get() ? "on" : "off";
        }, () => get() ? "on" : "off");
    }
}
