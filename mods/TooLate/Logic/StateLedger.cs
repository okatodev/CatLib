using System;
using System.Collections.Generic;
using System.Linq;

namespace TooLate.Logic;

public readonly struct StateMessage
{
    public StateMessage(uint entityId, string text, string kind)
    {
        EntityId = entityId;
        Text = text;
        Kind = kind;
    }

    public uint EntityId { get; }

    public string Text { get; }

    public string Kind { get; }
}

public sealed class StateLedger
{
    public const int MostMessages = 20000;
    public const string CounterKind = "CustomerCounter";
    public const string CounterOpenKind = "CustomerCounter;OpenState";

    private static readonly char[] Separators = { ';', ':' };

    private static readonly HashSet<string> MomentKinds = new(StringComparer.Ordinal)
    {
        "Captain",
        "Customer",
        "CustomerAudio",
        "CustomerAppearance",
        "CarryParcel"
    };

    private readonly Dictionary<(uint EntityId, string Kind), (long Order, StateMessage Message)> _latest = new();
    private long _order;

    public int Count => _latest.Count;

    public static string KindOf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var first = text.IndexOfAny(Separators);
        if (first < 0)
        {
            return text;
        }

        var kind = text.Substring(0, first);
        if (kind != CounterKind)
        {
            return kind;
        }

        var second = text.IndexOfAny(Separators, first + 1);
        return second < 0 ? text : text.Substring(0, second);
    }

    public static bool IsLasting(string kind)
    {
        if (string.IsNullOrEmpty(kind) || MomentKinds.Contains(kind))
        {
            return false;
        }

        return !kind.StartsWith(CounterKind, StringComparison.Ordinal) || kind == CounterOpenKind;
    }

    public bool Record(uint entityId, string text)
    {
        if (entityId == 0 || text == null)
        {
            return false;
        }

        var kind = KindOf(text);
        if (!IsLasting(kind))
        {
            return false;
        }

        var key = (entityId, kind);
        if (!_latest.ContainsKey(key) && _latest.Count >= MostMessages)
        {
            return false;
        }

        _latest[key] = (_order++, new StateMessage(entityId, text, kind));
        return true;
    }

    public int Forget(uint entityId)
    {
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

    public IReadOnlyList<StateMessage> For(Func<uint, bool> known) =>
        _latest.Values.Where(entry => known == null || known(entry.Message.EntityId)).OrderBy(entry => entry.Order).Select(entry => entry.Message).ToList();
}
