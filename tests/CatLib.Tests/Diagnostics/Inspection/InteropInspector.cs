using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace CatLib.Tests.Diagnostics.Inspection;

public sealed class InteropInspector
{
    public const int MaxDepth = 4;
    public const int MaxGridSide = 64;
    public const int MaxItems = 16;

    private static readonly Dictionary<Type, PropertyInfo[]> Cache = new();

    private readonly Assembly _gameAssembly;
    private readonly List<TextAsset> _textAssets = new();
    private readonly List<Component> _referencedComponents = new();

    public InteropInspector(Assembly gameAssembly)
    {
        _gameAssembly = gameAssembly;
    }

    public IReadOnlyList<TextAsset> TextAssets => _textAssets;

    public IReadOnlyList<Component> ReferencedComponents => _referencedComponents;

    public Type ManagedType(Il2CppSystem.Object value)
    {
        var name = value.GetIl2CppType().FullName;
        return _gameAssembly.GetType(name) ?? typeof(UnityEngine.Object).Assembly.GetType(name);
    }

    public object Wrap(Il2CppSystem.Object value, Type managedType)
    {
        if (managedType == null || !typeof(Il2CppObjectBase).IsAssignableFrom(managedType))
        {
            return value;
        }

        return Activator.CreateInstance(managedType, value.Pointer);
    }

