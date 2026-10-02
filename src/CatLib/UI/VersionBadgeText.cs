using System.Text;
using CatLib.Game;

namespace CatLib.UI;

internal static class VersionBadgeText
{
    public const string Separator = " · ";
    public const string MarkColor = "#C0462B";

    public static string Title => "CatLib " + PluginMeta.Version;

    public static string Compose(GameBuildCheck check, bool hovered, int modCount, string languageCode)
    {
        var builder = new StringBuilder();
        var warning = Warning(check, languageCode);
        if (warning != null)
        {
            builder.Append("<color=").Append(MarkColor).Append("><b>!</b></color>  ");
        }

        builder.Append(Title);
        if (warning != null)
        {
            builder.Append("\n<size=80%>").Append(warning).Append("</size>");
        }

        if (hovered)
        {
            builder.Append("\n<size=70%>").Append(Detail(check, modCount, languageCode)).Append("</size>");
        }

        return builder.ToString();
    }

    public static string Warning(GameBuildCheck check, string languageCode) => check?.Status switch
    {
        GameBuildStatus.GameNewer => UiText.Get(UiText.BuildNewer, languageCode),
        GameBuildStatus.GameOlder => UiText.Get(UiText.BuildOlder, languageCode),
        _ => null
    };

    public static string Detail(GameBuildCheck check, int modCount, string languageCode)
    {
        var mods = UiText.Plural(UiText.LobbyMods, modCount, languageCode);
        switch (check?.Status)
        {
            case GameBuildStatus.Supported:
                return UiText.Format(UiText.BuildMatches, languageCode, Build(check.Running, languageCode)) + Separator + mods;
            case GameBuildStatus.GameNewer:
            case GameBuildStatus.GameOlder:
                return UiText.Format(UiText.BuildCompare, languageCode, Build(check.Target, languageCode), Build(check.Running, languageCode));
            default:
                return UiText.Get(UiText.BuildUnknown, languageCode) + Separator + mods;
        }
    }

    public static string Build(GameBuild build, string languageCode)
    {
        if (build == null)
        {
            return "?";
        }

        var number = build.HasSteamBuild ? UiText.Format(UiText.BuildNumber, languageCode, build.SteamBuild) : null;
        if (!build.HasStamp)
        {
            return number ?? (string.IsNullOrEmpty(build.Version) ? "?" : build.Version);
        }

        return number == null ? build.BuiltOn : build.BuiltOn + " (" + number + ")";
    }
}
