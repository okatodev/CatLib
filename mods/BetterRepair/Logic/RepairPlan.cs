using System.Collections.Generic;

namespace BetterRepair.Logic;

public sealed class RepairPlan
{
    public RepairPlan(List<Entity> damaged, int parcels)
    {
        Damaged = damaged;
        Parcels = parcels;
    }

    public List<Entity> Damaged { get; }

    public int Parcels { get; }
}