    public static PropertyInfo[] FieldProperties(Type type)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(type, out var cached))
            {
                return cached;
            }
        }

        var result = new List<PropertyInfo>();
        for (var current = type; current != null && current != typeof(Il2CppObjectBase) && current != typeof(object); current = current.BaseType)
        {
            if (current.Assembly != type.Assembly)
            {
                break;
            }

            foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length != 0 || property.GetMethod == null)
                {
                    continue;
                }

                var pointer = current.GetField("NativeFieldInfoPtr_" + property.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (pointer != null)
                {
                    result.Add(property);
                }
            }
        }

        var array = result.ToArray();
        lock (Cache)
        {
            Cache[type] = array;
        }

        return array;
    }

    public void WriteFields(StringBuilder builder, object target, int depth)
    {
        foreach (var property in FieldProperties(target.GetType()))
        {
            string text;
            try
            {
                text = Format(property.GetValue(target), depth);
            }
            catch (Exception exception)
            {
                text = "<error: " + (exception.InnerException ?? exception).GetType().Name + ">";
            }

            InspectionText.Line(builder, depth, InspectionText.MemberName(property.Name) + " = " + text);
        }
    }

    public string Format(object value, int depth)
    {
        if (value == null)
        {
            return "null";
        }

        var type = value.GetType();
        if (InspectionText.IsScalar(type))
        {
            return InspectionText.Scalar(value);
        }

        switch (value)
        {
            case Vector2 vector:
                return $"({InspectionText.Scalar(vector.x)}, {InspectionText.Scalar(vector.y)})";
            case Vector3 vector:
                return $"({InspectionText.Scalar(vector.x)}, {InspectionText.Scalar(vector.y)}, {InspectionText.Scalar(vector.z)})";
            case Vector2Int vector:
                return $"({vector.x}, {vector.y})";
            case Vector3Int vector:
                return $"({vector.x}, {vector.y}, {vector.z})";
            case Quaternion rotation:
                var euler = rotation.eulerAngles;
                return $"euler({InspectionText.Scalar(euler.x)}, {InspectionText.Scalar(euler.y)}, {InspectionText.Scalar(euler.z)})";
            case Color color:
                return $"rgba({InspectionText.Scalar(color.r)}, {InspectionText.Scalar(color.g)}, {InspectionText.Scalar(color.b)}, {InspectionText.Scalar(color.a)})";
            case UnityEngine.Object unityObject:
                return FormatUnityObject(unityObject);
        }

        if (TryCollection(value, out var items, out var count))
        {
            return FormatItems(items, count, depth);
        }

        if (value is Il2CppObjectBase nested)
        {
            return FormatNested(nested, depth);
        }

        if (type.IsValueType)
        {
            return FormatStruct(value, depth);
        }

        return InspectionText.Clip(value.ToString());
    }

    private string FormatUnityObject(UnityEngine.Object unityObject)
    {
        if (unityObject == null || unityObject.WasCollected)
        {
            return "null (destroyed)";
        }

        var typeName = unityObject.GetIl2CppType().Name;
        var component = unityObject.TryCast<Component>();
        if (component != null && _gameAssembly.GetType(unityObject.GetIl2CppType().FullName) != null && !_referencedComponents.Any(existing => existing.Pointer == component.Pointer))
        {
            _referencedComponents.Add(component);
        }

        var text = unityObject.TryCast<TextAsset>();
        if (text != null && !_textAssets.Any(existing => existing.Pointer == text.Pointer))
        {
            _textAssets.Add(text);
        }

        return $"\"{InspectionText.Clip(unityObject.name, 80)}\" <{typeName}>";
    }

    private string FormatItems(IEnumerable items, int count, int depth)
    {
        var parts = new List<string>();
        foreach (var item in items)
        {
            if (parts.Count >= MaxItems)
            {
                break;
            }

            parts.Add(Format(item, depth + 1));
        }

        var more = count > MaxItems ? $", ...+{count - MaxItems}" : string.Empty;
        return $"[{count}] {{ {string.Join(", ", parts)}{more} }}";
    }

    private string FormatNested(Il2CppObjectBase nested, int depth)
    {
        var il2Cpp = nested.TryCast<Il2CppSystem.Object>();
        if (il2Cpp != null && MultiDimensionalArray.TryFormat(il2Cpp, MaxGridSide, out var grid))
        {
            return grid;
        }

        var managedType = il2Cpp == null ? nested.GetType() : ManagedType(il2Cpp) ?? nested.GetType();
        if (depth >= MaxDepth)
        {
            return "<" + managedType.Name + ">";
        }

        var target = il2Cpp == null ? nested : Wrap(il2Cpp, managedType);
        var properties = FieldProperties(target.GetType());
        if (properties.Length == 0)
        {
            return "<" + managedType.Name + ">";
        }

        var parts = new List<string>();
        foreach (var property in properties)
        {
            string text;
            try
            {
                text = Format(property.GetValue(target), depth + 1);
            }
            catch (Exception exception)
            {
                text = "<error: " + (exception.InnerException ?? exception).GetType().Name + ">";
            }

            parts.Add(InspectionText.MemberName(property.Name) + "=" + text);
        }

        return managedType.Name + " { " + string.Join("; ", parts) + " }";
    }

    private string FormatStruct(object value, int depth)
    {
        var fields = value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        if (fields.Length == 0 || depth >= MaxDepth)
        {
            return InspectionText.Clip(value.ToString());
        }

        return value.GetType().Name + " { " + string.Join("; ", fields.Select(field => field.Name + "=" + Format(field.GetValue(value), depth + 1))) + " }";
    }

    private static bool TryCollection(object value, out IEnumerable items, out int count)
    {
        items = null;
        count = 0;
        for (var current = value.GetType(); current != null; current = current.BaseType)
        {
            if (!current.IsGenericType)
            {
                continue;
            }

            var definition = current.GetGenericTypeDefinition().FullName ?? string.Empty;
            if (definition.StartsWith("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase`1", StringComparison.Ordinal) && value is IEnumerable enumerable)
            {
                count = (int)(current.GetProperty("Length")?.GetValue(value) ?? 0);
                items = enumerable;
                return true;
            }

            if (definition.StartsWith("Il2CppSystem.Collections.Generic.List`1", StringComparison.Ordinal))
            {
                var countProperty = current.GetProperty("Count");
                var indexer = current.GetMethod("get_Item", new[] { typeof(int) });
                if (countProperty == null || indexer == null)
                {
                    return false;
                }

                var total = (int)countProperty.GetValue(value);
                count = total;
                items = Indexed(value, indexer, Math.Min(total, MaxItems));
                return true;
            }

            if (definition.StartsWith("Il2CppSystem.Collections.Generic.", StringComparison.Ordinal))
            {
                var countProperty = current.GetProperty("Count");
                var enumeratorMethod = current.GetMethod("GetEnumerator", Type.EmptyTypes);
                if (countProperty == null || enumeratorMethod == null)
                {
                    return false;
                }

                count = (int)countProperty.GetValue(value);
                items = Enumerated(enumeratorMethod.Invoke(value, null), MaxItems);
                return true;
            }
        }

        return false;
    }

    private static IEnumerable Enumerated(object enumerator, int take)
    {
        if (enumerator == null)
        {
            yield break;
        }

        var moveNext = enumerator.GetType().GetMethod("MoveNext", Type.EmptyTypes);
        var current = enumerator.GetType().GetProperty("Current");
        if (moveNext == null || current == null)
        {
            yield break;
        }

        for (var index = 0; index < take && (bool)moveNext.Invoke(enumerator, null); index++)
        {
            yield return current.GetValue(enumerator);
        }
    }

    private static IEnumerable Indexed(object list, MethodInfo indexer, int take)
    {
        for (var index = 0; index < take; index++)
        {
            yield return indexer.Invoke(list, new object[] { index });
        }
    }
}
