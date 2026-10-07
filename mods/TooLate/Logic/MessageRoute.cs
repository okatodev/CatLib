namespace TooLate.Logic;

public enum Route
{
    Pass,
    Drop,
    Hold,
    HoldLatest,
    Replace
}

public static class MessageRoute
{
    public static Route For(JoinStage stage, int code)
    {
        if (code == ProtocolCodes.IdentifiersSynchronization)
        {
            return Route.Replace;
        }

        if (stage is JoinStage.Synced or JoinStage.Playing || IsJoinFlow(code))
        {
            return Route.Pass;
        }

        if (stage == JoinStage.Appearing && code == ProtocolCodes.PlayerSpawnedBroadcast)
        {
            return Route.Pass;
        }

        if (IsContinuous(code) || code is ProtocolCodes.SaveSynchronization or ProtocolCodes.PlayerSpawnedBroadcast or ProtocolCodes.GenericMessage)
        {
            return Route.Drop;
        }

        if (stage == JoinStage.Waiting)
        {
            return Route.Drop;
        }

        return code == ProtocolCodes.GameTimeSynchronization ? Route.HoldLatest : Route.Hold;
    }

    public static bool IsJoinFlow(int code) => code is ProtocolCodes.ConnectAck or ProtocolCodes.ClientConnected or ProtocolCodes.ClientDisconnect
        or ProtocolCodes.ClientDisconnectBroadcast or ProtocolCodes.ServerDisconnect or ProtocolCodes.ServerGameStart
        or ProtocolCodes.ServerFinalizeLoad or ProtocolCodes.GrantPlayerSpawn
        or ProtocolCodes.PlayerName or ProtocolCodes.PlayerNameBroadcast;

    public static bool IsContinuous(int code) => code is ProtocolCodes.MovementAndRotation or ProtocolCodes.MovementAndRotationBroadcast
        or ProtocolCodes.AnimationParameter or ProtocolCodes.AnimationParameterBroadcast;
}
