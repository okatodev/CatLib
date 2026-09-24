using System;
using System.Collections.Generic;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace CatLib.Il2Cpp;

public sealed class Il2CppEventBindings
{
    private readonly List<Binding> _bindings = new();
    private readonly List<string> _failures = new();

    public int Count => _bindings.Count;

    public IReadOnlyList<string> Failures => _failures;

    public bool Add<THandler>(string eventName, Delegate handler, Action<THandler> add, Action<THandler> remove)
        where THandler : Il2CppObjectBase
    {
        try
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var converted = DelegateSupport.ConvertDelegate<THandler>(handler);
            if (converted == null)
            {
                throw new InvalidOperationException($"Could not convert handler to {typeof(THandler).FullName}");
            }

            add(converted);
            _bindings.Add(new Binding(eventName, () => remove(converted), converted, handler));
            return true;
        }
        catch (Exception exception)
        {
            _failures.Add($"{eventName}: {exception}");
            return false;
        }
    }

    public void Clear(CatLogger log)
    {
        for (var index = _bindings.Count - 1; index >= 0; index--)
        {
            var binding = _bindings[index];
            try
            {
                binding.Remove();
            }
            catch (Exception exception)
            {
                log?.Warning($"Failed to unbind {binding.EventName}: {exception.Message}");
            }
        }

        _bindings.Clear();
        _failures.Clear();
    }

    private sealed class Binding
    {
        public Binding(string eventName, Action remove, Il2CppObjectBase nativeHandler, Delegate managedHandler)
        {
            EventName = eventName;
            Remove = remove;
            NativeHandler = nativeHandler;
            ManagedHandler = managedHandler;
        }

        public string EventName { get; }

        public Action Remove { get; }

        public Il2CppObjectBase NativeHandler { get; }

        public Delegate ManagedHandler { get; }
    }
}
