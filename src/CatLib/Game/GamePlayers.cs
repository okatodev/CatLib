using System;

namespace CatLib.Game;

public static class GamePlayers
{
    public static bool TryGetGhost(ulong clientId, out PlayerGhostEntity ghost)
    {
        ghost = null;
        try
        {
            if (!Singleton<PlayerManager>.HasInstance())
            {
                return false;
            }

            var ghosts = Singleton<PlayerManager>.Instance._ghostEntities;
            if (ghosts == null || !ghosts.ContainsKey(clientId))
            {
                return false;
            }

            ghost = ghosts[clientId];
            return ghost != null;
        }
        catch (Exception)
        {
            ghost = null;
            return false;
        }
    }
}
