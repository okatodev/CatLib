namespace CatLib.Net;

public static class SteamIds
{
    public const ulong IndividualBase = 76561197960265728UL;
    public const ulong IndividualLimit = IndividualBase + uint.MaxValue;

    public static bool IsIndividual(ulong steamId) => steamId > IndividualBase && steamId <= IndividualLimit;
}
