using System.Collections.Generic;

namespace CatLib.Saves;

public sealed class SaveCommitReport
{
    internal SaveCommitReport(string saveName, IReadOnlyList<string> written, IReadOnlyList<string> failed, string archivedTo, string skippedReason)
    {
        SaveName = saveName;
        Written = written;
        Failed = failed;
        ArchivedTo = archivedTo;
        SkippedReason = skippedReason;
    }

    public string SaveName { get; }

    public IReadOnlyList<string> Written { get; }

    public IReadOnlyList<string> Failed { get; }

    public string ArchivedTo { get; }

    public string SkippedReason { get; }

    public bool Skipped => SkippedReason != null;
}
