using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CatLib.UI;

internal static class UiClone
{
    private static GameObject _staging;

    public static Transform Staging
    {
        get
        {
            if (_staging == null)
            {
                _staging = new GameObject("CatLib.UiStaging");
                _staging.SetActive(false);
                _staging.hideFlags = HideFlags.HideAndDontSave;
            }

            return _staging.transform;
        }
    }

    public static bool IsAlive(Object value) => value != null && !value.WasCollected;

    public static GameObject CloneInactive(GameObject original, string name)
    {
        var clone = Object.Instantiate(original, Staging, false);
        clone.name = name;
        return clone;
    }

    public static void Place(GameObject clone, Transform parent, int siblingIndex)
    {
        clone.transform.SetParent(parent, false);
        if (siblingIndex >= 0)
        {
            clone.transform.SetSiblingIndex(siblingIndex);
        }
    }

    public static int StripLocalization(GameObject root)
    {
        var removed = 0;
        foreach (var localize in root.GetComponentsInChildren<Localize>(true))
        {
            Object.DestroyImmediate(localize);
            removed++;
        }

        foreach (var localize in root.GetComponentsInChildren<LocalizeDropdown>(true))
        {
            Object.DestroyImmediate(localize);
            removed++;
        }

        return removed;
    }

    public static int DisablePersistentListeners(UnityEventBase unityEvent)
    {
        if (unityEvent == null)
        {
            return 0;
        }

        var count = unityEvent.GetPersistentEventCount();
        for (var index = 0; index < count; index++)
        {
            unityEvent.SetPersistentListenerState(index, UnityEventCallState.Off);
        }

        return count;
    }

    public static void DestroyChildren(Transform parent)
    {
        for (var index = parent.childCount - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }
    }

    public static int DestroyComponents<T>(GameObject root) where T : Component
    {
        var removed = 0;
        foreach (var component in root.GetComponentsInChildren<T>(true))
        {
            Object.DestroyImmediate(component);
            removed++;
        }

        return removed;
    }

    public static void SetText(GameObject root, string text)
    {
        var label = root.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }
}
