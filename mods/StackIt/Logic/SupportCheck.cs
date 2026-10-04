using System.Collections.Generic;
using System.Numerics;

namespace StackIt.Logic;

public static class SupportCheck
{
    public static SupportReport Evaluate(bool rootChanged, IReadOnlyList<HeldState> held, float tolerance = BridgeRules.MoveTolerance)
    {
        var limit = tolerance * tolerance;
        var lost = new List<int>();
        for (var index = 0; index < held.Count; index++)
        {
            var state = held[index];
            if (!state.Present || Vector3.DistanceSquared(state.Now, state.Then) > limit)
            {
                lost.Add(index);
            }
        }

        var mainLost = held.Count > 0 && rootChanged && lost.Count == held.Count;
        return new SupportReport(mainLost, lost, rootChanged && !mainLost && lost.Count == 0);
    }
}
