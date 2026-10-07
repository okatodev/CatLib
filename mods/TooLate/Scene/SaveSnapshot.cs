using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Il2CppMicrosoft.Data.Sqlite;
using TooLate.Logic;

namespace TooLate.Scene;

public sealed class SnapshotResult
{
    public string SaveName { get; set; }

    public byte[] Data { get; set; }

    public IReadOnlyList<EntityPlace> Places { get; set; }

    public int Stamps { get; set; }

    public bool FromScratch { get; set; }

    public long Milliseconds { get; set; }

    public override string ToString() =>
        $"{SaveName}: {Data?.Length ?? 0} bytes, {Places?.Count ?? 0} entities, {Stamps} stamps{(FromScratch ? ", built without a save file" : string.Empty)}, {Milliseconds} ms";
}

public static class SaveSnapshot
{
    public const string FolderName = "TooLate";
    public const string FileName = "snapshot.bin";

    public static string Folder => Path.Combine(BepInEx.Paths.CachePath, FolderName);

    public static SnapshotResult Write()
    {
        var watch = Stopwatch.StartNew();
        var save = Singleton<SaveManager>.HasInstance() ? Singleton<SaveManager>.Instance : null;
        if (save == null)
        {
            throw new InvalidOperationException("The game has no save manager");
        }

        var name = save._currentSaveFileName;
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("The game has no current save");
        }

        Directory.CreateDirectory(Folder);
        var target = Path.Combine(Folder, FileName);
        var source = save.GetGameSaveFullPath(name);
        SqliteConnection.ClearAllPools();
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        var fromScratch = !File.Exists(source);
        var places = new List<EntityPlace>();
        var stamps = 0;
        var connection = new SqliteConnection("Data Source=" + target);
        try
        {
            connection.Open();
            var setup = connection.CreateCommand();
            CreateTables(setup);
            if (fromScratch)
            {
                AddMetadata(setup, save);
            }
            else
            {
                CopyProgress(setup, source);
            }

            var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (!save.ExecuteSaveEntities(command))
            {
                throw new InvalidOperationException("The game could not write its entities into the snapshot");
            }

            if (!save.ExecuteSaveStamps(command))
            {
                throw new InvalidOperationException("The game could not write its stamps into the snapshot");
            }

            if (!save.ExecuteEnqueuedGenericSaveData(command))
            {
                throw new InvalidOperationException("The game could not write its progress into the snapshot");
            }

            transaction.Commit();

            var read = connection.CreateCommand();
            read.CommandText = "SELECT Id, PositionX, PositionY, PositionZ FROM Entity";
            var reader = read.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                places.Add(new EntityPlace((uint)Number(reader.GetValue(0)), (float)Number(reader.GetValue(1)), (float)Number(reader.GetValue(2)),
                    (float)Number(reader.GetValue(3))));
            }

            reader.Close();
            read.CommandText = "SELECT COUNT(*) FROM Stamps";
            var countReader = read.ExecuteReader();
            if (countReader.Read())
            {
                stamps = (int)Number(countReader.GetValue(0));
            }

            countReader.Close();
        }
        finally
        {
            connection.Close();
            SqliteConnection.ClearAllPools();
        }

        return new SnapshotResult
        {
            SaveName = name,
            Data = File.ReadAllBytes(target),
            Places = places,
            Stamps = stamps,
            FromScratch = fromScratch,
            Milliseconds = watch.ElapsedMilliseconds
        };
    }

    public static readonly string[] CopiedTables = { "Metadata", "Generic", "ProgressionGeneric" };

    private static void CreateTables(SqliteCommand command)
    {
        Execute(command, SaveManager.CreateMetadataTable);
        Execute(command, SaveManager.CreateEntityTable);
        Execute(command, SaveManager.CreateStampsTable);
        Execute(command, SaveManager.CreateGameGenericTable);
        Execute(command, SaveManager.CreateGameProgressionGenericTable);
    }

    private static void CopyProgress(SqliteCommand command, string source)
    {
        Execute(command, "ATTACH DATABASE '" + Quote(source) + "' AS original");
        try
        {
            Execute(command, "INSERT INTO Metadata (CreationGameVersion, SaveName) SELECT CreationGameVersion, SaveName FROM original.Metadata");
            Execute(command, "INSERT INTO Generic (Key, ValueNumeric, ValueString, ValueBlob) SELECT Key, ValueNumeric, ValueString, ValueBlob FROM original.Generic");
            Execute(command, "INSERT INTO ProgressionGeneric (Key, ValueNumeric, ValueString, ValueBlob) SELECT Key, ValueNumeric, ValueString, ValueBlob FROM original.ProgressionGeneric");
        }
        finally
        {
            Execute(command, "DETACH DATABASE original");
        }
    }

    private static void AddMetadata(SqliteCommand command, SaveManager save)
    {
        var version = Singleton<BootstrapManager>.HasInstance() ? Singleton<BootstrapManager>.Instance.GameVersion : string.Empty;
        command.CommandText = "INSERT INTO Metadata (CreationGameVersion, SaveName) VALUES ('" + Quote(version) + "', '" + Quote(save._currentSaveDisplayName) + "')";
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteCommand command, string text)
    {
        command.CommandText = text;
        command.ExecuteNonQuery();
    }

    private static string Quote(string text) => (text ?? string.Empty).Replace("'", "''");

    private static double Number(Il2CppSystem.Object value)
    {
        if (value == null)
        {
            return 0;
        }

        var type = value.GetIl2CppType()?.Name;
        return type switch
        {
            "DBNull" => 0,
            "Int64" => value.Unbox<long>(),
            "Int32" => value.Unbox<int>(),
            "Double" => value.Unbox<double>(),
            "Single" => value.Unbox<float>(),
            _ => double.Parse(value.ToString(), CultureInfo.InvariantCulture)
        };
    }
}
