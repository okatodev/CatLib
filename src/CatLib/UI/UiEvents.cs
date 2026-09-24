using System;
using Il2CppInterop.Runtime;
using UnityEngine.Events;

namespace CatLib.UI;

internal static class UiEvents
{
    public static void Listen<T>(UnityEvent<T> unityEvent, Action<T> action) =>
        unityEvent.AddListener(DelegateSupport.ConvertDelegate<UnityAction<T>>(action));

    public static void Listen(UnityEvent unityEvent, Action action) =>
        unityEvent.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));

    public static Il2CppSystem.Action ToIl2Cpp(Action action) =>
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action);
}
