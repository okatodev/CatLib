using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace CatLib.UI;

internal sealed class SettingPresentation
{
    public const int FractionDigits = 2;

    private static readonly HashSet<Type> WholeTypes = new()
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong)
    };

    private static readonly HashSet<Type> FractionTypes = new()
    {
        typeof(float), typeof(double), typeof(decimal)
    };

    private SettingPresentation(Type valueType, ControlKind kind)
    {
        ValueType = valueType;
        Kind = kind;
    }

    public Type ValueType { get; }

    public ControlKind Kind { get; }

    public float Min { get; private set; }

    public float Max { get; private set; }

    public bool WholeNumbers { get; private set; }

    public IReadOnlyList<object> Choices { get; private set; } = Array.Empty<object>();

    public IReadOnlyList<string> ChoiceLabels { get; private set; } = Array.Empty<string>();

    public static bool IsNumeric(Type type) => WholeTypes.Contains(type) || FractionTypes.Contains(type);

    public static SettingPresentation For(Type valueType, AcceptableValueBase acceptableValues)
    {
        if (valueType == typeof(bool))
        {
            return new SettingPresentation(valueType, ControlKind.Toggle);
        }

        var listed = ListedValues(acceptableValues);
        if (listed != null && listed.Count > 0)
        {
            return Dropdown(valueType, listed, listed.Select(value => TomlTypeConverter.ConvertToString(value, valueType)).ToList());
        }

        if (valueType.IsEnum && !valueType.IsDefined(typeof(FlagsAttribute), false))
        {
            var values = Enum.GetValues(valueType).Cast<object>().ToList();
            return Dropdown(valueType, values, values.Select(value => LabelFormatter.Prettify(value.ToString())).ToList());
        }

        if (IsNumeric(valueType) && TryRange(acceptableValues, out var min, out var max))
        {
            return new SettingPresentation(valueType, ControlKind.Slider)
            {
                Min = min,
                Max = max,
                WholeNumbers = WholeTypes.Contains(valueType)
            };
        }

        return new SettingPresentation(valueType, ControlKind.Text);
    }

    public float ToSlider(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);

    public object FromSlider(float value)
    {
        var rounded = WholeNumbers ? Math.Round(value) : Math.Round(value, FractionDigits);
        return Convert.ChangeType(rounded, ValueType, CultureInfo.InvariantCulture);
    }

    public string FormatSlider(float value) =>
        WholeNumbers
            ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
            : Math.Round(value, FractionDigits).ToString("0.##", CultureInfo.InvariantCulture);

    public int IndexOf(object value)
    {
        for (var index = 0; index < Choices.Count; index++)
        {
            if (Equals(Choices[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private static SettingPresentation Dropdown(Type valueType, IReadOnlyList<object> values, IReadOnlyList<string> labels) =>
        new(valueType, ControlKind.Dropdown)
        {
            Choices = values,
            ChoiceLabels = labels
        };

    private static IReadOnlyList<object> ListedValues(AcceptableValueBase acceptableValues)
    {
        if (acceptableValues == null)
        {
            return null;
        }

        var type = acceptableValues.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
        {
            return null;
        }

        var array = type.GetProperty(nameof(AcceptableValueList<int>.AcceptableValues))?.GetValue(acceptableValues) as Array;
        return array?.Cast<object>().ToList();
    }

    private static bool TryRange(AcceptableValueBase acceptableValues, out float min, out float max)
    {
        min = 0f;
        max = 0f;
        if (acceptableValues == null)
        {
            return false;
        }

        var type = acceptableValues.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>))
        {
            return false;
        }

        min = Convert.ToSingle(type.GetProperty(nameof(AcceptableValueRange<int>.MinValue))?.GetValue(acceptableValues), CultureInfo.InvariantCulture);
        max = Convert.ToSingle(type.GetProperty(nameof(AcceptableValueRange<int>.MaxValue))?.GetValue(acceptableValues), CultureInfo.InvariantCulture);
        return max > min;
    }
}
