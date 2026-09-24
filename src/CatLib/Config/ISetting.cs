using System;
using BepInEx.Configuration;

namespace CatLib.Config;

public interface ISetting
{
    CatSettings Owner { get; }

    ConfigEntryBase EntryBase { get; }

    string Id { get; }

    string Section { get; }

    string Key { get; }

    SettingScope Scope { get; }

    string MenuLabel { get; }

    bool IsHiddenInMenu { get; }

    bool IsOverridden { get; }

    object BoxedOverride { get; }

    bool IsRestartRequired { get; }

    bool IsRestartPending { get; }

    Type ValueType { get; }

    object BoxedValue { get; }

    object BoxedLocalValue { get; }
}
