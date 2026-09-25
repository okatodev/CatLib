using System;

namespace BoatTweaks;

public sealed class InstanceTracker
{
    public IntPtr Current { get; private set; }

    public bool Observe(IntPtr instance)
    {
        if (instance == Current)
        {
            return false;
        }

        Current = instance;
        return true;
    }
}
