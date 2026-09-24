using System.Collections.Generic;

namespace CatLib.Net;

public sealed record SessionApplyResult(int Applied, IReadOnlyList<string> Unknown, IReadOnlyList<string> Invalid, IReadOnlyList<string> RestartBlocked)
{
    public static SessionApplyResult Empty { get; } = new(0, new string[0], new string[0], new string[0]);
}
