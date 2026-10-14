using System;
using System.Collections.Generic;
using System.Linq;

namespace TooLate.Logic;

public readonly struct ReplayMessage<T>
{
    public ReplayMessage(long order, int code, uint entityId, T payload)
    {
        Order = order;
        Code = code;
        EntityId = entityId;
        Payload = payload;
    }

    public long Order { get; }

    public int Code { get; }

    public uint EntityId { get; }

    public T Payload { get; }
}

public readonly struct CustomerLine
{
    public CustomerLine(long order, uint customerId, string text)
    {
        Order = order;
        CustomerId = customerId;
        Text = text;
    }

    public long Order { get; }

    public uint CustomerId { get; }

    public string Text { get; }
}

public sealed class CustomerLedger<T>
{
    public const string CustomerKey = "p_EntityCustomer";
    public const string StoragePrefix = "p_Entity_Storage_CustomerParcels";
    public const string AppearanceKind = "CustomerAppearance";
    public const string CustomerKind = "Customer";
    public const string CarryKind = "CarryParcel";
    public const int MostRecent = 128;
    public const int MostMembers = 1000;

    private readonly Dictionary<uint, Entry> _entries = new();
    private long _order;
    private long _created;

    public long Mark => _order;

    public int Count => _entries.Values.Count(entry => entry.Member);

    public int CustomerCount => _entries.Values.Count(entry => entry.Customer && entry.HasSpawn);

    public static bool IsCustomer(string key) => key == CustomerKey;

    public static bool IsCustomerStorage(string key) => key != null && key.StartsWith(StoragePrefix, StringComparison.Ordinal);

    public static Slot SlotOf(string text, out uint carried)
    {
        carried = 0;
        if (string.IsNullOrEmpty(text))
        {
            return Slot.None;
        }

        var parts = text.Split(';');
        switch (parts[0])
        {
            case AppearanceKind:
                return Slot.Appearance;
            case CarryKind:
                return parts.Length > 1 && uint.TryParse(parts[1], out carried) ? Slot.Carry : Slot.None;
            case CustomerKind when parts.Length > 1:
                return parts[1] switch
                {
                    "1" or "2" or "3" => Slot.Request,
                    "6" or "7" => Slot.Farewell,
                    _ => Slot.None
                };
            default:
                return Slot.None;
        }
    }

    public void RecordSpawn(uint entityId, string key, T payload)
    {
        if (entityId == 0)
        {
            return;
        }

        var entry = Get(entityId);
        if (entry == null)
        {
            return;
        }

        entry.Key = key;
        entry.Spawn = payload;
        entry.SpawnOrder = _order++;
        entry.HasSpawn = true;
        if (IsCustomer(key))
        {
            entry.Customer = true;
            Promote(entry);
        }
        else if (IsCustomerStorage(key))
        {
            Promote(entry);
        }
    }

    public void RecordProperties(uint entityId, T payload)
    {
        if (entityId == 0)
        {
            return;
        }

        var entry = Get(entityId);
        if (entry == null)
        {
            return;
        }

        entry.Properties = payload;
        entry.PropertiesOrder = _order++;
        entry.HasProperties = true;
    }

    public void RecordStored(uint storeId, uint entityId, T payload)
    {
        if (entityId == 0)
        {
            return;
        }

        var entry = Get(entityId);
        if (entry == null)
        {
            return;
        }

        entry.Stored = payload;
        entry.StoredOrder = _order++;
        entry.Store = storeId;
        entry.HasStored = true;
        if (_entries.TryGetValue(storeId, out var store) && store.Member)
        {
            Promote(entry);
        }
    }

    public bool RecordLine(uint entityId, string text)
    {
        if (!_entries.TryGetValue(entityId, out var entry) || !entry.Customer)
        {
            return false;
        }

        var slot = SlotOf(text, out var carried);
        if (slot == Slot.None)
        {
            return false;
        }

        entry.Lines[slot] = new CustomerLine(_order++, entityId, text);
        if (slot == Slot.Carry)
        {
            var parcel = Get(carried);
            if (parcel != null)
            {
                Promote(parcel);
            }
        }

        return true;
    }

    public bool Forget(uint entityId)
    {
        if (!_entries.TryGetValue(entityId, out var entry))
        {
            return false;
        }

        _entries.Remove(entityId);
        if (entry.Member)
        {
            Release(entry);
        }

        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _order = 0;
        _created = 0;
    }

