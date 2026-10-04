namespace CatLib.UI;

internal static class ModListMeta
{
    public const string Separator = " · ";
    public const string MarkColor = "AD6E1A";

    public static string Text(string version, int settingsCount, ModBadgeKind kind, string language, bool colored = true)
    {
        var head = string.IsNullOrWhiteSpace(version) ? string.Empty : UiText.Format(UiText.Version, language, version) + Separator;
        if (kind == ModBadgeKind.None)
        {
            return head + UiText.Plural(UiText.SettingsCount, settingsCount, language);
        }

        var mark = ModBadge.Text(kind, language);
        return colored && kind != ModBadgeKind.Host ? head + "<color=#" + MarkColor + ">" + mark + "</color>" : head + mark;
    }
}
