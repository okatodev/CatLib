using System;
using System.Collections.Generic;
using System.Linq;

namespace CatLib.Tests.Framework;

public static class Assert
{
    public static void Fail(string message) => throw new AssertionException(message);

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            Fail(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            Fail(message);
        }
    }

    public static void NotNull(object value, string what)
    {
        if (value == null)
        {
            Fail($"{what} is null");
        }
    }

    public static void NotEmpty(string value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail($"{what} is empty");
        }
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            Fail($"{what}: expected <{InvariantFormat.Value(expected)}>, actual <{InvariantFormat.Value(actual)}>");
        }
    }

    public static void NotEqual<T>(T unexpected, T actual, string what)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            Fail($"{what}: value must differ from <{InvariantFormat.Value(unexpected)}>");
        }
    }

    public static void AtLeast(long minimum, long actual, string what)
    {
        if (actual < minimum)
        {
            Fail($"{what}: expected at least <{InvariantFormat.Value(minimum)}>, actual <{InvariantFormat.Value(actual)}>");
        }
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();
        if (!expectedList.SequenceEqual(actualList))
        {
            Fail($"{what}: expected [{string.Join(", ", expectedList.Select(item => InvariantFormat.Value(item)))}], actual [{string.Join(", ", actualList.Select(item => InvariantFormat.Value(item)))}]");
        }
    }

    public static void Null(object value, string what)
    {
        if (value != null)
        {
            Fail($"{what}: expected null, actual <{InvariantFormat.Value(value)}>");
        }
    }

    public static TException Throws<TException>(Action action, string what) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            Fail($"{what}: expected {typeof(TException).Name}, actual {exception.GetType().Name}");
        }

        Fail($"{what}: expected {typeof(TException).Name}, nothing was thrown");
        return null;
    }
}
