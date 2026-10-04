using System;
using CatLib.Config;

namespace StackIt.Settings;

public sealed class StackSettings
{
    public StackSettings(CatSettings settings, Action changed)
    {
        Enabled = settings.Session("Bridges", "Enabled", true,
            "A parcel can stand on two or more parcels at once when their tops are level and it lies fully on them.");
        KeepBalanced = settings.Session("Bridges", "KeepBalanced", false,
            "Off: a parcel standing across a joint falls when any parcel under it is taken away. On: it stays while its centre is above a parcel, and goes along with the parcel under its centre.");
        Enabled.Changed += (_, _) => changed?.Invoke();
        KeepBalanced.Changed += (_, _) => changed?.Invoke();
    }

    public Setting<bool> Enabled { get; }

    public Setting<bool> KeepBalanced { get; }
}
