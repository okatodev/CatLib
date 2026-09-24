using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CatLib.Logging;
using CatLib.Tests.Framework;
using I2.Loc;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CatLib.Tests.Diagnostics;

public sealed class UiHierarchyDumper
{
    private const int MaxNodes = 20000;
    private const int MaxTextLength = 120;

    private readonly string _directory;
    private readonly CatLogger _log;

    public UiHierarchyDumper(string directory, CatLogger log)
    {
        _directory = directory;
        _log = log;
    }

    public void DumpSettingsMenus()
    {
        var dumped = 0;

        if (Singleton<MainMenuInterfacesManager>.HasInstance())
        {
            var settings = Singleton<MainMenuInterfacesManager>.Instance.SettingsMenuInterface;
            if (settings != null)
            {
                Dump("MainMenu", settings.gameObject, settings.TryCast<OptionsInterface>());
                dumped++;
            }
        }

        if (Singleton<InterfaceManager>.HasInstance())
        {
            var settings = Singleton<InterfaceManager>.Instance.SettingsInterface;
            if (settings != null)
            {
                Dump("InGame", settings.gameObject, settings);
                dumped++;
            }
        }

        if (dumped == 0)
        {
            _log.Warning("No settings menu found. Open the settings menu in the main menu or from the pause menu and try again");
        }
    }

