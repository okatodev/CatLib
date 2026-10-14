using System;
using System.Collections.Generic;
using System.Linq;

namespace TooLate.Logic;

public enum HoldKind
{
    Authority,
    PickUp
}

public readonly struct HoldMessage
{
    public HoldMessage(long order, HoldKind kind, ulong clientId, uint entityId)
    {
        Order = order;
        Kind = kind;
        ClientId = clientId;
        EntityId = entityId;
    }

    public long Order { get; }

    public HoldKind Kind { get; }

    public ulong ClientId { get; }

    public uint EntityId { get; }
}

public sealed class HoldLedger
{
    public const int MostAuthorities = 20000;

    private readonly Dictionary<uint, (long Order, ulong ClientId)> _authority = new();
    private readonly Dictionary<ulong, (long Order, uint EntityId)> _held = new();
    private long _order;

    public long Mark => _order;

    public int AuthorityCount => _authority.Count;

    public int HeldCount => _held.Count;

    public bool RecordAuthority(ulong clientId, uint entityId)
    {
        if (entityId == 0 || (!_authority.ContainsKey(entityId) && _authority.Count >= MostAuthorities))
        {
            return false;
        }

        _authority[entityId] = (_order++, clientId);
        return true;
    }

    public bool RecordPickUp(ulong clientId, uint entityId)
    {
        if (entityId == 0)
        {
            return false;
        }

        foreach (var other in _held.Where(pair => pair.Key != clientId && pair.Value.EntityId == entityId).Select(pair => pair.Key).ToList())
        {
            _held.Remove(other);
        }

        _held[clientId] = (_order++, entityId);
        return true;
    }

    public bool RecordDrop(ulong clientId, uint entityId)
    {
        if (!_held.TryGetValue(clientId, out var held) || (entityId != 0 && held.EntityId != entityId))
        {
            return false;
        }

        _held.Remove(clientId);
        return true;
    }

    public int Forget(uint entityId)
    {
        var count = _authority.Remove(entityId) ? 1 : 0;
        foreach (var client in _held.Where(pair => pair.Value.EntityId == entityId).Select(pair => pair.Key).ToList())
        {
            _held.Remove(client);
            count++;
        }

        return count;
    }

    public int ForgetClient(ulong clientId)
    {
        var count = _held.Remove(clientId) ? 1 : 0;
        foreach (var entity in _authority.Where(pair => pair.Value.ClientId == clientId).Select(pair => pair.Key).ToList())
        {
            _authority.Remove(entity);
            count++;
        }

        return count;
    }

    public void Clear()
    {
        _authority.Clear();
        _held.Clear();
        _order = 0;
    }

    public IReadOnlyList<HoldMessage> Before(long mark, Func<uint, bool> known, Func<ulong, bool> present) =>
        _authority.Select(pair => new HoldMessage(pair.Value.Order, HoldKind.Authority, pair.Value.ClientId, pair.Key))
            .Concat(_held.Select(pair => new HoldMessage(pair.Value.Order, HoldKind.PickUp, pair.Key, pair.Value.EntityId)))
            .Where(message => message.Order < mark && (known == null || known(message.EntityId)) && (present == null || present(message.ClientId)))
            .OrderBy(message => message.Kind == HoldKind.Authority ? 0 : 1).ThenBy(message => message.Order).ToList();
}
