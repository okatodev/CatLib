using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLib.Logging;

namespace CatLib.Saves;

public sealed class SaveSession
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ModSave> _mods = new(StringComparer.Ordinal);
    private readonly Func<string> _root;
    private readonly Func<bool> _isAuthority;
    private readonly Func<DateTime> _clock;
    private readonly CatLogger _log;
    private SaveFileStore _store;
    private List<(ModSave Mod, SaveDocument Document, long Changes)> _pending;
    private bool _archivePending;

    public SaveSession(Func<string> root, Func<bool> isAuthority, Func<DateTime> clock, CatLogger log)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
        _clock = clock ?? (() => DateTime.Now);
        _log = log;
    }

    public string CurrentSave { get; private set; }

    public bool IsAttached => CurrentSave != null;

    public bool IsSaving
    {
        get
        {
            lock (_sync)
            {
                return _pending != null;
            }
        }
    }

    public string Root => _store?.Root;

    public IReadOnlyList<ModSave> Mods
    {
        get
        {
            lock (_sync)
            {
                return _mods.Values.OrderBy(mod => mod.ModId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public ModSave Register(string modId, string modVersion, int dataVersion)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            throw new ArgumentException("Mod id must not be empty.", nameof(modId));
        }

        if (dataVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dataVersion), "Data version starts at 1.");
        }

        ModSave mod;
        lock (_sync)
        {
            if (_mods.TryGetValue(modId, out var existing))
            {
                if (existing.DataVersion != dataVersion)
                {
                    throw new InvalidOperationException($"{modId} is already registered with data version {existing.DataVersion}");
                }

                return existing;
            }

            mod = new ModSave(modId, modVersion ?? string.Empty, dataVersion);
            _mods[modId] = mod;
        }

        if (IsAttached)
        {
            AttachOne(mod, CurrentSave, _archivePending);
        }

        return mod;
    }

    public void Select(string gameFileName, bool? isNewSave)
    {
        var saveName = SaveNames.FromFileName(gameFileName);
        Close();
        if (saveName == null)
        {
            _log?.Warning($"Save data is not attached: the game reported no save file name ('{gameFileName}')");
            return;
        }

        string root;
        try
        {
            root = _root();
        }
        catch (Exception exception)
        {
            _log?.Error($"Save data is not attached: the folder is unknown ({exception.GetType().Name}: {exception.Message})");
            return;
        }

        if (string.IsNullOrWhiteSpace(root))
        {
            _log?.Error("Save data is not attached: the folder is unknown");
            return;
        }

        lock (_sync)
        {
            _store = new SaveFileStore(root);
            CurrentSave = saveName;
            _archivePending = isNewSave == true && Directory.Exists(_store.FolderOf(saveName));
        }

        if (_archivePending)
        {
            _log?.Warning($"Found old mod data for the new save {saveName}; it will not be used and will be archived on the first save");
        }

        foreach (var mod in Mods)
        {
            AttachOne(mod, saveName, _archivePending);
        }

        _log?.Info($"Save data attached to {saveName} for {Mods.Count} mod(s)");
    }

    public void Close()
    {
        lock (_sync)
        {
            if (CurrentSave == null)
            {
                return;
            }

            CurrentSave = null;
            _pending = null;
            _archivePending = false;
        }

        foreach (var mod in Mods)
        {
            mod.Detach();
        }
    }

    public void BeginSave()
    {
        if (!IsAttached)
        {
            return;
        }

        foreach (var mod in Mods.Where(mod => mod.State == ModSaveState.Ready))
        {
            Invoke(mod.SavingHandlers, mod.ModId, "Saving");
        }

        var pending = Mods
            .Where(mod => mod.State == ModSaveState.Ready && mod.HasUnsavedChanges)
            .Select(mod =>
            {
                var snapshot = mod.Snapshot();
                return (mod, snapshot.Document, snapshot.Changes);
            })
            .ToList();

        lock (_sync)
        {
            _pending = pending;
        }
    }

    public SaveCommitReport CommitSave()
    {
        var saveName = CurrentSave;
        if (saveName == null)
        {
            return new SaveCommitReport(null, Array.Empty<string>(), Array.Empty<string>(), null, "no save is attached");
        }

        if (!IsSaving)
        {
            BeginSave();
        }

        List<(ModSave Mod, SaveDocument Document, long Changes)> pending;
        lock (_sync)
        {
            pending = _pending ?? new List<(ModSave, SaveDocument, long)>();
            _pending = null;
        }

        if (!_isAuthority())
        {
            return new SaveCommitReport(saveName, Array.Empty<string>(), Array.Empty<string>(), null, "only the host writes save data");
        }

        string archivedTo = null;
        if (_archivePending)
        {
            try
            {
                archivedTo = _store.Archive(saveName, _clock());
                _archivePending = false;
                if (archivedTo != null)
                {
                    _log?.Warning($"Old mod data of {saveName} was moved to {Path.GetFileName(archivedTo)}");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log?.Error($"Could not archive old mod data of {saveName}, nothing was written: {exception.Message}");
                return new SaveCommitReport(saveName, Array.Empty<string>(), pending.Select(entry => entry.Mod.ModId).ToList(), null, "old data could not be archived");
            }
        }

        var written = new List<string>();
        var failed = new List<string>();
        foreach (var (mod, document, changes) in pending)
        {
            if (mod.State != ModSaveState.Ready || mod.SaveName != saveName)
            {
                continue;
            }

            try
            {
                _store.Write(document, _clock());
                mod.MarkWritten(changes);
                written.Add(mod.ModId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed.Add(mod.ModId);
                _log?.Error($"Could not write save data of {mod.ModId} for {saveName}, the previous file is kept: {exception.Message}");
            }
        }

        if (written.Count > 0)
        {
            _log?.Info($"Save data written for {saveName}: {string.Join(", ", written)}");
        }

        return new SaveCommitReport(saveName, written, failed, archivedTo, null);
    }

    public void FailSave()
    {
        lock (_sync)
        {
            _pending = null;
        }
    }

    private void AttachOne(ModSave mod, string saveName, bool ignoreExisting)
    {
        var read = ignoreExisting ? new SaveReadResult(SaveReadStatus.Missing) : _store.Read(saveName, mod.ModId);
        mod.Attach(saveName, read);
        switch (read.Status)
        {
            case SaveReadStatus.LoadedFromBackup:
                _log?.Warning($"Save data of {mod.ModId} was restored from the backup: {read.Detail}");
                break;
            case SaveReadStatus.Corrupt:
                _log?.Error($"Save data of {mod.ModId} is damaged and starts empty: {read.Detail}");
                break;
            case SaveReadStatus.Unreadable:
                _log?.Error($"Save data of {mod.ModId} cannot be read and will not be written this session: {read.Detail}");
                break;
            case SaveReadStatus.TooNew:
                _log?.Error($"Save data of {mod.ModId} comes from a newer version and will not be touched: {read.Detail}");
                break;
        }

        if (mod.State == ModSaveState.ReadOnly && read.Status is SaveReadStatus.Loaded or SaveReadStatus.LoadedFromBackup)
        {
            _log?.Error($"Save data of {mod.ModId} has data version {read.Document?.DataVersion}, newer than {mod.DataVersion}; it will not be touched");
        }

        Invoke(mod.LoadedHandlers, mod.ModId, "Loaded");
    }

    private void Invoke(Action handlers, string modId, string name)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                _log?.Error($"{name} handler of {modId} failed: {exception}");
            }
        }
    }
}
