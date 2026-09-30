using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Net;

public sealed record RosterEntry(ulong Id, string Name, bool IsHost, RosterStatus Status, string CatLibVersion, string GameVersion,
    IReadOnlyList<CompatibilityProblem> Problems, IReadOnlyList<RosterMod> Mods)
{
    public int ModCount => Mods.Count(mod => mod.Mark != ModMark.Missing);

    public int ProblemCount => Mods.Count(mod => mod.IsProblem) + Problems.Count(problem => problem.Kind is ProblemKind.GameVersionMismatch or ProblemKind.ProtocolMismatch);

    public bool Has(ProblemKind kind) => Problems.Any(problem => problem.Kind == kind);

    public RosterMod Find(string modId) => Mods.FirstOrDefault(mod => string.Equals(mod.Id, modId, StringComparison.Ordinal));
}
