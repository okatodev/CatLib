using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using ParcelBoard.Logic;
using UnityEngine;

namespace ParcelBoard.Settings;

public sealed class BoardSettings
{
    private readonly Dictionary<BoardList, Setting<ListVisibility>> _lists = new();

    public BoardSettings(CatSettings settings, Action changed)
    {
        Enabled = settings.Local("Board", "Enabled", true,
            "Shows the parcel board in a level. The key below hides and shows it too.");
        ShowKey = settings.Local("Board", "ShowKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftControl),
            "Hides or shows the whole board.");
        OpenKey = settings.Local("Board", "OpenKey", new KeyboardShortcut(KeyCode.O, KeyCode.LeftControl),
            "Opens or closes the lists: closed, only the totals show; open, every destination with its count.");
        StartOpen = settings.Local("Board", "StartOpen", false,
            "The lists are open when a level starts.");
        Corner = settings.Local("Board", "Corner", BoardCorner.TopLeft,
            "The corner of the screen for the board.");
        Scale = settings.Local("Board", "Scale", 0.9f,
            "Size of the board.", new AcceptableValueRange<float>(0.5f, 1.6f));
        Opacity = settings.Local("Board", "Opacity", 0.15f,
            "How solid the background of the lists is: 0 is fully clear, 1 is solid.", new AcceptableValueRange<float>(0f, 1f));
        RegionNames = settings.Local("Board", "RegionNames", false,
            "Writes the name of a destination next to its icon.");
        Order = settings.Local("Board", "Order", RegionOrder.ByCount,
            "Order of the destinations in an open list: the most parcels first, or the game's order. Cat's Island always comes first.");
        HideEmptyRows = settings.Local("Board", "HideEmptyRows", true,
            "Leaves out destinations and sizes with no parcels; they come back as soon as there is one.");
        Scope = settings.Local("Board", "Scope", CountScope.Everything,
            "Which parcels are counted: everything in the level, or without the parcels on the boat and at customers.");

        foreach (var list in BoardCounter.DefaultOrder)
        {
            var setting = settings.Local("Lists", list.ToString(), list == BoardList.All ? ListVisibility.Always : ListVisibility.WhenNotEmpty,
                Describe(list));
            _lists[list] = setting;
            setting.Changed += (_, _) => changed();
        }

        Enabled.Changed += (_, _) => changed();
        Corner.Changed += (_, _) => changed();
        Scale.Changed += (_, _) => changed();
        Opacity.Changed += (_, _) => changed();
        RegionNames.Changed += (_, _) => changed();
        Order.Changed += (_, _) => changed();
        HideEmptyRows.Changed += (_, _) => changed();
        Scope.Changed += (_, _) => changed();
    }

    public Setting<bool> Enabled { get; }

    public Setting<KeyboardShortcut> ShowKey { get; }

    public Setting<KeyboardShortcut> OpenKey { get; }

    public Setting<bool> StartOpen { get; }

    public Setting<BoardCorner> Corner { get; }

    public Setting<float> Scale { get; }

    public Setting<float> Opacity { get; }

    public Setting<bool> RegionNames { get; }

    public Setting<RegionOrder> Order { get; }

    public Setting<bool> HideEmptyRows { get; }

    public Setting<CountScope> Scope { get; }

    public ListVisibility Visibility(BoardList list) => _lists.TryGetValue(list, out var setting) ? setting.Value : ListVisibility.WhenNotEmpty;

    public BoardOptions Options() => new()
    {
        Visibility = Visibility,
        HideEmptyRows = HideEmptyRows.Value,
        Order = Order.Value,
        Scope = Scope.Value
    };

    private static string Describe(BoardList list) => list switch
    {
        BoardList.All => "Every parcel, by destination.",
        BoardList.NeedsStamps => "Parcels for other places that still miss a stamp: destination, weight or a mark.",
        BoardList.Damaged => "Damaged parcels, by destination.",
        BoardList.Sizes => "Every parcel, by size instead of destination.",
        _ => "Parcels with this mark, by destination."
    };
}
