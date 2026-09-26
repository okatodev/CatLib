using System;
using System.IO;
using CatLib.Core;
using CatLib.Saves;
using CatLib.Tests.Suites.Settings;

namespace CatLib.Tests.Suites.Saves;

internal sealed class SaveSandbox : IDisposable
{
    public const string SaveName = "GameSave_20260101_120000_Cat-Mail-Co";
    public const string SaveFile = SaveName + ".bin";

    public SaveSandbox(string name)
    {
        if (string.IsNullOrEmpty(ConfigSandbox.RootDirectory))
        {
            throw new InvalidOperationException("ConfigSandbox.RootDirectory is not set");
        }

        Root = Path.GetFullPath(Path.Combine(ConfigSandbox.RootDirectory, "Saves_" + name + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)));
        Directory.CreateDirectory(Root);
        Session = NewSession();
    }

    public string Root { get; }

    public SaveSession Session { get; private set; }

    public bool Authority { get; set; } = true;

    public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0);

    public SaveFileStore Store => new(Root);

    public string Folder => Path.Combine(Root, SaveName);

    public SaveSession NewSession() => new(() => Root, () => Authority, () => Now, CatLibRuntime.Log?.Scope("SavesTest"));

    public SaveSession Restart()
    {
        Session = NewSession();
        return Session;
    }

    public string PathOf(string modId) => Store.PathOf(SaveName, modId);

    public string BackupOf(string modId) => Store.BackupPathOf(SaveName, modId);

    public void Save()
    {
        Session.BeginSave();
        Session.CommitSave();
    }

    public void WriteRaw(string modId, string text)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathOf(modId), text);
    }

    public static string Document(string modId, int dataVersion, string valuesJson, int format = SaveDocument.CurrentFormat) =>
        "{\"format\":" + format + ",\"mod\":\"" + modId + "\",\"modVersion\":\"0.1.0\",\"dataVersion\":" + dataVersion + ",\"save\":\"" + SaveName + "\",\"writtenAt\":\"2026-01-01T12:00:00Z\",\"values\":" + valuesJson + "}";

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (Exception)
        {
        }
    }
}
