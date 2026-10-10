using CatLib.Config;
using CatLib.UI;

namespace CatLib.Localization;

public static class SettingTexts
{
    public const string ModNameKey = "mod.name";
    public const string ModDescriptionKey = "mod.description";

    public static string SectionKey(string section) => "section." + section;

    public static string LabelKey(ISetting setting) => "setting." + setting.Section + "." + setting.Key;

    public static string DescriptionKey(ISetting setting) => LabelKey(setting) + ".description";

    public static string EnumKey(object value) => "enum." + value.GetType().Name + "." + value;

    public static string ModName(CatSettings settings, string language) =>
        CatLocalization.Find(settings.OwnerId)?.Find(ModNameKey, language) ?? settings.DisplayName;

    public static string ModDescription(CatSettings settings, string language) =>
        CatLocalization.Find(settings.OwnerId)?.Find(ModDescriptionKey, language) ?? settings.Description ?? string.Empty;

    public static string Section(CatSettings settings, string section, string language) =>
        CatLocalization.Find(settings.OwnerId)?.Find(SectionKey(section), language) ?? LabelFormatter.Prettify(section);

    public static string Label(ISetting setting, string language) =>
        Catalog(setting)?.Find(LabelKey(setting), language) ?? setting.MenuLabel ?? LabelFormatter.Prettify(setting.Key);

    public static string Description(ISetting setting, string language) =>
        Catalog(setting)?.Find(DescriptionKey(setting), language) ?? setting.EntryBase.Description?.Description;

    public static string EnumValue(ISetting setting, object value, string language) =>
        Catalog(setting)?.Find(EnumKey(value), language) ?? LabelFormatter.Prettify(value.ToString());

    public static string ItemKey(MenuItem item) => "setting." + item.Section + "." + item.Key;

    public static string ItemLabel(MenuItem item, string language) =>
        CatLocalization.Find(item.Owner?.OwnerId)?.Find(ItemKey(item), language) ?? item.Label?.Invoke(language) ?? LabelFormatter.Prettify(item.Key);

    public static string ItemDescription(MenuItem item, string language) =>
        CatLocalization.Find(item.Owner?.OwnerId)?.Find(ItemKey(item) + ".description", language) ?? item.Hint?.Invoke(language) ?? item.Description;

    public static string GalleryEmpty(MenuGallery gallery, string language) =>
        CatLocalization.Find(gallery.Owner?.OwnerId)?.Find(ItemKey(gallery) + ".empty", language) ?? gallery.EmptyText ?? string.Empty;

    public static string ButtonCaption(MenuButton button, string language) =>
        CatLocalization.Find(button.Owner?.OwnerId)?.Find(ItemKey(button) + ".button", language) ?? button.Caption?.Invoke(language) ?? UiText.Get(UiText.ButtonRun, language);

    private static TextCatalog Catalog(ISetting setting) => CatLocalization.Find(setting.Owner?.OwnerId);
}
