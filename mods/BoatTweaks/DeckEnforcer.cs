using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Il2Cpp;
using CatLib.Logging;

namespace BoatTweaks;

public sealed class DeckEnforcer
{
    private readonly CatLogger _log;
    private readonly Dictionary<IntPtr, Entry> _decks = new();

    public DeckEnforcer(CatLogger log)
    {
        _log = log;
    }

    public int Count => _decks.Count;

    public void Hold(EntityInteractableStore store, string key, IReadOnlyList<(int Row, int Column)> cells)
    {
        _decks[store.Pointer] = new Entry(store, key, cells.ToList());
    }

    public void Clear() => _decks.Clear();

    public void Update()
    {
        if (_decks.Count == 0)
        {
            return;
        }

        List<IntPtr> gone = null;
        foreach (var (pointer, entry) in _decks)
        {
            if (entry.Store == null || entry.Store.WasCollected)
            {
                (gone ??= new List<IntPtr>()).Add(pointer);
                continue;
            }

            var array = entry.Store._availableCells;
            var written = entry.Cells.Count(cell => Il2CppArrays.TrySet(array, cell.Row, cell.Column, false));
            if (!entry.Reported)
            {
                entry.Reported = true;
                if (written == entry.Cells.Count)
                {
                    _log.Info($"Deck of {entry.Key}: {written} cell(s) are held taken");
                }
                else
                {
                    _log.Warning($"Deck of {entry.Key}: only {written} of {entry.Cells.Count} cell(s) could be held taken");
                }
            }
        }

        if (gone != null)
        {
            foreach (var pointer in gone)
            {
                _decks.Remove(pointer);
            }
        }
    }

    private sealed class Entry
    {
        public Entry(EntityInteractableStore store, string key, List<(int Row, int Column)> cells)
        {
            Store = store;
            Key = key;
            Cells = cells;
        }

        public EntityInteractableStore Store { get; }

        public string Key { get; }

        public List<(int Row, int Column)> Cells { get; }

        public bool Reported { get; set; }
    }
}
