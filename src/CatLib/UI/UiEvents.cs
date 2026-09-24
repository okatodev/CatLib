using System;
using Il2CppInterop.Runtime;
using UnityEngine.Events;

namespace CatLib.UI;

internal static class UiEvents
{
    public static void Listen<T>(UnityEvent<T> unityEvent, Action<T> action) =>
        unityEvent.AddListener(DelegateSupport.ConvertDelegate<UnityAction<T>>(action));
}
