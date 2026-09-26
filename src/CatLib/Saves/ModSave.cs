using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatLib.Saves;

public sealed class ModSave
{
    private readonly object _sync = new();
    private JsonObject _values = new();
    private long _changes;
    private long _writtenChanges;

    internal ModSave(string modId, string modVersion, int dataVersion)
    {
        ModId = modId;
        ModVersion = modVersion;
        DataVersion = dataVersion;
    }

    public event Action Loaded;

    public event Action Saving;

    public string ModId { get; }

    public string ModVersion { get; }

    public int DataVersion { get; }

    public ModSaveState State { get; private set; } = ModSaveState.NoSave;

    public string SaveName { get; private set; }

    public int? StoredDataVersion { get; private set; }

    public SaveReadStatus LastRead { get; private set; } = SaveReadStatus.Missing;

    public string LastReadDetail { get; private set; }

    public bool IsReady => State == ModSaveState.Ready;

    public bool HasUnsavedChanges
    {
        get
        {
            lock (_sync)
            {
                return _changes != _writtenChanges;
            }
        }
    }

    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_sync)
            {
                return _values.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal).ToList();
            }
        }
    }

    public bool Has(string key)
    {
        lock (_sync)
        {
            return key != null && _values.ContainsKey(key);
        }
    }

    public T Get<T>(string key, T fallback = default)
    {
        lock (_sync)
        {
            if (key == null || !_values.TryGetPropertyValue(key, out var node) || node == null)
            {
                return fallback;
            }

            try
            {
                return node.Deserialize<T>();
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException or FormatException)
            {
                return fallback;
            }
        }
    }

    public bool Set<T>(string key, T value)
    {
        ValidateKey(key);
        var node = JsonSerializer.SerializeToNode(value);
        lock (_sync)
        {
            if (State != ModSaveState.Ready)
            {
                return false;
            }

            if (_values.TryGetPropertyValue(key, out var current) && string.Equals(current?.ToJsonString(), node?.ToJsonString(), StringComparison.Ordinal))
            {
                return true;
            }

            _values[key] = node;
            _changes++;
            return true;
        }
    }

    public bool Remove(string key)
    {
        lock (_sync)
        {
            if (State != ModSaveState.Ready || key == null || !_values.Remove(key))
            {
                return false;
            }

            _changes++;
            return true;
        }
    }

    internal void Attach(string saveName, SaveReadResult read)
    {
        lock (_sync)
        {
            SaveName = saveName;
            LastRead = read.Status;
            LastReadDetail = read.Detail;
            StoredDataVersion = read.Document?.DataVersion;
            var tooNew = read.Status == SaveReadStatus.TooNew || (read.Document != null && read.Document.DataVersion > DataVersion);
            State = !read.AllowsWriting || tooNew ? ModSaveState.ReadOnly : ModSaveState.Ready;
            _values = tooNew || read.Document == null ? new JsonObject() : (JsonObject)JsonNode.Parse(read.Document.Values.ToJsonString());
            _changes = State == ModSaveState.Ready && read.Status == SaveReadStatus.LoadedFromBackup ? 1 : 0;
            _writtenChanges = 0;
        }
    }

    internal void Detach()
    {
        lock (_sync)
        {
            State = ModSaveState.NoSave;
            SaveName = null;
            StoredDataVersion = null;
            LastRead = SaveReadStatus.Missing;
            LastReadDetail = null;
            _values = new JsonObject();
            _changes = 0;
            _writtenChanges = 0;
        }
    }

    internal (SaveDocument Document, long Changes) Snapshot()
    {
        lock (_sync)
        {
            var document = new SaveDocument(ModId, SaveName)
            {
                ModVersion = ModVersion,
                DataVersion = DataVersion
            };
            foreach (var pair in _values)
            {
                document.Values[pair.Key] = pair.Value == null ? null : JsonNode.Parse(pair.Value.ToJsonString());
            }

            return (document, _changes);
        }
    }

    internal void MarkWritten(long changes)
    {
        lock (_sync)
        {
            _writtenChanges = Math.Max(_writtenChanges, changes);
        }
    }

    internal Action LoadedHandlers => Loaded;

    internal Action SavingHandlers => Saving;

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }
    }
}
