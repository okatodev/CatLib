using System;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using UnityEngine;

namespace ShelfLabels;

public sealed class LabelSettings
{
    public LabelSettings(CatSettings settings, Action changed)
    {
        Slots = settings.Session("Labels", "ExtraSlots", 1,
            "How many extra labels each shelf label gets. Pictures of hidden labels are kept in the save and come back when they are shown again.",
            new AcceptableValueRange<int>(0, LabelSlot.MaxSlots));
        Placement = settings.Session("Labels", "DefaultPlacement", ShelfLabels.Placement.Auto,
            "Where extra labels go on shelves that have no placement of their own. Each shelf label can get its own placement with the hotkey.");
        HideStands = settings.Session("Labels", "HideStands", false,
            "Extra labels on shelves without a choice of their own are shown without the wooden stand under the frame. The game's own label always keeps it.");
        Spacing = settings.Local("Look", "Spacing", 0.01f,
            "Gap between the labels in meters.", new AcceptableValueRange<float>(0f, 0.1f));
        PlacementHotkey = settings.Local("Look", "PlacementHotkey", new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl),
            "Look at a shelf label or its extra label and press this to switch where its extra labels go. Every player can do it, the host keeps it in the save.");
        StandHotkey = settings.Local("Look", "StandHotkey", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl),
            "Look at a shelf label or its extra label and press this to show or hide the wooden stand of its extra labels. Every player can do it, the host keeps it in the save.");

        Slots.Apply(_ => changed());
        Placement.Apply(_ => changed());
        HideStands.Apply(_ => changed());
        Spacing.Apply(_ => changed());
    }

    public Setting<int> Slots { get; }

    public Setting<Placement> Placement { get; }

    public Setting<float> Spacing { get; }

    public Setting<KeyboardShortcut> PlacementHotkey { get; }

    public Setting<bool> HideStands { get; }

    public Setting<KeyboardShortcut> StandHotkey { get; }
}
