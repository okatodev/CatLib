using System.Text;

namespace CatLib.Net;

public sealed class ModMessage
{
    internal ModMessage(string modId, string name, byte[] data, ulong sender, bool fromHost, bool isLocal)
    {
        ModId = modId;
        Name = name;
        Data = data ?? System.Array.Empty<byte>();
        Sender = sender;
        FromHost = fromHost;
        IsLocal = isLocal;
    }

    public string ModId { get; }

    public string Name { get; }

    public byte[] Data { get; }

    public ulong Sender { get; }

    public bool FromHost { get; }

    public bool IsLocal { get; }

    public string Text => Encoding.UTF8.GetString(Data);

    public override string ToString() => $"{ModId}/{Name} from {(IsLocal ? "self" : Sender.ToString(System.Globalization.CultureInfo.InvariantCulture))}{(FromHost ? " (host)" : string.Empty)}, {Data.Length} byte(s)";
}
