namespace CatLib.Saves;

public sealed class SaveReadResult
{
    public SaveReadResult(SaveReadStatus status, SaveDocument document = null, string detail = null)
    {
        Status = status;
        Document = document;
        Detail = detail;
    }

    public SaveReadStatus Status { get; }

    public SaveDocument Document { get; }

    public string Detail { get; }

    public bool AllowsWriting => Status is SaveReadStatus.Missing or SaveReadStatus.Loaded or SaveReadStatus.LoadedFromBackup or SaveReadStatus.Corrupt;
}
