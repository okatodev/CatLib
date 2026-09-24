using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using CatLib.Config;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatLib.UI;

internal sealed class ModsPanel
{
    public const int ListRefreshIntervalFrames = 30;
    public const int CardRefreshIntervalFrames = 15;
    public const int ContextRefreshIntervalFrames = 30;

    private readonly OptionsInterface _options;
    private readonly TabInterface _tab;
    private readonly RowTemplates _templates;
    private readonly RectTransform _listContent;
    private readonly RectTransform _settingsContent;
    private readonly RectTransform _settingsViewport;
    private readonly Canvas _canvas;
    private readonly float _listWidth;
    private readonly float _settingsWidth;
    private readonly CatLogger _log;
    private readonly SelectableDropdown[] _gameDropdowns;
    private readonly SelectableInputField[] _gameInputFields;
    private readonly List<ModListItem> _items = new();
    private readonly List<SettingRow> _rows = new();
    private readonly List<GameObject> _sectionHeaders = new();
    private string _listSignature = string.Empty;
    private string _selectedOwnerId;
    private int _listCountdown;
    private int _cardCountdown;
    private int _contextCountdown;
    private SettingRow _contextRow;
    private bool _contextShown;
    private bool _pointerMode;
    private Vector3 _lastMouse;
    private IntPtr _lastSelected;

    public ModsPanel(
        OptionsInterface options,
        TabInterface tab,
        RowTemplates templates,
        ScrollRect listScroll,
        ScrollRect settingsScroll,
        TMP_Text contextText,
        TMP_Text statusText,
        Button resetButton,
        float listWidth,
        float settingsWidth,
        CatLogger log)
    {
        _options = options;
        _tab = tab;
        _templates = templates;
        _listContent = listScroll.content;
        _settingsContent = settingsScroll.content;
        _settingsViewport = settingsScroll.viewport ?? settingsScroll.transform.TryCast<RectTransform>();
        _canvas = options.GetComponent<Canvas>();
        _listWidth = listWidth;
        _settingsWidth = settingsWidth;
        _log = log;
        ContextLabel = contextText;
        StatusLabel = statusText;
        ResetButton = resetButton;
        _gameDropdowns = options._dropdowns?.ToArray() ?? Array.Empty<SelectableDropdown>();
        _gameInputFields = options._inputFields?.ToArray() ?? Array.Empty<SelectableInputField>();
        Card = new ModCard(templates, _settingsContent, settingsWidth);

        if (resetButton != null)
        {
            UiEvents.Listen(resetButton.onClick, OnResetClicked);
        }
    }

    public RowTemplates Templates => _templates;

    public ModCard Card { get; }

    public TMP_Text ContextLabel { get; }

    public TMP_Text StatusLabel { get; }

    public Button ResetButton { get; }

    public float ListWidth => _listWidth;

    public IReadOnlyList<ModListItem> Items => _items;

    public IReadOnlyList<SettingRow> Rows => _rows;

    public IReadOnlyList<GameObject> SectionHeaders => _sectionHeaders;

    public SettingRow ContextRow => _contextRow;

    public CatSettings Selected => _items.FirstOrDefault(item => item.IsSelected)?.Settings;

    public static IReadOnlyList<ISetting> VisibleSettings(CatSettings settings) =>
        settings.Settings.Where(setting => !setting.IsHiddenInMenu).ToList();

    public static string RowLabel(ISetting setting, string languageCode)
    {
        var label = setting.MenuLabel ?? LabelFormatter.Prettify(setting.Key);
        if (setting.IsOverridden)
        {
            label += " " + UiText.Get(UiText.HostSuffix, languageCode);
        }

        return setting.IsRestartRequired ? label + " " + UiText.Get(UiText.RestartSuffix, languageCode) : label;
    }

    public void Update()
    {
        if (--_listCountdown <= 0)
        {
            _listCountdown = ListRefreshIntervalFrames;
            RefreshList(false);
        }

        var languageCode = _rows.Count > 0 ? UiText.LanguageCode : null;
        for (var index = 0; index < _rows.Count; index++)
        {
            try
            {
                _rows[index].SyncOverride(languageCode);
                _rows[index].Update();
            }
            catch (Exception exception)
            {
                _log.Error($"Updating the row of {_rows[index].Setting.Id} failed", exception);
            }
        }

        if (--_cardCountdown <= 0)
        {
            _cardCountdown = CardRefreshIntervalFrames;
            Card.Show(Selected, UiText.LanguageCode);
        }

        UpdateStatus();

        if (_settingsContent.gameObject.activeInHierarchy)
        {
            UpdateContext();
        }
    }

