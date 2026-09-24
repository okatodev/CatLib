namespace CatLib.Net;

public enum ProblemKind : byte
{
    ProtocolMismatch = 1,
    GameVersionMismatch = 2,
    MissingOnClient = 3,
    MissingOnHost = 4,
    VersionMismatch = 5
}
