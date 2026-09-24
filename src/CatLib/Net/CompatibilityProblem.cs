namespace CatLib.Net;

public sealed record CompatibilityProblem(ProblemKind Kind, string Subject, string HostValue, string ClientValue)
{
    public override string ToString() => $"{Kind} {Subject}: host \"{HostValue}\", client \"{ClientValue}\"";
}
