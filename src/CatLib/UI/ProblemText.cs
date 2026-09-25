using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;

namespace CatLib.UI;

internal static class ProblemText
{
    public const int MaxListed = 3;

    public static string Summarize(IReadOnlyList<CompatibilityProblem> problems, string languageCode, Func<string, string> nameOf = null)
    {
        var parts = problems.Take(MaxListed).Select(problem => Describe(problem, languageCode, nameOf)).ToList();
        if (problems.Count > MaxListed)
        {
            parts.Add("+" + (problems.Count - MaxListed));
        }

        return string.Join(", ", parts);
    }

    public static string ModsOnly(IReadOnlyList<CompatibilityProblem> problems, Func<string, string> nameOf = null) =>
        string.Join(", ", problems.Take(MaxListed).Select(problem => Name(problem.Subject, nameOf))) +
        (problems.Count > MaxListed ? ", +" + (problems.Count - MaxListed) : string.Empty);

    public static string Describe(CompatibilityProblem problem, string languageCode, Func<string, string> nameOf = null) => problem.Kind switch
    {
        ProblemKind.ProtocolMismatch => UiText.Get(UiText.ProblemProtocol, languageCode),
        ProblemKind.GameVersionMismatch => UiText.Get(UiText.ProblemGame, languageCode),
        ProblemKind.MissingOnClient => UiText.Format(UiText.ProblemMissingOnClient, languageCode, Name(problem.Subject, nameOf)),
        ProblemKind.MissingOnHost => UiText.Format(UiText.ProblemMissingOnHost, languageCode, Name(problem.Subject, nameOf)),
        _ => UiText.Format(UiText.ProblemVersion, languageCode, Name(problem.Subject, nameOf), problem.HostValue, problem.ClientValue)
    };

    private static string Name(string id, Func<string, string> nameOf)
    {
        var name = nameOf?.Invoke(id);
        return string.IsNullOrWhiteSpace(name) ? id : name;
    }
}
