using System;

namespace TooLate.Logic;

public static class PlayerLimit
{
    public const int GamePlayers = 4;
    public const int MostPlayers = 8;
    public const string CountPattern = "E8 ?? ?? ?? ?? 83 F8 05 0F 8D";
    public const int CountOffset = 7;
    public const string LoadingPattern = "44 38 A1 90 00 00 00 0F 85 ?? ?? ?? ??";
    public const int LoadingOffset = 7;
    public const int LoadingLength = 6;

    public static readonly byte[] SkipJump = { 0x66, 0x0F, 0x1F, 0x44, 0x00, 0x00 };

    public static int Clamp(int players) => Math.Max(GamePlayers, Math.Min(MostPlayers, players));

    public static byte ConnectionLimit(int players) => (byte)(Clamp(players) + 1);
}
