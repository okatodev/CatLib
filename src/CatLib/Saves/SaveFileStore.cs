using System;
using System.Globalization;
using System.IO;

namespace CatLib.Saves;

public sealed class SaveFileStore
{
    public const string Extension = ".json";
    public const string BackupExtension = ".json.bak";
    public const string TemporaryExtension = ".json.tmp";
    public const string CorruptMarker = ".corrupt-";
    public const string ArchiveMarker = ".archived-";

    public SaveFileStore(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string FolderOf(string saveName) => Path.Combine(Root, saveName);

    public string PathOf(string saveName, string modId) => Path.Combine(FolderOf(saveName), SaveNames.ForFile(modId) + Extension);

    public string BackupPathOf(string saveName, string modId) => Path.Combine(FolderOf(saveName), SaveNames.ForFile(modId) + BackupExtension);

    public SaveReadResult Read(string saveName, string modId)
    {
        var path = PathOf(saveName, modId);
        var backup = BackupPathOf(saveName, modId);
        var hasMain = File.Exists(path);
        var hasBackup = File.Exists(backup);
        if (!hasMain && !hasBackup)
        {
            return new SaveReadResult(SaveReadStatus.Missing);
        }

        var problem = "only the backup was found";
        if (hasMain)
        {
            var main = Load(path);
            if (main.Status != SaveReadStatus.Corrupt)
            {
                return main;
            }

            var kept = SetAside(path, CorruptMarker);
            if (kept == null)
            {
                return new SaveReadResult(SaveReadStatus.Unreadable, null, main.Detail + "; the damaged file could not be moved aside");
            }

            problem = $"{main.Detail}; the damaged file was kept as {Path.GetFileName(kept)}";
            if (!hasBackup)
            {
                return new SaveReadResult(SaveReadStatus.Corrupt, null, problem);
            }
        }

        var fallback = Load(backup);
        return fallback.Status switch
        {
            SaveReadStatus.Loaded => new SaveReadResult(SaveReadStatus.LoadedFromBackup, fallback.Document, problem),
            SaveReadStatus.Corrupt => new SaveReadResult(SaveReadStatus.Corrupt, null, $"{problem}; the backup is damaged too: {fallback.Detail}"),
            _ => new SaveReadResult(fallback.Status, fallback.Document, $"{problem}; backup: {fallback.Detail}")
        };
    }

    public void Write(SaveDocument document, DateTime now)
    {
        var path = PathOf(document.SaveName, document.ModId);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temporary = Path.Combine(FolderOf(document.SaveName), SaveNames.ForFile(document.ModId) + TemporaryExtension);
        var backup = BackupPathOf(document.SaveName, document.ModId);
        File.WriteAllText(temporary, document.ToJson(now));
        if (File.Exists(path))
        {
            File.Replace(temporary, path, backup, true);
        }
        else
        {
            File.Move(temporary, path);
        }
    }

    public string Archive(string saveName, DateTime now)
    {
        var folder = FolderOf(saveName);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var target = folder + ArchiveMarker + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        for (var suffix = 2; Directory.Exists(target); suffix++)
        {
            target = folder + ArchiveMarker + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        Directory.Move(folder, target);
        return target;
    }

    private static SaveReadResult Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SaveReadResult(SaveReadStatus.Unreadable, null, exception.Message);
        }

        try
        {
            var document = SaveDocument.FromJson(text);
            return document.Format > SaveDocument.CurrentFormat
                ? new SaveReadResult(SaveReadStatus.TooNew, document, $"format {document.Format} is newer than {SaveDocument.CurrentFormat}")
                : new SaveReadResult(SaveReadStatus.Loaded, document);
        }
        catch (FormatException exception)
        {
            return new SaveReadResult(SaveReadStatus.Corrupt, null, exception.Message);
        }
    }

    private static string SetAside(string path, string marker)
    {
        var target = path + marker + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        for (var suffix = 2; File.Exists(target); suffix++)
        {
            target = path + marker + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        try
        {
            File.Move(path, target);
            return target;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
