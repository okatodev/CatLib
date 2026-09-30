using System.Linq;
using CatLib.Config;
using CatLib.Net;

namespace CatLib.UI;

internal enum ModBadgeKind
{
    None,
    Paused,
    Restart,
    Host
}

internal static class ModBadge
{
    public static ModBadgeKind Kind(bool paused, bool restartPending, bool hostSet) =>
        paused ? ModBadgeKind.Paused : restartPending ? ModBadgeKind.Restart : hostSet ? ModBadgeKind.Host : ModBadgeKind.None;

    public static ModBadgeKind For(CatSettings settings)
    {
        var declared = CatNetwork.DeclaredMods.FirstOrDefault(mod => mod.Id == settings.OwnerId);
        var paused = declared != null && declared.Policy == SessionPolicy.RequiredOnAll && !CatNetwork.IsActive(settings.OwnerId);
        var visible = settings.Settings.Where(setting => !setting.IsHiddenInMenu).ToList();
        return Kind(paused, visible.Any(setting => setting.IsRestartPending), visible.Any(setting => setting.IsOverridden));
    }

    public static string Text(ModBadgeKind kind, string language) => kind switch
    {
        ModBadgeKind.Paused => UiText.Get(UiText.BadgePaused, language),
        ModBadgeKind.Restart => UiText.Get(UiText.BadgeRestart, language),
        ModBadgeKind.Host => UiText.Get(UiText.BadgeHost, language),
        _ => string.Empty
    };
}
