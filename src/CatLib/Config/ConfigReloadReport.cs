namespace CatLib.Config;

public sealed record ConfigReloadReport(string FilePath, string OwnerId, int Changed, int Rejected, int Adjusted, int Attempts, bool Forced);
