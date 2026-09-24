using System.Collections.Generic;
using System.Linq;

namespace CatLib.Net;

public sealed record LocalIdentity(string CatLibVersion, string GameVersion, IReadOnlyList<ModInfo> Mods)
{
    public ModInfo Find(string id) => Mods.FirstOrDefault(mod => mod.Id == id);
}
