using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.DevTools;
using CatLib.Logging;
using CatLib.Net;
using StackIt.Research;

namespace StackIt;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
[BepInDependency("catlib.core")]
public sealed class StackItPlugin : BasePlugin
{
    public const string DevGroup = "Stack it!";

    public override void Load()
    {
        var log = CatLogger.From(Log);
        CatNetwork.Declare(this, SessionPolicy.ClientOnly);
        var survey = new StackSurvey(log);
        DevMenu.Command(DevGroup, "Parcel grids in the log", survey.ParcelGrids,
            "Writes the grid on top of one parcel of every size next to its shelf footprint, and the height of its top.");
        DevMenu.Command(DevGroup, "Storages in the log", survey.Storages,
            "Writes every shelf level, boat and counter with its grid, limits and the tree of parcels standing on it.");
        DevMenu.Command(DevGroup, "Level tops in the log", survey.LevelTops,
            "Writes, for every shelf level and boat, the parcels standing on it grouped by the height of their tops.");
        log.Info($"Stack it! {PluginMeta.Version} loaded, research commands only");
    }
}
