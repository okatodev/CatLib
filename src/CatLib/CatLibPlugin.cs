using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Core;

namespace CatLib;

[BepInPlugin(PluginMeta.Guid, PluginMeta.Name, PluginMeta.Version)]
public sealed class CatLibPlugin : BasePlugin
{
    public override void Load()
    {
        CatLibRuntime.Initialize(this);
    }
}
