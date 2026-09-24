using System.Collections.Generic;

namespace CatLib.Net;

public sealed record HelloMessage(LocalIdentity Identity);

public sealed record VerdictMessage(bool Accepted, IReadOnlyList<CompatibilityProblem> Problems, IReadOnlyList<SessionSettingValue> Settings);

public sealed record SettingsUpdateMessage(IReadOnlyList<SessionSettingValue> Settings);

public sealed record DecodedMessage(ushort Protocol, MessageType Type, object Payload)
{
    public bool IsProtocolMismatch => Protocol != MessageCodec.ProtocolVersion;
}
