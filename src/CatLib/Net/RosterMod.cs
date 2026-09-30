namespace CatLib.Net;

public sealed record RosterMod(string Id, string Name, string Version, SessionPolicy Policy, ModMark Mark, string HostVersion = null)
{
    public bool IsProblem => Mark is ModMark.Missing or ModMark.OtherVersion or ModMark.NotOnHost;
}