    public void RefreshList(bool force)
    {
        var mods = CatConfig.All
            .Where(settings => !settings.IsDisposed && VisibleSettings(settings).Count > 0)
            .OrderBy(settings => settings.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(settings => settings.OwnerId, StringComparer.Ordinal)
            .ToList();

        var signature = string.Join("|", mods.Select(settings => settings.OwnerId + ":" + VisibleSettings(settings).Count));
        if (!force && signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;

        foreach (var item in _items)
        {
            UnityEngine.Object.DestroyImmediate(item.Root);
        }

        _items.Clear();

        foreach (var settings in mods)
        {
            var root = RowTemplates.Create(_templates.ListItem, _listContent, "item_CatLibMod " + settings.OwnerId);
            RowSizer.Fit(root, _listWidth, RowTemplates.ListItemHeight);
            _items.Add(new ModListItem(settings, root, OnItemSelected));
        }

        var selected = _items.FirstOrDefault(item => item.Settings.OwnerId == _selectedOwnerId) ?? _items.FirstOrDefault();
        Select(selected?.Settings);
    }

    public void Select(CatSettings settings)
    {
        foreach (var item in _items)
        {
            item.SetSelected(settings != null && ReferenceEquals(item.Settings, settings));
        }

        _selectedOwnerId = settings?.OwnerId;
        RebuildRows(settings);
    }

    public void RebuildRows() => RebuildRows(Selected);

    public void CommitPending()
    {
        foreach (var row in _rows)
        {
            row.Commit();
        }
    }

    public void SetHeaderText(GameObject header, string text, float maxWidth) =>
        RowGeometry.SetTapeText(header, text, _templates.TapeWidth, maxWidth);

    public int ResetMod(CatSettings settings)
    {
        if (settings == null)
        {
            return 0;
        }

        foreach (var row in _rows)
        {
            row.Discard();
        }

        var changed = settings.ResetToDefaults(setting => !setting.IsHiddenInMenu);
        PlayerMessages.Post(UiText.Format(UiText.MessageReset, UiText.LanguageCode, settings.DisplayName));
        _cardCountdown = 0;
        _contextCountdown = 0;
        return changed;
    }

    private void OnResetClicked()
    {
        var settings = Selected;
        if (settings == null)
        {
            return;
        }

        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == null && ResetButton != null)
        {
            eventSystem.SetSelectedGameObject(ResetButton.gameObject);
        }

        try
        {
            _options.ShowConfirmRevertPopUp(UiEvents.ToIl2Cpp(() =>
            {
                try
                {
                    ResetMod(settings);
                }
                catch (Exception exception)
                {
                    _log.Error($"Resetting {settings.OwnerId} failed", exception);
                }
            }));
        }
        catch (Exception exception)
        {
            _log.Error($"Could not open the reset confirmation for {settings.OwnerId}", exception);
        }
    }

    private void OnItemSelected(ModListItem item)
    {
        if (item.Settings.OwnerId != _selectedOwnerId)
        {
            Select(item.Settings);
        }
        else
        {
            item.SetSelected(true);
        }
    }

    private void RebuildRows(CatSettings settings)
    {
        CommitPending();

        foreach (var row in _rows)
        {
            UnityEngine.Object.DestroyImmediate(row.Root);
        }

        foreach (var header in _sectionHeaders)
        {
            UnityEngine.Object.DestroyImmediate(header);
        }

        _rows.Clear();
        _sectionHeaders.Clear();
        _contextRow = null;
        _contextCountdown = 0;

        var languageCode = UiText.LanguageCode;
        Card.Show(settings, languageCode);

        if (settings != null)
        {
            foreach (var section in VisibleSettings(settings).GroupBy(setting => setting.Section))
            {
                var header = RowTemplates.Create(_templates.Header, _settingsContent, "group_SettingsHeader " + section.Key);
                SetHeaderText(header, LabelFormatter.Prettify(section.Key), _settingsWidth);
                _sectionHeaders.Add(header);

                foreach (var setting in section)
                {
                    try
                    {
                        _rows.Add(CreateRow(setting, languageCode));
                    }
                    catch (Exception exception)
                    {
                        _log.Error($"Could not create a menu row for {setting.Id}", exception);
                    }
                }
            }
        }

        RegisterControls();
    }

    private SettingRow CreateRow(ISetting setting, string languageCode)
    {
        var presentation = SettingPresentation.For(setting.ValueType, setting.EntryBase.Description?.AcceptableValues);
        var template = presentation.Kind switch
        {
            ControlKind.Toggle => _templates.Toggle,
            ControlKind.Slider => _templates.Slider,
            ControlKind.Dropdown => _templates.Dropdown,
            _ => _templates.Text
        };

        var root = RowTemplates.Create(template, _settingsContent, "group_SettingEntry_CatLib " + setting.Key);
        RowSizer.Fit(root, _settingsWidth, RowTemplates.RowHeight);
        RowGeometry.FitDash(root);
        if (presentation.Kind == ControlKind.Slider && !RowGeometry.FitSliderBackground(root, _templates.TemplateRowWidth, _settingsWidth))
        {
            _log.Warning($"Could not size the slider track of {setting.Id}, the template row width is unknown");
        }

        SettingRow row = presentation.Kind switch
        {
            ControlKind.Toggle => new ToggleRow(setting, presentation, root, _log),
            ControlKind.Slider => new SliderRow(setting, presentation, root, _log),
            ControlKind.Dropdown => new DropdownRow(setting, presentation, root, _log),
            _ => new TextRow(setting, presentation, root, _log)
        };

        row.SetLabel(RowLabel(setting, languageCode));
        return row;
    }

    private void RegisterControls()
    {
        var dropdowns = _rows.OfType<DropdownRow>()
            .Select(row => row.Dropdown.TryCast<SelectableDropdown>())
            .Where(dropdown => dropdown != null)
            .ToArray();
        var inputFields = _rows.OfType<TextRow>()
            .Select(row => row.Input.TryCast<SelectableInputField>())
            .Where(input => input != null)
            .ToArray();

        _options._dropdowns = new Il2CppReferenceArray<SelectableDropdown>(_gameDropdowns.Concat(dropdowns).ToArray());
        _options._inputFields = new Il2CppReferenceArray<SelectableInputField>(_gameInputFields.Concat(inputFields).ToArray());
        _tab._dropdowns = new Il2CppReferenceArray<SelectableDropdown>(dropdowns);
    }

    private void UpdateStatus()
    {
        if (StatusLabel == null)
        {
            return;
        }

        var text = PlayerMessages.IsFresh ? PlayerMessages.Last : string.Empty;
        if (StatusLabel.text != text)
        {
            StatusLabel.text = text;
        }
    }

    private void UpdateContext()
    {
        if (ContextLabel == null)
        {
            return;
        }

        var row = FindFocusedRow();
        var changed = !ReferenceEquals(row, _contextRow) || !_contextShown;
        if (!changed && --_contextCountdown > 0)
        {
            return;
        }

        _contextCountdown = ContextRefreshIntervalFrames;
        _contextRow = row;
        _contextShown = true;
        var languageCode = UiText.LanguageCode;
        var text = row == null ? UiText.Get(UiText.HoverHint, languageCode) : ContextText.For(row.Setting, languageCode);
        if (ContextLabel.text != text)
        {
            ContextLabel.text = text;
        }
    }

    private SettingRow FindFocusedRow()
    {
        var input = UnityInput.Current;
        var mouse = input.mousePresent ? input.mousePosition : _lastMouse;
        if (input.mousePresent && (mouse - _lastMouse).sqrMagnitude > 0.25f)
        {
            _pointerMode = true;
            _lastMouse = mouse;
        }

        var eventSystem = EventSystem.current;
        var selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        var selectedPointer = selected == null ? IntPtr.Zero : selected.Pointer;
        if (selectedPointer != _lastSelected)
        {
            _lastSelected = selectedPointer;
            _pointerMode = false;
        }

        if (_pointerMode)
        {
            var camera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var point = new Vector2(mouse.x, mouse.y);
            if (!RectTransformUtility.RectangleContainsScreenPoint(_settingsViewport, point, camera))
            {
                return null;
            }

            foreach (var row in _rows)
            {
                var rect = row.Root.transform.TryCast<RectTransform>();
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera))
                {
                    return row;
                }
            }

            return null;
        }

        if (selected == null)
        {
            return null;
        }

        foreach (var row in _rows)
        {
            if (selected.transform.IsChildOf(row.Root.transform))
            {
                return row;
            }
        }

        return null;
    }
}
