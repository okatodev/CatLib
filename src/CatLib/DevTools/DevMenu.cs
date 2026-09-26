using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using CatLib.Config;
using CatLib.Core;
using CatLib.Logging;
using UnityEngine;

namespace CatLib.DevTools;

public static class DevMenu
{
    public const float ReferenceHeight = 1080f;
    public const float BaseScale = 1.35f;
    public const float Width = 560f;
    public const float LineHeight = 22f;
    public const float Padding = 10f;

    private static readonly (KeyCode Key, DevKey Action)[] Keys =
    {
        (KeyCode.UpArrow, DevKey.Up), (KeyCode.DownArrow, DevKey.Down), (KeyCode.LeftArrow, DevKey.Left), (KeyCode.RightArrow, DevKey.Right),
        (KeyCode.Return, DevKey.Enter), (KeyCode.KeypadEnter, DevKey.Enter),
        (KeyCode.Alpha1, DevKey.Digit1), (KeyCode.Alpha2, DevKey.Digit2), (KeyCode.Alpha3, DevKey.Digit3), (KeyCode.Alpha4, DevKey.Digit4),
        (KeyCode.Alpha5, DevKey.Digit5), (KeyCode.Alpha6, DevKey.Digit6), (KeyCode.Alpha7, DevKey.Digit7), (KeyCode.Alpha8, DevKey.Digit8),
        (KeyCode.Alpha9, DevKey.Digit9), (KeyCode.Keypad1, DevKey.Digit1), (KeyCode.Keypad2, DevKey.Digit2), (KeyCode.Keypad3, DevKey.Digit3),
        (KeyCode.Keypad4, DevKey.Digit4), (KeyCode.Keypad5, DevKey.Digit5), (KeyCode.Keypad6, DevKey.Digit6), (KeyCode.Keypad7, DevKey.Digit7),
        (KeyCode.Keypad8, DevKey.Digit8), (KeyCode.Keypad9, DevKey.Digit9)
    };

    private static CatLogger _log;
    private static Setting<KeyboardShortcut> _hotkey;
    private static bool _reportedEmpty;
    private static bool _reportedDrawFailure;

    public static DevMenuModel Model { get; } = new();

    public static bool IsOpen { get; private set; }

    public static DevItem Command(string group, string label, Action run, string hint = null) => Model.Add(DevItem.Command(group, label, run, hint));

    public static DevItem Command(string group, string label, Func<string> run, string hint = null) => Model.Add(DevItem.Command(group, label, run, hint));

    public static DevItem Toggle(string group, string label, Func<bool> get, Action<bool> set, string hint = null) => Model.Add(DevItem.Toggle(group, label, get, set, hint));

    public static bool Remove(DevItem item) => Model.Remove(item);

    internal static void Initialize(CatLogger log, CatSettings settings)
    {
        _log = log;
        Model.Log = log;
        _hotkey = settings.Local("DevTools", "MenuHotkey", new KeyboardShortcut(KeyCode.BackQuote),
            "Opens the developer menu when a mod has registered developer commands, for example CatLib.Tests.");
        FrameLoop.Update += Update;
    }

    internal static void Update()
    {
        if (_hotkey == null)
        {
            return;
        }

        if (_hotkey.Value.IsDown())
        {
            if (!IsOpen && Model.Count == 0)
            {
                if (!_reportedEmpty)
                {
                    _reportedEmpty = true;
                    _log?.Info("The developer menu has no commands, install CatLib.Tests to get them");
                }

                return;
            }

            IsOpen = !IsOpen;
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        if (UnityInput.Current.GetKeyDown(KeyCode.Escape))
        {
            IsOpen = false;
            return;
        }

        foreach (var (key, action) in Keys)
        {
            if (UnityInput.Current.GetKeyDown(key))
            {
                Model.Press(action);
            }
        }
    }

    internal static void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        var oldMatrix = GUI.matrix;
        var oldColor = GUI.color;
        try
        {
            var scale = Mathf.Max(1f, Screen.height / ReferenceHeight * BaseScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var lines = Lines(out var selectedLine);
            var height = Padding * 2 + LineHeight * lines.Count;
            var panel = new Rect(Padding * 2, Padding * 2, Width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.Box(panel, string.Empty);
            GUI.Box(panel, string.Empty);
            for (var index = 0; index < lines.Count; index++)
            {
                var (text, color) = lines[index];
                var row = new Rect(panel.x + Padding, panel.y + Padding + index * LineHeight, Width - Padding * 2, LineHeight);
                if (index == selectedLine)
                {
                    GUI.color = new Color(1f, 0.85f, 0.35f, 0.35f);
                    GUI.Box(row, string.Empty);
                }

                GUI.color = color;
                GUI.Label(row, text);
            }
        }
        catch (Exception exception)
        {
            if (!_reportedDrawFailure)
            {
                _reportedDrawFailure = true;
                _log?.Error("Drawing the developer menu failed", exception);
            }
        }
        finally
        {
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }

    private static List<(string Text, Color Color)> Lines(out int selectedLine)
    {
        var white = Color.white;
        var grey = new Color(0.7f, 0.7f, 0.7f, 1f);
        var groups = Model.Groups;
        var items = Model.Current;
        var lines = new List<(string, Color)>
        {
            ($"CatLib developer menu    {Model.CurrentGroup}  ({Model.GroupIndex + 1}/{groups.Count})", new Color(1f, 0.85f, 0.35f, 1f)),
            (string.Join("  ·  ", groups), grey)
        };

        selectedLine = -1;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var number = index < DevMenuModel.DigitItems ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : " ";
            var state = item.State;
            if (index == Model.Selected)
            {
                selectedLine = lines.Count;
            }

            lines.Add(($"{number}   {item.Label}{(state == null ? string.Empty : "   [" + state + "]")}", white));
        }

        lines.Add((Model.SelectedItem?.Hint ?? string.Empty, grey));
        lines.Add((Model.Status ?? string.Empty, new Color(0.6f, 1f, 0.6f, 1f)));
        lines.Add(($"Up/Down select · Enter or 1-9 run · Left/Right group · {_hotkey?.Value} or Esc close", grey));
        return lines;
    }
}
