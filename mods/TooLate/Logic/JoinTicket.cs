using System.Collections.Generic;

namespace TooLate.Logic;

public readonly struct HeldMessage<T>
{
    public HeldMessage(int code, bool reliable, T payload)
    {
        Code = code;
        Reliable = reliable;
        Payload = payload;
    }

    public int Code { get; }

    public bool Reliable { get; }

    public T Payload { get; }
}

public sealed class JoinTicket<T>
{
    public const int MostHeldMessages = 20000;

    private readonly List<HeldMessage<T>> _held = new();

    public JoinTicket(ulong clientId, string name, double now)
    {
        ClientId = clientId;
        Name = name;
        ConnectedAt = now;
        StageSince = now;
    }

    public ulong ClientId { get; }

    public string Name { get; }

    public double ConnectedAt { get; }

    public JoinStage Stage { get; private set; } = JoinStage.Waiting;

    public double StageSince { get; private set; }

    public IReadOnlyList<EntityPlace> Places { get; set; }

    public int Dropped { get; private set; }

    public int Overflowed { get; private set; }

    public bool WaitingNoticeShown { get; set; }

    public bool Flushing { get; set; }

    public bool PlacesMerged { get; set; }

    public bool StateSent { get; set; }

    public IReadOnlyList<EntityPlace> ScenePlaces { get; set; }

    public string FailReason { get; set; }

    public IReadOnlyList<HeldMessage<T>> Held => _held;

    public void MoveTo(JoinStage stage, double now)
    {
        Stage = stage;
        StageSince = now;
    }

    public Route Accept(int code, bool reliable, T payload)
    {
        var route = MessageRoute.For(Stage, code);
        switch (route)
        {
            case Route.Drop:
                Dropped++;
                break;
            case Route.Hold:
                Hold(code, reliable, payload);
                break;
            case Route.HoldLatest:
                _held.RemoveAll(message => message.Code == code);
                Hold(code, reliable, payload);
                break;
        }

        return route;
    }

    public List<HeldMessage<T>> TakeHeld() => TakeHeld(int.MaxValue);

    public List<HeldMessage<T>> TakeHeld(int most)
    {
        var count = System.Math.Min(System.Math.Max(0, most), _held.Count);
        var taken = _held.GetRange(0, count);
        _held.RemoveRange(0, count);
        return taken;
    }

    private void Hold(int code, bool reliable, T payload)
    {
        if (_held.Count >= MostHeldMessages)
        {
            Overflowed++;
            return;
        }

        _held.Add(new HeldMessage<T>(code, reliable, payload));
    }
}
