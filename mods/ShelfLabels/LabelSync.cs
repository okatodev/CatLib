using System;
using System.Globalization;
using CatLib.Logging;
using CatLib.Net;

namespace ShelfLabels;

public sealed class LabelSync
{
    public const string StepMessage = "step";
    public const string SetMessage = "set";
    public const string ResetMessage = "reset";
    public const string PlaceMessage = "place";
    public const string LayoutMessage = "layout";
    public const string StandMessage = "stand";
    public const string StandsMessage = "stands";
    public const int ChunkBytes = 12 * 1024;

    private readonly ModChannel _channel;
    private readonly LabelBoard _board;
    private readonly PlacementBoard _placements;
    private readonly StandBoard _stands;
    private readonly Func<bool> _hideStandsByDefault;
    private readonly Func<int, int> _spriteCount;
    private readonly Func<int> _slots;
    private readonly Func<Placement> _defaultPlacement;
    private readonly CatLogger _log;

    public LabelSync(ModChannel channel, LabelBoard board, PlacementBoard placements, StandBoard stands, Func<int, int> spriteCount, Func<int> slots,
        Func<Placement> defaultPlacement, Func<bool> hideStandsByDefault, CatLogger log)
    {
        _channel = channel;
        _board = board;
        _placements = placements;
        _stands = stands;
        _hideStandsByDefault = hideStandsByDefault;
        _spriteCount = spriteCount;
        _slots = slots;
        _defaultPlacement = defaultPlacement;
        _log = log;
        _channel.Received += OnReceived;
        _channel.PeerJoined += peer => SendAll(peer);
    }

    public int Rejected { get; private set; }

    public bool Click(LabelSlot slot, int step)
    {
        if (!slot.IsValid || (step != 1 && step != -1))
        {
            return false;
        }

        return _channel.SendToHost(StepMessage, slot.Text + "/" + step.ToString("+0;-0", CultureInfo.InvariantCulture));
    }

    public bool CyclePlacement(int labelId) =>
        labelId > 0 && _channel.SendToHost(PlaceMessage, labelId.ToString(CultureInfo.InvariantCulture) + "/+1");

    public bool ToggleStand(int labelId) =>
        labelId > 0 && _channel.SendToHost(StandMessage, labelId.ToString(CultureInfo.InvariantCulture) + "/toggle");

    public int SendAll(ulong peer)
    {
        var sent = _channel.SendTo(peer, ResetMessage, string.Empty) ? 1 : 0;
        foreach (var chunk in _board.Chunks(ChunkBytes))
        {
            sent += _channel.SendTo(peer, SetMessage, chunk) ? 1 : 0;
        }

        foreach (var chunk in _placements.Chunks(ChunkBytes))
        {
            sent += _channel.SendTo(peer, LayoutMessage, chunk) ? 1 : 0;
        }

        foreach (var chunk in _stands.Chunks(ChunkBytes))
        {
            sent += _channel.SendTo(peer, StandsMessage, chunk) ? 1 : 0;
        }

        _log?.Info($"Sent {_board.Count} label picture(s), {_placements.Count} placement(s) and {_stands.Count} stand choice(s) to player {peer} in {sent} message(s)");
        return sent;
    }

    private void OnReceived(ModMessage message)
    {
        if (!message.FromHost)
        {
            if (message.Name == PlaceMessage)
            {
                HandlePlace(message);
            }
            else if (message.Name == StandMessage)
            {
                HandleStand(message);
            }
            else
            {
                HandleStep(message);
            }

            return;
        }

        switch (message.Name)
        {
            case ResetMessage:
                _board.Clear();
                _placements.Clear();
                _stands.Clear();
                break;
            case StandsMessage:
                _stands.Apply(StandBoard.Parse(message.Text));
                break;
            case SetMessage:
                _board.Apply(LabelBoard.Parse(message.Text));
                break;
            case LayoutMessage:
                _placements.Apply(PlacementBoard.Parse(message.Text));
                break;
            default:
                _log?.Debug($"Ignored an unknown label message {message.Name}");
                break;
        }
    }

    private void HandleStep(ModMessage message)
    {
        if (message.Name != StepMessage || !TryParseStep(message.Text, out var slot, out var step))
        {
            Reject(message, "malformed request");
            return;
        }

        if (slot.Slot > _slots())
        {
            Reject(message, $"slot {slot.Slot} is not shown, {_slots()} extra label(s) per shelf");
            return;
        }

        var count = _spriteCount(slot.LabelId);
        if (count <= 0)
        {
            Reject(message, $"label {slot.LabelId} is unknown here");
            return;
        }

        var index = _board.Step(slot, step, count);
        _channel.Broadcast(SetMessage, slot.Text + "=" + index.ToString(CultureInfo.InvariantCulture));
    }

    private void HandlePlace(ModMessage message)
    {
        var text = message.Text;
        var cut = text.IndexOf('/');
        if (cut <= 0 || text.Substring(cut) != "/+1"
            || !int.TryParse(text.Substring(0, cut), NumberStyles.None, CultureInfo.InvariantCulture, out var labelId) || labelId <= 0)
        {
            Reject(message, "malformed placement request");
            return;
        }

        if (_spriteCount(labelId) <= 0)
        {
            Reject(message, $"label {labelId} is unknown here");
            return;
        }

        var next = PlacementBoard.Next(_placements.Get(labelId, _defaultPlacement()));
        _placements.Set(labelId, next);
        _channel.Broadcast(LayoutMessage, PlacementBoard.Format(labelId, next));
    }

    private void HandleStand(ModMessage message)
    {
        var text = message.Text;
        var cut = text.IndexOf('/');
        if (cut <= 0 || text.Substring(cut) != "/toggle"
            || !int.TryParse(text.Substring(0, cut), NumberStyles.None, CultureInfo.InvariantCulture, out var labelId) || labelId <= 0)
        {
            Reject(message, "malformed stand request");
            return;
        }

        if (_spriteCount(labelId) <= 0)
        {
            Reject(message, $"label {labelId} is unknown here");
            return;
        }

        var hidden = !_stands.IsHidden(labelId, _hideStandsByDefault());
        _stands.Set(labelId, hidden);
        _channel.Broadcast(StandsMessage, StandBoard.Format(labelId, hidden));
    }

    private void Reject(ModMessage message, string reason)
    {
        Rejected++;
        _log?.Warning($"Ignored a label request \"{message.Text}\" from {message.Sender}: {reason}");
    }

    public static bool TryParseStep(string text, out LabelSlot slot, out int step)
    {
        slot = default;
        step = 0;
        var cut = text?.LastIndexOf('/') ?? -1;
        if (cut <= 0 || !LabelSlot.TryParse(text.Substring(0, cut), out slot))
        {
            return false;
        }

        var tail = text.Substring(cut + 1);
        step = tail switch
        {
            "+1" => 1,
            "-1" => -1,
            _ => 0
        };
        return step != 0;
    }
}
