using Notio.Common.Exceptions;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Notio.Lite;

/// <summary>
/// A static class to handle conversions from strings to specific types.
/// </summary>
public static class FromString
{
    private static readonly MethodInfo _convertFromInvariantStringMethod = typeof(TypeConverter).GetMethod("ConvertFromInvariantString", [typeof(string)])!;
    private static readonly ConcurrentDictionary<Type, Func<string[], (bool Success, object? Result)>> _genericTryConvertToMethods = new();
    private static readonly ConcurrentDictionary<Type, Func<string[], object>> _genericConvertToMethods = new();

    /// <summary>
    /// Checks if the specified type can be converted from a string.
    /// </summary>
    /// <param name="type">The target type to check.</param>
    /// <returns>True if conversion is possible; otherwise, false.</returns>
    public static bool CanConvertTo(Type type) => TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));

    /// <summary>
    /// Checks if the specified generic type can be converted from a string.
    /// </summary>
    /// <typeparam name="TResult">The target type to check.</typeparam>
    /// <returns>True if conversion is possible; otherwise, false.</returns>
    public static bool CanConvertTo<TResult>() => TypeDescriptor.GetConverter(typeof(TResult)).CanConvertFrom(typeof(string));

    /// <summary>
    /// Tries to convert a string to the specified generic type.
    /// </summary>
    /// <typeparam name="TResult">The target type to convert to.</typeparam>
    /// <param name="str">The string to convert.</param>
    /// <param name="result">The converted result if successful; otherwise, the default value of the target type.</param>
    /// <returns>True if conversion is successful; otherwise, false.</returns>
    public static bool TryConvertTo<TResult>(string str, out TResult result)
    {
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            result = default!;
            return false;
        }

        try
        {
            var convertedResult = converter.ConvertFromInvariantString(str);
            if (convertedResult is null)
            {
                result = default!;
                return false;
            }
            result = (TResult)convertedResult;
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    /// <summary>
    /// Converts a string to a specified type.
    /// </summary>
    /// <param name="type">The target type to convert to.</param>
    /// <param name="str">The string to convert.</param>
    /// <returns>The converted object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the type is null.</exception>
    /// <exception cref="StringConversionException">Thrown when the conversion fails.</exception>
    public static object ConvertTo(Type type, string str)
    {
        ArgumentNullException.ThrowIfNull(type);

        var converter = TypeDescriptor.GetConverter(type);
        try
        {
            var result = converter.ConvertFromInvariantString(str) ?? throw new StringConversionException(type, "Conversion returned null.");
            return result;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(type, ex);
        }
    }

    /// <summary>
    /// Converts a string to a specified generic type.
    /// </summary>
    /// <typeparam name="TResult">The target type to convert to.</typeparam>
    /// <param name="str">The string to convert.</param>
    /// <returns>The converted object of type <typeparamref name="TResult"/>.</returns>
    /// <exception cref="StringConversionException">Thrown when the conversion fails.</exception>
    public static TResult ConvertTo<TResult>(string str)
    {
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        try
        {
            var result = converter.ConvertFromInvariantString(str) ?? throw new StringConversionException(typeof(TResult), "Conversion returned null.");
            return (TResult)result;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }

    public static bool TryConvertTo(Type type, string[] strings, out object? result)
    {
        if (strings == null)
        {
            result = null;
            return false;
        }

        var method = _genericTryConvertToMethods.GetOrAdd(type, BuildNonGenericTryConvertLambda);
        var (success, res) = method(strings);
        result = res;
        return success;
    }

    public static bool TryConvertTo<TResult>(string[] strings, out TResult[]? result)
    {
        if (strings == null)
        {
            result = null;
            return false;
        }

        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            result = null;
            return false;
        }

        try
        {
            result = Array.ConvertAll(strings, str => (TResult)converter.ConvertFromInvariantString(str)!);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public static object? ConvertTo(Type type, string[] strings)
    {
        if (strings == null) return null;
        var method = _genericConvertToMethods.GetOrAdd(type, BuildNonGenericConvertLambda);
        return method(strings);
    }

    public static TResult[]? ConvertTo<TResult>(string[] strings)
    {
        if (strings == null) return null;
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        try
        {
            return Array.ConvertAll(strings, str => (TResult)converter.ConvertFromInvariantString(str)!);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }

    public static Expression? ConvertExpressionTo(Type type, Expression str)
    {
        var converter = TypeDescriptor.GetConverter(type);
        if (!converter.CanConvertFrom(typeof(string))) return null;
        return Expression.Convert(Expression.Call(Expression.Constant(converter), _convertFromInvariantStringMethod, str), type);
    }

    private static Func<string[], (bool Success, object? Result)> BuildNonGenericTryConvertLambda(Type type)
    {
        var method = typeof(FromString).GetMethod(nameof(TryConvertToInternal), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(type);
        var parameter = Expression.Parameter(typeof(string[]));
        var lambda = Expression.Lambda<Func<string[], (bool, object?)>>(Expression.Call(method, parameter), parameter);
        return lambda.Compile();
    }

    private static Func<string[], object> BuildNonGenericConvertLambda(Type type)
    {
        var method = typeof(FromString).GetMethod(nameof(ConvertToInternal), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(type);
        var parameter = Expression.Parameter(typeof(string[]));
        var lambda = Expression.Lambda<Func<string[], object>>(Expression.Call(method, parameter), parameter);
        return lambda.Compile();
    }

    private static (bool Success, object? Result) TryConvertToInternal<TResult>(string[] strings)
    {
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string))) return (false, null);

        try
        {
            var result = Array.ConvertAll(strings, str => (TResult)converter.ConvertFromInvariantString(str)!);
            return (true, result);
        }
        catch
        {
            return (false, null);
        }
    }

    private static TResult[] ConvertToInternal<TResult>(string[] strings)
    {
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        try
        {
            return Array.ConvertAll(strings, str => (TResult)converter.ConvertFromInvariantString(str)!);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }
}