using System;
using System.Collections.Generic;
using CatLib.Logging;
using CustomStamps.Packs;
using UnityEngine;

namespace CustomStamps.Game;

public sealed class StampSelection
{
    private readonly StampLibrary _library;
    private readonly StampFactory _factory;
    private readonly CatLogger _log;
    private readonly HashSet<string> _unknown = new(StringComparer.Ordinal);

    public StampSelection(StampLibrary library, StampFactory factory, CatLogger log)
    {
        _library = library;
        _factory = factory;
        _log = log;
    }

    public int Added { get; private set; }

    public static StampKind? KindOf(StampCategory category) => category switch
    {
        StampCategory.Decorative => StampKind.Decorative,
        StampCategory.Weight => StampKind.Weight,
        _ => null
    };

    public void AfterPopulate(StampSelectionCategoryInterface category)
    {
        var kind = KindOf(category.LinkedStampCategory);
        if (kind == null)
        {
            return;
        }

        var added = 0;
        foreach (var stamp in _library.Active(kind.Value))
        {
            var data = _factory.Get(stamp, _library.Generation);
            if (data == null)
            {
                continue;
            }

            category.InstantiateStampItem(data);
            added++;
        }

        Added = added;
    }

    public void Refresh()
    {
        if (!Singleton<InterfaceManager>.HasInstance())
        {
            return;
        }

        var ui = Singleton<InterfaceManager>.Instance.StampSelectionInterface;
        if (ui == null || !ui._isInitialized)
        {
            return;
        }

        var selected = ui.CurrentSelectedStampItem;
        var selectedData = selected == null ? null : selected.StampData;
        if (_factory.IsCustom(selectedData) && (!_library.TryGet(selectedData.name, out var stamp) || !stamp.IsActive))
        {
            ui.ClearSelectedStamp();
        }

        var categories = ui._stampSelectionCategories;
        if (categories == null)
        {
            return;
        }

        foreach (var category in new[] { StampCategory.Decorative, StampCategory.Weight })
        {
            if (categories.ContainsKey(category))
            {
                categories[category].Populate();
            }
        }
    }

    public bool PlaceFromNetwork(EntityInteractableStamp stamps, string key, int identifier, Vector3 position, Quaternion rotation, bool fromPlayer)
    {
        if (!StampFiles.IsCustomKey(key))
        {
            return false;
        }

        if (!_library.TryGet(key, out var stamp))
        {
            if (_unknown.Add(key))
            {
                _log.Info($"{key} is a stamp of a pack this game does not have{(_library.IsLoading ? " or has not read yet" : string.Empty)}, it is not shown");
            }

            return true;
        }

        var data = _factory.Get(stamp, _library.Generation);
        if (data == null)
        {
            return true;
        }

        Place(stamps, data, identifier, position, rotation, fromPlayer);
        return true;
    }

    private static void Place(EntityInteractableStamp stamps, StampData data, int identifier, Vector3 position, Quaternion rotation, bool fromPlayer)
    {
        var helper = UnityEngine.Object.Instantiate(data._StampPrefab_k__BackingField, stamps._StampsParent_k__BackingField);
        var transform = helper.transform;
        transform.localRotation = rotation;
        transform.localPosition = position;
        var entity = stamps._LinkedEntity_k__BackingField;
        var properties = entity == null ? null : entity.Properties;
        var multiplier = properties == null ? 1f : properties.StampSizeMultiplier;
        transform.localScale = Vector3.one * multiplier;
        if (Singleton<VFXManager>.HasInstance())
        {
            Singleton<VFXManager>.Instance.PlayStampingVFX(transform.position, transform.rotation);
        }

        if (fromPlayer)
        {
            helper.StartStampingFeedback();
        }

        if (stamps._stampNextIdentifier <= identifier)
        {
            stamps._stampNextIdentifier = identifier + 1;
        }

        var instance = new StampInstance(identifier, helper, data, false);
        if (instance._StampObject_k__BackingField == null)
        {
            instance._StampObject_k__BackingField = helper.gameObject;
        }
        stamps._stampInstances.Add(instance);
        stamps.StampAdded?.Invoke(instance);
        stamps.CheckOnboardingValidation();
    }
}
