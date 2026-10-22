using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Game;
using CatLib.Logging;
using TMPro;
using UnityEngine;

namespace Purr.Scene;

public sealed class NameTags
{
    public static readonly Color Talking = new(0.55f, 0.95f, 0.55f, 1f);

    private readonly Dictionary<ulong, (TMP_Text Text, Color Original)> _colored = new();
    private readonly CatLogger _log;
    private bool _reported;

    public NameTags(CatLogger log)
    {
        _log = log;
    }

    public void Show(IReadOnlyCollection<ulong> speaking)
    {
        foreach (var id in _colored.Keys.Where(id => !speaking.Contains(id)).ToList())
        {
            Restore(id);
        }

        foreach (var id in speaking)
        {
            if (_colored.TryGetValue(id, out var known) && known.Text != null && !known.Text.WasCollected)
            {
                continue;
            }

            var text = NameOf(id);
            if (text == null)
            {
                continue;
            }

            _colored[id] = (text, text.color);
            text.color = Talking;
        }
    }

    public void Clear()
    {
        foreach (var id in _colored.Keys.ToList())
        {
            Restore(id);
        }
    }

    private void Restore(ulong id)
    {
        if (_colored.Remove(id, out var entry))
        {
            try
            {
                if (entry.Text != null && !entry.Text.WasCollected)
                {
                    entry.Text.color = entry.Original;
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private TMP_Text NameOf(ulong id)
    {
        try
        {
            if (!GamePlayers.TryGetGhost(id, out var ghost))
            {
                return null;
            }

            return ghost.NameText;
        }
        catch (Exception exception)
        {
            if (!_reported)
            {
                _reported = true;
                _log.Warning($"Reading the name over a cat failed: {exception.Message}");
            }

            return null;
        }
    }
}
