using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CatLib.Tests.Framework;

public static class TestRegistry
{
    public static IReadOnlyList<TestCase> Discover(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type != null).ToArray();
        }

        return types
            .Where(type => typeof(TestCase).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (TestCase)Activator.CreateInstance(type))
            .OrderBy(test => test.Suite, StringComparer.Ordinal)
            .ThenBy(test => test.Order)
            .ThenBy(test => test.Name, StringComparer.Ordinal)
            .ToList();
    }
}
