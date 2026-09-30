using System.Collections.Generic;

namespace CatLib.Net;

public static class MessageCodec
{
    public const uint Magic = 0x4C544143;
    public const ushort ProtocolVersion = 5;
    public const int HeaderBytes = 7;
    public const int MaxMessageBytes = 64 * 1024;
    public const int MaxStringBytes = 1024;
    public const int MaxItems = 1024;
    public const int MaxModDataBytes = 16 * 1024;
    public const int MaxModNameBytes = 64;

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
        WriteProblems(writer, message.Problems);
        WriteSettings(writer, message.Settings);
        WriteIds(writer, message.SharedModIds);
        return writer.ToArray();
    }

    public static byte[] Encode(ModMessageData message)
    {
        var writer = Header(MessageType.Mod);
        writer.WriteString(message.ModId);
        writer.WriteString(message.Name);
        writer.WriteBytes(message.Data, MaxModDataBytes);
        return writer.ToArray();
    }

    public static byte[] Encode(RosterMessage message)
    {
        var writer = Header(MessageType.Roster);
        var roster = message.Roster;
        writer.WriteByte((byte)roster.Policy);
        WriteIds(writer, roster.ActiveMods);
        writer.WriteCount(roster.Players.Count);
        foreach (var player in roster.Players)
        {
            writer.WriteUInt64(player.Id);
            writer.WriteString(player.Name);
            writer.WriteBool(player.IsHost);
            writer.WriteByte((byte)player.Status);
            writer.WriteString(player.CatLibVersion);
            writer.WriteString(player.GameVersion);
            WriteProblems(writer, player.Problems);
            writer.WriteCount(player.Mods.Count);
            foreach (var mod in player.Mods)
            {
                writer.WriteString(mod.Id);
                writer.WriteString(mod.Name);
                writer.WriteString(mod.Version);
                writer.WriteByte((byte)mod.Policy);
                writer.WriteByte((byte)mod.Mark);
                writer.WriteString(mod.HostVersion);
            }
        }

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
        var type = reader.ReadEnum(MessageType.Hello, MessageType.Roster);
        if (protocol != ProtocolVersion)
        {
            return new DecodedMessage(protocol, type, null);
        }

        object payload = type switch
        {
            MessageType.Hello => ReadHello(reader),
            MessageType.Verdict => ReadVerdict(reader),
            MessageType.Announce => new AnnounceMessage(),
            MessageType.Mod => ReadMod(reader),
            MessageType.Roster => ReadRoster(reader),
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
        var problems = ReadProblems(reader);
        var settings = ReadSettings(reader);
        return new VerdictMessage(accepted, problems, settings, disconnecting, ReadIds(reader));
    }

    private static RosterMessage ReadRoster(WireReader reader)
    {
        var policy = reader.ReadEnum(IncompatiblePlayerAction.Warn, IncompatiblePlayerAction.Disconnect);
        var active = ReadIds(reader);
        var count = reader.ReadCount();
        var players = new List<RosterEntry>(count);
        for (var index = 0; index < count; index++)
        {
            var id = reader.ReadUInt64();
            var name = reader.ReadString() ?? string.Empty;
            var isHost = reader.ReadBool();
            var status = reader.ReadEnum(RosterStatus.Checking, RosterStatus.Leaving);
            var catLibVersion = reader.ReadString() ?? string.Empty;
            var gameVersion = reader.ReadString() ?? string.Empty;
            var problems = ReadProblems(reader);
            var modCount = reader.ReadCount();
            var mods = new List<RosterMod>(modCount);
            for (var modIndex = 0; modIndex < modCount; modIndex++)
            {
                var modId = reader.ReadString();
                if (string.IsNullOrEmpty(modId))
                {
                    throw new WireFormatException("Roster mod id is empty");
                }

                mods.Add(new RosterMod(modId, reader.ReadString() ?? modId, reader.ReadString() ?? string.Empty,
                    reader.ReadEnum(SessionPolicy.RequiredOnAll, SessionPolicy.ClientOnly),
                    reader.ReadEnum(ModMark.Same, ModMark.Local), reader.ReadString()));
            }

            players.Add(new RosterEntry(id, name, isHost, status, catLibVersion, gameVersion, problems, mods));
        }

        return new RosterMessage(new SessionRoster(policy, active, players));
    }

    private static void WriteProblems(WireWriter writer, IReadOnlyList<CompatibilityProblem> problems)
    {
        writer.WriteCount(problems.Count);
        foreach (var problem in problems)
        {
            writer.WriteByte((byte)problem.Kind);
            writer.WriteString(problem.Subject);
            writer.WriteString(problem.HostValue);
            writer.WriteString(problem.ClientValue);
        }
    }

    private static IReadOnlyList<CompatibilityProblem> ReadProblems(WireReader reader)
    {
        var count = reader.ReadCount();
        var problems = new List<CompatibilityProblem>(count);
        for (var index = 0; index < count; index++)
        {
            problems.Add(new CompatibilityProblem(reader.ReadEnum(ProblemKind.ProtocolMismatch, ProblemKind.VersionMismatch),
                reader.ReadString(), reader.ReadString(), reader.ReadString()));
        }

        return problems;
    }

    private static ModMessageData ReadMod(WireReader reader)
    {
        var modId = reader.ReadString();
        var name = reader.ReadString();
        if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(name))
        {
            throw new WireFormatException("Mod message has no mod id or name");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(name) > MaxModNameBytes)
        {
            throw new WireFormatException($"Mod message name exceeds {MaxModNameBytes} bytes");
        }

        return new ModMessageData(modId, name, reader.ReadBytes(MaxModDataBytes));
    }

    private static void WriteIds(WireWriter writer, IReadOnlyList<string> ids)
    {
        writer.WriteCount(ids.Count);
        foreach (var id in ids)
        {
            writer.WriteString(id);
        }
    }

    private static IReadOnlyList<string> ReadIds(WireReader reader)
    {
        var count = reader.ReadCount();
        var ids = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var id = reader.ReadString();
            if (string.IsNullOrEmpty(id))
            {
                throw new WireFormatException("Shared mod id is empty");
            }

            ids.Add(id);
        }

        return ids;
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