    private void Dump(string context, GameObject root, OptionsInterface options)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"CatLib UI dump: {context}");
        builder.AppendLine("Created: " + InvariantFormat.Timestamp(DateTime.Now));
        builder.AppendLine("Root: " + PathOf(root.transform));
        builder.AppendLine("Root active in hierarchy: " + root.activeInHierarchy);
        builder.AppendLine();

        Section(builder, "Tabs", () => AppendTabs(builder, options));
        Section(builder, "Gameplay templates", () => AppendGameplayTemplates(builder, root));
        Section(builder, "Hierarchy", () => AppendHierarchy(builder, root.transform));

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "ui_" + context + "_" + InvariantFormat.FileStamp(DateTime.Now) + ".txt");
        File.WriteAllText(path, builder.ToString());
        _log.Message($"UI dump of the {context} settings menu written to {path}");
    }

    private void Section(StringBuilder builder, string title, Action append)
    {
        builder.AppendLine("== " + title);
        try
        {
            append();
        }
        catch (Exception exception)
        {
            builder.AppendLine("!! Section failed: " + exception);
            _log.Warning($"UI dump section {title} failed: {exception.Message}");
        }

        builder.AppendLine();
    }

    private static void AppendTabs(StringBuilder builder, OptionsInterface options)
    {
        if (options == null)
        {
            builder.AppendLine("Root is not an OptionsInterface");
            return;
        }

        builder.AppendLine("TabsParent: " + PathOf(options.TabsParent));
        var tabs = options._tabs;
        if (tabs == null)
        {
            builder.AppendLine("_tabs is null, the menu was probably never opened");
            return;
        }

        builder.AppendLine("Tab count: " + tabs.Count);
        for (var index = 0; index < tabs.Count; index++)
        {
            var tab = tabs[index];
            builder.AppendLine($"[{index}] {PathOf(tab.transform)}");
            builder.AppendLine("    button: " + PathOf(tab.AssociatedButton?.transform));
            builder.AppendLine("    panel: " + PathOf(tab.AssociatedPanel?.transform));
            builder.AppendLine("    scroll: " + PathOf(tab.ContentScrollRect?.transform));
            builder.AppendLine("    content: " + PathOf(tab.ContentScrollRect?.content));
            builder.AppendLine("    first selected: " + PathOf(tab.FirstSelected?.transform));
        }

        builder.AppendLine("Selected tab: " + PathOf(options._selectedTab?.transform));
    }

    private static void AppendGameplayTemplates(StringBuilder builder, GameObject root)
    {
        var interfaces = root.GetComponentsInChildren<GameplaySettingsInterface>(true);
        if (interfaces.Length == 0)
        {
            builder.AppendLine("No GameplaySettingsInterface under the root");
            return;
        }

        var gameplay = interfaces[0];
        builder.AppendLine("GameplaySettingsInterface: " + PathOf(gameplay.transform));
        builder.AppendLine("FieldOfViewSlider: " + PathOf(gameplay.FieldOfViewSlider?.transform));
        builder.AppendLine("FieldOfViewValueText: " + PathOf(gameplay.FieldOfViewValueText?.transform));
        builder.AppendLine("ZoomInMultiplierSlider: " + PathOf(gameplay.ZoomInMultiplierSlider?.transform));
        builder.AppendLine("ZoomInAnimationToggle: " + PathOf(gameplay.ZoomInAnimationToggle?.transform));
        builder.AppendLine("VSyncToggle: " + PathOf(gameplay.VSyncToggle?.transform));
        builder.AppendLine("ShadowsToggle: " + PathOf(gameplay.ShadowsToggle?.transform));
        builder.AppendLine("FullScreenDropdown: " + PathOf(gameplay.FullScreenDropdown?.transform));
        builder.AppendLine("LanguageDropdown: " + PathOf(gameplay.LanguageDropdown?.transform));
        builder.AppendLine("PlayerNameField: " + PathOf(gameplay.PlayerNameField?.transform));
        builder.AppendLine("BackToDefaultButton: " + PathOf(gameplay.BackToDefaultButton?.transform));
    }

    private static void AppendHierarchy(StringBuilder builder, Transform root)
    {
        var nodes = 0;
        var stack = new Stack<(Transform Node, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            if (++nodes > MaxNodes)
            {
                builder.AppendLine($"!! Stopped after {MaxNodes} nodes");
                return;
            }

            var indent = new string(' ', depth * 2);
            var gameObject = node.gameObject;
            builder.AppendLine($"{indent}{(gameObject.activeSelf ? "+" : "-")} {gameObject.name}");

            foreach (var component in gameObject.GetComponents(Il2CppType.Of<Component>()))
            {
                if (component == null)
                {
                    builder.AppendLine($"{indent}    <missing component>");
                    continue;
                }

                builder.AppendLine($"{indent}    {Describe(component)}");
            }

            for (var index = node.childCount - 1; index >= 0; index--)
            {
                stack.Push((node.GetChild(index), depth + 1));
            }
        }
    }

    private static string Describe(Component component)
    {
        string typeName;
        try
        {
            typeName = component.GetIl2CppType().FullName;
        }
        catch (Exception exception)
        {
            return "<type unavailable: " + exception.Message + ">";
        }

        try
        {
            return typeName + Details(component);
        }
        catch (Exception exception)
        {
            return typeName + " <details failed: " + exception.Message + ">";
        }
    }

    private static string Details(Component component)
    {
        var rect = component.TryCast<RectTransform>();
        if (rect != null)
        {
            var rotation = rect.localEulerAngles;
            var scale = rect.localScale;
            var transformText = (rotation.x != 0f || rotation.y != 0f || rotation.z != 0f ? $" rot=({Number(rotation.x)},{Number(rotation.y)},{Number(rotation.z)})" : string.Empty) +
                                (scale.x != 1f || scale.y != 1f || scale.z != 1f ? $" scale=({Number(scale.x)},{Number(scale.y)},{Number(scale.z)})" : string.Empty);
            return $" size={Vector(rect.sizeDelta)} pos={Vector(rect.anchoredPosition)} anchors={Vector(rect.anchorMin)}-{Vector(rect.anchorMax)} pivot={Vector(rect.pivot)}{transformText}";
        }

        var text = component.TryCast<TMP_Text>();
        if (text != null)
        {
            return $" text=\"{Clip(text.text)}\" fontSize={Number(text.fontSize)}";
        }

        var localize = component.TryCast<Localize>();
        if (localize != null)
        {
            return $" term=\"{localize.Term}\" secondary=\"{localize.SecondaryTerm}\"";
        }

        var slider = component.TryCast<Slider>();
        if (slider != null)
        {
            return $" min={Number(slider.minValue)} max={Number(slider.maxValue)} value={Number(slider.value)} whole={slider.wholeNumbers}{Selectable(slider)}{Listeners("onValueChanged", slider.onValueChanged)}";
        }

        var toggle = component.TryCast<Toggle>();
        if (toggle != null)
        {
            return $" isOn={toggle.isOn}{Selectable(toggle)}{Listeners("onValueChanged", toggle.onValueChanged)}";
        }

        var dropdown = component.TryCast<TMP_Dropdown>();
        if (dropdown != null)
        {
            return $" value={dropdown.value} options={dropdown.options?.Count ?? 0}{Selectable(dropdown)}{Listeners("onValueChanged", dropdown.onValueChanged)}";
        }

        var input = component.TryCast<TMP_InputField>();
        if (input != null)
        {
            return $" text=\"{Clip(input.text)}\" contentType={input.contentType}{Selectable(input)}" +
                   Listeners("onValueChanged", input.onValueChanged) + Listeners("onEndEdit", input.onEndEdit) + Listeners("onSubmit", input.onSubmit);
        }

        var button = component.TryCast<Button>();
        if (button != null)
        {
            return Selectable(button) + Listeners("onClick", button.onClick);
        }

        var layout = component.TryCast<LayoutElement>();
        if (layout != null)
        {
            return $" ignore={layout.ignoreLayout} minW={Number(layout.minWidth)} prefW={Number(layout.preferredWidth)} minH={Number(layout.minHeight)} prefH={Number(layout.preferredHeight)} flexH={Number(layout.flexibleHeight)}";
        }

        return string.Empty;
    }

    private static string Selectable(Selectable selectable) => " interactable=" + selectable.interactable;

    private static string Listeners(string eventName, UnityEventBase unityEvent)
    {
        if (unityEvent == null)
        {
            return $" {eventName}=null";
        }

        var count = unityEvent.GetPersistentEventCount();
        if (count == 0)
        {
            return $" {eventName}=[]";
        }

        var listeners = new List<string>();
        for (var index = 0; index < count; index++)
        {
            var target = unityEvent.GetPersistentTarget(index);
            var targetName = target == null ? "null" : target.GetIl2CppType().FullName + "(" + target.name + ")";
            listeners.Add(targetName + "." + unityEvent.GetPersistentMethodName(index));
        }

        return $" {eventName}=[{string.Join(", ", listeners)}]";
    }

    private static string PathOf(Transform transform)
    {
        if (transform == null)
        {
            return "null";
        }

        var parts = new List<string>();
        for (var current = transform; current != null; current = current.parent)
        {
            parts.Add(current.gameObject.name);
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Clip(string value)
    {
        if (value == null)
        {
            return "null";
        }

        var flat = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return flat.Length <= MaxTextLength ? flat : flat.Substring(0, MaxTextLength) + "...";
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Vector(Vector2 value) => "(" + Number(value.x) + "," + Number(value.y) + ")";
}