    public IReadOnlyList<ReplayMessage<T>> Before(long mark, Func<uint, bool> known, int spawnCode, int storedCode, int propertiesCode)
    {
        var replayed = new HashSet<uint>(_entries.Values
            .Where(entry => entry.Member && entry.HasSpawn && entry.SpawnOrder < mark && (known == null || !known(entry.Id)))
            .Select(entry => entry.Id));
        var result = new List<ReplayMessage<T>>();
        foreach (var id in replayed)
        {
            var entry = _entries[id];
            result.Add(new ReplayMessage<T>(entry.SpawnOrder, spawnCode, id, entry.Spawn));
            if (entry.HasProperties && entry.PropertiesOrder < mark)
            {
                result.Add(new ReplayMessage<T>(Math.Max(entry.PropertiesOrder, entry.SpawnOrder), propertiesCode, id, entry.Properties));
            }

            if (entry.HasStored && entry.StoredOrder < mark && (replayed.Contains(entry.Store) || (known != null && known(entry.Store))))
            {
                var after = replayed.Contains(entry.Store) ? Math.Max(entry.SpawnOrder, _entries[entry.Store].SpawnOrder) : entry.SpawnOrder;
                result.Add(new ReplayMessage<T>(Math.Max(entry.StoredOrder, after), storedCode, id, entry.Stored));
            }
        }

        return result.OrderBy(message => message.Order).ThenBy(message => message.Code == spawnCode ? 0 : message.Code == storedCode ? 1 : 2).ToList();
    }

    public IReadOnlyList<uint> Customers() =>
        _entries.Values.Where(entry => entry.Customer && entry.HasSpawn).OrderBy(entry => entry.SpawnOrder).Select(entry => entry.Id).ToList();

    public IReadOnlyList<uint> MembersSince(long mark) =>
        _entries.Values.Where(entry => entry.Member && entry.HasSpawn && entry.SpawnOrder >= mark).OrderBy(entry => entry.SpawnOrder).Select(entry => entry.Id).ToList();

    public IReadOnlyList<CustomerLine> LinesOf(uint customerId, long since) =>
        _entries.TryGetValue(customerId, out var entry) && entry.Customer
            ? entry.Lines.Values.Where(line => line.Order >= since).OrderBy(line => line.Order).ToList()
            : new List<CustomerLine>();

    private Entry Get(uint entityId)
    {
        if (_entries.TryGetValue(entityId, out var entry))
        {
            return entry;
        }

        entry = new Entry(entityId, _created++);
        _entries[entityId] = entry;
        Trim();
        return entry;
    }

    private void Release(Entry gone)
    {
        var freed = new Queue<Entry>();
        freed.Enqueue(gone);
        while (freed.Count > 0)
        {
            var holder = freed.Dequeue();
            var carried = holder.Customer && holder.Lines.TryGetValue(Slot.Carry, out var line) && SlotOf(line.Text, out var parcel) == Slot.Carry ? parcel : 0;
            foreach (var entry in _entries.Values.Where(entry => entry.Member && !entry.Customer && !IsCustomerStorage(entry.Key)
                         && ((entry.HasStored && entry.Store == holder.Id) || entry.Id == carried)).ToList())
            {
                entry.Member = false;
                freed.Enqueue(entry);
            }
        }

        Trim();
    }

    private void Promote(Entry entry)
    {
        if (entry.Member)
        {
            return;
        }

        if (Count >= MostMembers)
        {
            return;
        }

        entry.Member = true;
    }

    private void Trim()
    {
        var waiting = _entries.Values.Where(entry => !entry.Member).ToList();
        if (waiting.Count <= MostRecent)
        {
            return;
        }

        foreach (var entry in waiting.OrderBy(entry => entry.Created).Take(waiting.Count - MostRecent))
        {
            _entries.Remove(entry.Id);
        }
    }

    public enum Slot
    {
        None,
        Appearance,
        Request,
        Carry,
        Farewell
    }

    private sealed class Entry
    {
        public Entry(uint id, long created)
        {
            Id = id;
            Created = created;
        }

        public uint Id { get; }

        public long Created { get; }

        public string Key { get; set; }

        public bool Member { get; set; }

        public bool Customer { get; set; }

        public bool HasSpawn { get; set; }

        public T Spawn { get; set; }

        public long SpawnOrder { get; set; }

        public bool HasProperties { get; set; }

        public T Properties { get; set; }

        public long PropertiesOrder { get; set; }

        public bool HasStored { get; set; }

        public T Stored { get; set; }

        public long StoredOrder { get; set; }

        public uint Store { get; set; }

        public Dictionary<Slot, CustomerLine> Lines { get; } = new();
    }
}
