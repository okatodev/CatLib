using System.Collections.Generic;

namespace CatLib.Net;

public static class MessageCodec
{
    public const uint Magic = 0x4C544143;
    public const ushort ProtocolVersion = 3;
    public const int HeaderBytes = 7;
    public const int MaxMessageBytes = 64 * 1024;
    public const int MaxStringBytes = 1024;
    public const int MaxItems = 1024;

    public static byte[] Encode(HelloMessage message)
    {
        var writer = Header(MessageType.Hello);
        var identity = message.Identity;
        writer.WriteString(identity.CatLibVersion);
        writer.WriteString(identity.GameVersion);
        writer.WriteCount(identity.Mods.Count);
        foreach (var mod in identity.Mods)
        {
            writer.WriteString(mod.Id);
            writer.WriteString(mod.Name);
            writer.WriteString(mod.Version);
            writer.WriteByte((byte)mod.Policy);
            writer.WriteByte((byte)mod.Rule);
        }

        return writer.ToArray();
    }

    public static byte[] Encode(VerdictMessage message)
    {
        var writer = Header(MessageType.Verdict);
        writer.WriteBool(message.Accepted);
        writer.WriteBool(message.Disconnecting);
        writer.WriteCount(message.Problems.Count);
        foreach (var problem in message.Problems)
        {
            writer.WriteByte((byte)problem.Kind);
            writer.WriteString(problem.Subject);
            writer.WriteString(problem.HostValue);
            writer.WriteString(problem.ClientValue);
        }

        WriteSettings(writer, message.Settings);
        return writer.ToArray();
    }

    public static byte[] Encode(AnnounceMessage message) => Header(MessageType.Announce).ToArray();

    public static byte[] Encode(SettingsUpdateMessage message)
    {
        var writer = Header(MessageType.SettingsUpdate);
        WriteSettings(writer, message.Settings);
        return writer.ToArray();
    }

    public static DecodedMessage Decode(byte[] data)
    {
        var reader = new WireReader(data, 0);
        if (reader.ReadUInt32() != Magic)
        {
            throw new WireFormatException("Not a CatLib message");
        }

        var protocol = reader.ReadUInt16();
        var type = reader.ReadEnum(MessageType.Hello, MessageType.Announce);
        if (protocol != ProtocolVersion)
        {
            return new DecodedMessage(protocol, type, null);
        }

        object payload = type switch
        {
            MessageType.Hello => ReadHello(reader),
            MessageType.Verdict => ReadVerdict(reader),
            MessageType.Announce => new AnnounceMessage(),
            _ => new SettingsUpdateMessage(ReadSettings(reader))
        };

        reader.EnsureEnd();
        return new DecodedMessage(protocol, type, payload);
    }

    private static WireWriter Header(MessageType type)
    {
        var writer = new WireWriter();
        writer.WriteUInt32(Magic);
        writer.WriteUInt16(ProtocolVersion);
        writer.WriteByte((byte)type);
        return writer;
    }

    private static HelloMessage ReadHello(WireReader reader)
    {
        var catLibVersion = reader.ReadString();
        var gameVersion = reader.ReadString();
        var count = reader.ReadCount();
        var mods = new List<ModInfo>(count);
        for (var index = 0; index < count; index++)
        {
            var id = reader.ReadString();
            if (string.IsNullOrEmpty(id))
            {
                throw new WireFormatException("Mod id is empty");
            }

            mods.Add(new ModInfo(id, reader.ReadString(), reader.ReadString(),
                reader.ReadEnum(SessionPolicy.RequiredOnAll, SessionPolicy.ClientOnly),
                reader.ReadEnum(VersionRule.Exact, VersionRule.Any)));
        }

        return new HelloMessage(new LocalIdentity(catLibVersion, gameVersion, mods));
    }

    private static VerdictMessage ReadVerdict(WireReader reader)
    {
        var accepted = reader.ReadBool();
        var disconnecting = reader.ReadBool();
        var count = reader.ReadCount();
        var problems = new List<CompatibilityProblem>(count);
        for (var index = 0; index < count; index++)
        {
            problems.Add(new CompatibilityProblem(reader.ReadEnum(ProblemKind.ProtocolMismatch, ProblemKind.VersionMismatch),
                reader.ReadString(), reader.ReadString(), reader.ReadString()));
        }

        return new VerdictMessage(accepted, problems, ReadSettings(reader), disconnecting);
    }

    private static void WriteSettings(WireWriter writer, IReadOnlyList<SessionSettingValue> settings)
    {
        writer.WriteCount(settings.Count);
        foreach (var setting in settings)
        {
            writer.WriteString(setting.OwnerId);
            writer.WriteString(setting.Section);
            writer.WriteString(setting.Key);
            writer.WriteString(setting.Value);
        }
    }

    private static IReadOnlyList<SessionSettingValue> ReadSettings(WireReader reader)
    {
        var count = reader.ReadCount();
        var settings = new List<SessionSettingValue>(count);
        for (var index = 0; index < count; index++)
        {
            var ownerId = reader.ReadString();
            var section = reader.ReadString();
            var key = reader.ReadString();
            var value = reader.ReadString();
            if (string.IsNullOrEmpty(ownerId) || section == null || string.IsNullOrEmpty(key) || value == null)
            {
                throw new WireFormatException("Session setting entry is incomplete");
            }

            settings.Add(new SessionSettingValue(ownerId, section, key, value));
        }

        return settings;
    }
}
