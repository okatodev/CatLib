using System.Collections.Generic;
using CatLib.Logging;
using CatLib.Saves;

namespace ShelfLabels;

public sealed class LabelStore
{
    private readonly ModSave _save;
    private readonly LabelBoard _board;
    private readonly PlacementBoard _placements;
    private readonly StandBoard _stands;
    private readonly CatLogger _log;

    public LabelStore(ModSave save, LabelBoard board, PlacementBoard placements, StandBoard stands, CatLogger log)
    {
        _save = save;
        _board = board;
        _placements = placements;
        _stands = stands;
        _log = log;
        _save.Loaded += Load;
        _save.Saving += () => Store();
    }

    public void Load()
    {
        _board.Clear();
        _placements.Clear();
        _stands.Clear();
        var entries = new List<KeyValuePair<LabelSlot, int>>();
        var placements = new List<KeyValuePair<int, Placement>>();
        var stands = new List<KeyValuePair<int, bool>>();
        foreach (var key in _save.Keys)
        {
            if (LabelSlot.TryParseSaveKey(key, out var slot))
            {
                entries.Add(new KeyValuePair<LabelSlot, int>(slot, _save.Get(key, 0)));
            }
            else if (PlacementBoard.TryParseSaveKey(key, out var labelId))
            {
                placements.Add(new KeyValuePair<int, Placement>(labelId, (Placement)_save.Get(key, -1)));
            }
            else if (StandBoard.TryParseSaveKey(key, out var standId))
            {
                stands.Add(new KeyValuePair<int, bool>(standId, _save.Get(key, false)));
            }
        }

        _board.Apply(entries);
        _placements.Apply(placements);
        _stands.Apply(stands);
        _log?.Info($"Loaded {_board.Count} label picture(s), {_placements.Count} placement(s) and {_stands.Count} stand choice(s) from {_save.SaveName} ({_save.State}, {_save.LastRead})");
    }

    public int Store()
    {
        if (!_save.IsReady)
        {
            return 0;
        }

        var written = 0;
        foreach (var (slot, index) in _board.Entries)
        {
            if (_save.Get(slot.SaveKey, -1) != index && _save.Set(slot.SaveKey, index))
            {
                written++;
            }
        }

        foreach (var (labelId, placement) in _placements.Entries)
        {
            var key = PlacementBoard.SaveKey(labelId);
            if (_save.Get(key, -1) != (int)placement && _save.Set(key, (int)placement))
            {
                written++;
            }
        }

        foreach (var (labelId, hidden) in _stands.Entries)
        {
            var key = StandBoard.SaveKey(labelId);
            if ((!_save.Has(key) || _save.Get(key, false) != hidden) && _save.Set(key, hidden))
            {
                written++;
            }
        }

        return written;
    }
}
