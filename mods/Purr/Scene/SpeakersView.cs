using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.UI;
using Purr.Logic;
using UnityEngine;

namespace Purr.Scene;

public sealed class SpeakersView
{
    public const string ObjectName = "Purr_Speakers";
    public const float Margin = 24f;

    private CountList _list;
    private IntPtr _hudPointer;

    public bool IsShown => _list != null && _list.IsAlive && _list.Rect.gameObject.activeSelf;

    public void Show(IReadOnlyList<string> names, Sprite icon, string title, SpeakersCorner corner)
    {
        var hud = HudLayer.Root;
        if (hud == null || names == null || names.Count == 0)
        {
            Hide();
            return;
        }

        if (_list == null || !_list.IsAlive || hud.Pointer != _hudPointer)
        {
            _list?.Destroy();
            _list = CountList.Create(hud, ObjectName, new CountListStyle { PlateAlpha = 0.35f, Scale = 0.85f });
            _hudPointer = hud.Pointer;
        }

        var bottom = corner is SpeakersCorner.BottomLeft or SpeakersCorner.BottomRight;
        var right = corner is SpeakersCorner.TopRight or SpeakersCorner.BottomRight;
        _list.Rect.gameObject.SetActive(true);
        _list.Show(new CountListContent(new CountRow(icon, title, string.Empty),
            names.Select(name => new CountRow(null, name, string.Empty)).ToList(), true), bottom);
        var anchor = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
        var rect = _list.Rect;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = new Vector2(right ? -Margin : Margin, bottom ? Margin : -Margin);
    }

    public void Hide()
    {
        if (_list != null && _list.IsAlive)
        {
            _list.Rect.gameObject.SetActive(false);
        }
    }

    public void Destroy()
    {
        _list?.Destroy();
        _list = null;
    }
}
