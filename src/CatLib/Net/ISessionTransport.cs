namespace CatLib.Net;

public interface ISessionTransport
{
    ulong LocalId { get; }

    void Send(ulong peer, byte[] payload);
}
