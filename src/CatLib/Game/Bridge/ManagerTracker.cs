using System;
using CatLib.Game.Events;
using CatLib.Il2Cpp;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes;

namespace CatLib.Game.Bridge;

internal sealed class ManagerTracker<TManager> : IManagerTracker where TManager : Il2CppObjectBase
{
    private readonly Func<bool> _hasInstance;
    private readonly Func<TManager> _getInstance;
    private readonly Action<TManager, Il2CppEventBindings> _bind;
    private readonly CatLogger _log;
    private Il2CppEventBindings _bindings;
    private IntPtr _attachedPointer;

    public ManagerTracker(string name, Func<bool> hasInstance, Func<TManager> getInstance, Action<TManager, Il2CppEventBindings> bind, int expectedEventCount, CatLogger log)
    {
        Name = name;
        ExpectedEventCount = expectedEventCount;
        _hasInstance = hasInstance;
        _getInstance = getInstance;
        _bind = bind;
        _log = log;
    }

    public string Name { get; }

    public bool IsAttached => _attachedPointer != IntPtr.Zero;

    public int BoundEventCount => _bindings?.Count ?? 0;

    public int ExpectedEventCount { get; }

    public string LastError { get; private set; }

    public void Update()
    {
        var instance = Resolve(out var pointer);
        if (pointer == _attachedPointer)
        {
            return;
        }

        Detach();

        if (instance != null)
        {
            Attach(instance, pointer);
        }
    }

    private TManager Resolve(out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        try
        {
            if (!_hasInstance())
            {
                return null;
            }

            var instance = _getInstance();
            if (instance == null || instance.WasCollected)
            {
                return null;
            }

            pointer = instance.Pointer;
            return instance;
        }
        catch (Exception exception)
        {
            ReportError($"Failed to resolve {Name}: {exception.Message}");
            return null;
        }
    }

    private void Attach(TManager instance, IntPtr pointer)
    {
        _attachedPointer = pointer;
        _bindings = new Il2CppEventBindings();

        try
        {
            _bind(instance, _bindings);
        }
        catch (Exception exception)
        {
            ReportError($"Binder of {Name} failed: {exception}");
            GameEventStream.Publish(ManagerRegistry.AttachedEventName, $"manager={Name} events={_bindings.Count} failed=binder");
            return;
        }

        if (_bindings.Failures.Count > 0)
        {
            ReportError($"{_bindings.Failures.Count} event binding(s) of {Name} failed:{Environment.NewLine}{string.Join(Environment.NewLine, _bindings.Failures)}");
        }
        else
        {
            LastError = null;
        }

        _log.Info($"Attached to {Name} (0x{pointer.ToInt64():X}), {_bindings.Count} events bound, {_bindings.Failures.Count} failed");
        GameEventStream.Publish(ManagerRegistry.AttachedEventName, $"manager={Name} events={_bindings.Count} failed={_bindings.Failures.Count}");
    }

    private void Detach()
    {
        if (!IsAttached)
        {
            return;
        }

        _bindings?.Clear(_log);
        _bindings = null;
        _attachedPointer = IntPtr.Zero;
        _log.Info($"Detached from {Name}");
        GameEventStream.Publish(ManagerRegistry.DetachedEventName, $"manager={Name}");
    }

    private void ReportError(string message)
    {
        if (message == LastError)
        {
            return;
        }

        LastError = message;
        _log.Error(message);
    }
}
