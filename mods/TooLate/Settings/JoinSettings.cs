using System;
using BepInEx.Configuration;
using CatLib.Config;
using TooLate.Logic;

namespace TooLate.Settings;

public sealed class JoinSettings
{
    public JoinSettings(CatSettings settings, Action changed)
    {
        Enabled = settings.Local("Join", "Enabled", true,
            "Players can join while you are already playing. They get the warehouse as it is right now. Only the host needs the mod.");
        MaxPlayers = settings.Local("Join", "MaxPlayers", PlayerLimit.GamePlayers,
            "How many players fit in your game, you included. 4 is the game's limit. Above 4 the lobby shows the extra players as a count, and the counters stay four.",
            new AcceptableValueRange<int>(PlayerLimit.GamePlayers, PlayerLimit.MostPlayers));
        Messages = settings.Local("Messages", "Enabled", true,
            "Tells you when a player starts joining, waits for the end of the day's results and is in the game. Only for you.");
        Enabled.Changed += (_, _) => changed?.Invoke();
        MaxPlayers.Changed += (_, _) => changed?.Invoke();
    }

    public Setting<bool> Enabled { get; }

    public Setting<int> MaxPlayers { get; }

    public Setting<bool> Messages { get; }
}
