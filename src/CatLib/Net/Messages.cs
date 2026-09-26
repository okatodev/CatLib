using System.Collections.Generic;

namespace CatLib.Net;

public sealed record HelloMessage(LocalIdentity Identity);

public sealed record VerdictMessage(bool Accepted, IReadOnlyList<CompatibilityProblem> Problems, IReadOnlyList<SessionSettingValue> Settings, bool Disconnecting = false, IReadOnlyList<string> SharedMods = null)
{
    public IReadOnlyList<string> SharedModIds => SharedMods ?? System.Array.Empty<string>();
}

public sealed record SettingsUpdateMessage(IReadOnlyList<SessionSettingValue> Settings);

public sealed record AnnounceMessage;

public sealed record ModMessageData(string ModId, string Name, byte[] Data);

public sealed record DecodedMessage(ushort Protocol, MessageType Type, object Payload)
{
    public bool IsProtocolMismatch => Protocol != MessageCodec.ProtocolVersion;
}
