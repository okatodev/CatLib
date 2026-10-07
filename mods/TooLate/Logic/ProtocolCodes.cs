namespace TooLate.Logic;

public static class ProtocolCodes
{
    public const int ConnectAck = 3;
    public const int ClientConnected = 4;
    public const int ClientDisconnect = 5;
    public const int ClientDisconnectBroadcast = 6;
    public const int ServerDisconnect = 9;
    public const int ServerGameStart = 10;
    public const int ServerFinalizeLoad = 11;
    public const int GrantPlayerSpawn = 13;
    public const int EntityDisposed = 18;
    public const int PlayerSpawnedBroadcast = 20;
    public const int IdentifiersSynchronization = 21;
    public const int MovementAndRotation = 22;
    public const int MovementAndRotationBroadcast = 23;
    public const int AnimationParameter = 26;
    public const int AnimationParameterBroadcast = 27;
    public const int PlayerName = 28;
    public const int PlayerNameBroadcast = 29;
    public const int GenericMessage = 38;
    public const int GameTimeSynchronization = 44;
    public const int GameTimePeriod = 45;
    public const int SetInteractionsLocked = 48;
    public const int SetParcelDamaged = 49;
    public const int BoatDestinations = 50;
    public const int SaveSynchronization = 51;
}
