using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Config;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CatLib.UI;

internal sealed class ModsPanel
{
    public const int ListRefreshIntervalFrames = 30;

    private readonly OptionsInterface _options;
    private readonly TabInterface _tab;
    private readonly RowTemplates _templates;
    private readonly RectTransform _listContent;
    private readonly RectTransform _settingsContent;
    private readonly GameObject _contentHeader;
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
    private int _refreshCountdown;

    public ModsPanel(
        OptionsInterface options,
        TabInterface tab,
        RowTemplates templates,
        RectTransform listContent,
        RectTransform settingsContent,
        GameObject contentHeader,
        float listWidth,
        float settingsWidth,
        CatLogger log)
    {
        _options = options;
        _tab = tab;
        _templates = templates;
        _listContent = listContent;
        _settingsContent = settingsContent;
        _contentHeader = contentHeader;
        _listWidth = listWidth;
        _settingsWidth = settingsWidth;
        _log = log;
        _gameDropdowns = options._dropdowns?.ToArray() ?? Array.Empty<SelectableDropdown>();
        _gameInputFields = options._inputFields?.ToArray() ?? Array.Empty<SelectableInputField>();
    }

    public RowTemplates Templates => _templates;

    public IReadOnlyList<ModListItem> Items => _items;

    public IReadOnlyList<SettingRow> Rows => _rows;

    public IReadOnlyList<GameObject> SectionHeaders => _sectionHeaders;

    public CatSettings Selected => _items.FirstOrDefault(item => item.IsSelected)?.Settings;

    public static IReadOnlyList<ISetting> VisibleSettings(CatSettings settings) =>
        settings.Settings.Where(setting => !setting.IsHiddenInMenu).ToList();

    public static string RowLabel(ISetting setting, string languageCode)
    {
        var label = setting.MenuLabel ?? LabelFormatter.Prettify(setting.Key);
        return setting.IsRestartRequired ? label + " " + UiText.Get(UiText.RestartSuffix, languageCode) : label;
    }

    public void Update()
    {
        if (--_refreshCountdown <= 0)
        {
            _refreshCountdown = ListRefreshIntervalFrames;
            RefreshList(false);
        }

        for (var index = 0; index < _rows.Count; index++)
        {
            try
            {
                _rows[index].Update();
            }
            catch (Exception exception)
            {
                _log.Error($"Updating the row of {_rows[index].Setting.Id} failed", exception);
            }
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

    public void SetHeaderText(GameObject header, string text, float maxWidth) =>
        RowGeometry.SetTapeText(header, text, _templates.TapeWidth, maxWidth);

    public float ListWidth => _listWidth;

    public void CommitPending()
    {
        foreach (var row in _rows)
        {
            row.Commit();
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

        var languageCode = UiText.LanguageCode;
        if (settings == null)
        {
            SetHeaderText(_contentHeader, UiText.Get(UiText.SelectMod, languageCode), _settingsWidth);
            RegisterControls();
            return;
        }

        SetHeaderText(_contentHeader, string.IsNullOrEmpty(settings.Version) ? settings.DisplayName : settings.DisplayName + " " + settings.Version, _settingsWidth);

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
        if (presentation.Kind == ControlKind.Slider)
        {
            if (!RowGeometry.FitSliderBackground(root, _templates.TemplateRowWidth, _settingsWidth))
            {
                _log.Warning($"Could not size the slider track of {setting.Id}, the template row width is unknown");
            }
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
}
