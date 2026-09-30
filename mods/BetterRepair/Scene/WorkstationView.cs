using System;
using System.Collections.Generic;
using BetterRepair.Logic;
using UnityEngine;

namespace BetterRepair.Scene;

public sealed class WorkstationView
{
    private readonly EntityRepairWorkstation _workstation;

    public WorkstationView(EntityRepairWorkstation workstation)
    {
        _workstation = workstation;
        Pointer = workstation.Pointer;
        Name = workstation.gameObject.name;
    }

    public IntPtr Pointer { get; }

    public string Name { get; }

    public int AppliedIndex { get; private set; } = -1;

    public bool IsAlive => _workstation != null;

    public int Capacity
    {
        get
        {
            var buttons = _workstation.ActivationButtons;
            return buttons == null ? CardboardStock.SheetsOnTable : buttons.Length;
        }
    }

    public int GameIndex => _workstation._currentRepairIndex;

    public void Apply(int index)
    {
        var capacity = Capacity;
        var clamped = Math.Max(0, Math.Min(capacity, index));
        _workstation._currentRepairIndex = clamped;
        var buttons = _workstation.ActivationButtons;
        if (buttons != null)
        {
            for (var slot = 0; slot < buttons.Length; slot++)
            {
                var button = buttons[slot];
                if (button != null)
                {
                    var active = slot >= clamped;
                    if (button.gameObject.activeSelf != active)
                    {
                        button.gameObject.SetActive(active);
                    }
                }
            }
        }

        AppliedIndex = clamped;
    }

    public string DescribeButtons()
    {
        var buttons = _workstation.ActivationButtons;
        if (buttons == null)
        {
            return "no buttons";
        }

        var parts = new List<string>();
        for (var slot = 0; slot < buttons.Length; slot++)
        {
            var button = buttons[slot];
            parts.Add(button == null ? "missing" : $"{button.gameObject.name} {(button.gameObject.activeSelf ? "shown" : "hidden")}");
        }

        return string.Join(", ", parts);
    }

    public static List<EntityRepairWorkstation> FindAll()
    {
        var found = new List<EntityRepairWorkstation>();
        foreach (var workstation in UnityEngine.Object.FindObjectsOfType<EntityRepairWorkstation>(true))
        {
            if (workstation != null)
            {
                found.Add(workstation);
            }
        }

        return found;
    }
}
