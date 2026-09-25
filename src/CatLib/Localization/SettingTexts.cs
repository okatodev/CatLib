using CatLib.Config;
using CatLib.UI;

namespace CatLib.Localization;

public static class SettingTexts
{
    public const string ModNameKey = "mod.name";

    public static string SectionKey(string section) => "section." + section;

    public static string LabelKey(ISetting setting) => "setting." + setting.Section + "." + setting.Key;

    public static string DescriptionKey(ISetting setting) => LabelKey(setting) + ".description";

    public static string EnumKey(object value) => "enum." + value.GetType().Name + "." + value;

    public static string ModName(CatSettings settings, string language) =>
        CatLocalization.Find(settings.OwnerId)?.Find(ModNameKey, language) ?? settings.DisplayName;

    public static string Section(CatSettings settings, string section, string language) =>
        CatLocalization.Find(settings.OwnerId)?.Find(SectionKey(section), language) ?? LabelFormatter.Prettify(section);

    public static string Label(ISetting setting, string language) =>
        Catalog(setting)?.Find(LabelKey(setting), language) ?? setting.MenuLabel ?? LabelFormatter.Prettify(setting.Key);

    public static string Description(ISetting setting, string language) =>
        Catalog(setting)?.Find(DescriptionKey(setting), language) ?? setting.EntryBase.Description?.Description;

    public static string EnumValue(ISetting setting, object value, string language) =>
        Catalog(setting)?.Find(EnumKey(value), language) ?? LabelFormatter.Prettify(value.ToString());

    private static TextCatalog Catalog(ISetting setting) => CatLocalization.Find(setting.Owner?.OwnerId);
}
