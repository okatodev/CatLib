using System;
using System.Collections.Generic;
using System.Linq;

namespace TooLate.Logic;

public readonly struct LastingMessage<T>
{
    public LastingMessage(int code, uint entityId, T payload)
    {
        Code = code;
        EntityId = entityId;
        Payload = payload;
    }

    public int Code { get; }

    public uint EntityId { get; }

    public T Payload { get; }
}

public sealed class LastingMessages<T>
{
    public const int MostMessages = 20000;

    private readonly Dictionary<(int Code, uint EntityId), (long Order, LastingMessage<T> Message)> _latest = new();
    private long _order;

    public int Count => _latest.Count;

    public static bool IsLasting(int code) => code is ProtocolCodes.SetParcelDamaged or ProtocolCodes.SetInteractionsLocked or ProtocolCodes.BoatDestinations;

    public static bool IsPerEntity(int code) => code != ProtocolCodes.BoatDestinations;

    public bool Record(int code, uint entityId, T payload)
    {
        if (!IsLasting(code))
        {
            return false;
        }

        if (!IsPerEntity(code))
        {
            entityId = 0;
        }
        else if (entityId == 0)
        {
            return false;
        }

        var key = (code, entityId);
        if (!_latest.ContainsKey(key) && _latest.Count >= MostMessages)
        {
            return false;
        }

        _latest[key] = (_order++, new LastingMessage<T>(code, entityId, payload));
        return true;
    }

    public int Forget(uint entityId)
    {
        if (entityId == 0)
        {
            return 0;
        }

        var keys = _latest.Keys.Where(key => key.EntityId == entityId).ToList();
        foreach (var key in keys)
        {
            _latest.Remove(key);
        }

        return keys.Count;
    }

    public void Clear()
    {
        _latest.Clear();
        _order = 0;
    }

    public IReadOnlyList<LastingMessage<T>> For(Func<uint, bool> known) =>
        _latest.Values.Where(entry => entry.Message.EntityId == 0 || known == null || known(entry.Message.EntityId))
            .OrderBy(entry => entry.Order).Select(entry => entry.Message).ToList();
}
