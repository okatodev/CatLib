using System;
using System.Globalization;
using System.Text;

namespace CatLib.Tests.Diagnostics.Inspection;

public static class InspectionText
{
    public const string BackingFieldSuffix = "_k__BackingField";
    public const int MaxStringLength = 160;

    public static string MemberName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        if (propertyName.EndsWith(BackingFieldSuffix, StringComparison.Ordinal))
        {
            var core = propertyName.Substring(0, propertyName.Length - BackingFieldSuffix.Length);
            return core.StartsWith("_", StringComparison.Ordinal) ? core.Substring(1) : core;
        }

        return propertyName;
    }

    public static string Clip(string text, int maxLength = MaxStringLength)
    {
        if (text == null)
        {
            return "null";
        }

        var single = text.Replace("\r", "\\r").Replace("\n", "\\n");
        return single.Length <= maxLength ? single : single.Substring(0, maxLength) + "...(" + single.Length.ToString(CultureInfo.InvariantCulture) + " chars)";
    }

    public static string Scalar(object value) => value switch
    {
        null => "null",
        string text => "\"" + Clip(text) + "\"",
        bool flag => flag ? "true" : "false",
        float number => number.ToString("0.###", CultureInfo.InvariantCulture),
        double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        Enum enumValue => enumValue.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    public static bool IsScalar(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);

    public static string Indent(int depth) => new(' ', depth * 2);

    public static void Line(StringBuilder builder, int depth, string text) => builder.Append(Indent(depth)).AppendLine(text);
}
