using System;
using System.Collections.Generic;
using System.Reflection;

namespace CatLib.Tests.Framework;

public abstract class TestCase
{
    public abstract string Suite { get; }

    public virtual string Name => GetType().Name;

    public virtual int Order => 0;

    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(15);

    public static readonly ISet<string> ModSuites = new HashSet<string>(StringComparer.Ordinal)
    {
        "BetterRepair", "BoatTweaks", "ParcelBoard", "ShelfLabels", "StackIt", "TooLate", "CustomStamps"
    };

    public virtual string RequiredMod => ModSuites.Contains(Suite) ? Suite : null;

    public static bool IsModInstalled(string assemblyName)
    {
        try
        {
            Assembly.Load(new AssemblyName(assemblyName));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string FullName => Suite + "/" + Name;

    public abstract IEnumerable<TestStep> Run(TestContext context);
}
