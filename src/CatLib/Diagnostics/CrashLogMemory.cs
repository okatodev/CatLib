using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;

namespace CatLib.Diagnostics;

internal sealed class CrashLogMemory : ILogListener
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly ILogListener _disk;
    private IntPtr _memory;
    private long _written;

    private CrashLogMemory(int capacity)
    {
        _capacity = capacity;
        _memory = Marshal.AllocHGlobal(CrashLogRing.HeaderSize + capacity);
        Marshal.Copy(new byte[CrashLogRing.HeaderSize + capacity], 0, _memory, CrashLogRing.HeaderSize + capacity);
        Marshal.WriteInt64(_memory, CrashLogRing.MagicOffset, unchecked((long)CrashLogRing.Magic));
        Marshal.WriteInt32(_memory, CrashLogRing.CapacityOffset, capacity);
        _disk = Logger.Listeners.FirstOrDefault(listener => listener is DiskLogListener);
    }

    public IntPtr Address => _memory;

    public int Capacity => _capacity;

    public LogLevel LogLevelFilter => _disk?.LogLevelFilter ?? LogLevel.All;

    public static CrashLogMemory Start(int capacity = CrashLogRing.DefaultCapacity)
    {
        var ring = new CrashLogMemory(capacity);
        Logger.Listeners.Add(ring);
        return ring;
    }

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if (eventArgs == null || (eventArgs.Level & LogLevelFilter) == 0)
        {
            return;
        }

        Write(eventArgs.ToStringLine());
    }

    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n");
        lock (_gate)
        {
            if (_memory == IntPtr.Zero)
            {
                return;
            }

            var offset = bytes.Length > _capacity ? bytes.Length - _capacity : 0;
            var count = bytes.Length - offset;
            var position = (int)(_written % _capacity);
            var first = Math.Min(count, _capacity - position);
            Marshal.Copy(bytes, offset, _memory + CrashLogRing.HeaderSize + position, first);
            if (count > first)
            {
                Marshal.Copy(bytes, offset + first, _memory + CrashLogRing.HeaderSize, count - first);
            }

            _written += count;
            Marshal.WriteInt64(_memory, CrashLogRing.WrittenOffset, _written);
        }
    }

    public void Dispose()
    {
        Logger.Listeners.Remove(this);
    }
}
