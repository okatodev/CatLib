using System.Collections.Generic;

namespace StackIt.Logic;

public sealed record SupportReport(bool MainLost, IReadOnlyList<int> Lost, bool MovedTogether)
{
    public bool SupportLost => !MainLost && Lost.Count > 0;
}
