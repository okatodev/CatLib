using System;

namespace BoatTweaks.Logic;

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

    public void Forget() => Current = IntPtr.Zero;
}
