using Nalix.Common.Exceptions;
using Nalix.Extensions;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Network.Web.Internal;

/// <summary>
/// Provides a standard way to convert strings to different types.
/// </summary>
public static class FromString
{
    // This method info is used for converting via TypeConverter in ConvertExpressionTo.
    private static readonly MethodInfo ConvertFromInvariantStringMethod =
        typeof(TypeConverter).GetMethod(nameof(TypeConverter.ConvertFromInvariantString), [typeof(string)])
        ?? throw new InvalidOperationException($"Method '{nameof(TypeConverter.ConvertFromInvariantString)}' not found.");

    // The non-generic internal method for converting arrays.
    private static readonly MethodInfo TryConvertToInternalMethod =
            typeof(FromString).GetMethod(nameof(TryConvertToInternal), BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{nameof(TryConvertToInternal)}' not found.");

    // Caches for compiled lambdas for converting arrays.
    private static readonly ConcurrentDictionary<Type, Func<string[], (bool Success, object Result)>> GenericTryConvertToMethods = new();

    // Caches for TypeConverters to avoid repeated calls to TypeDescriptor.GetConverter.
    private static readonly ConcurrentDictionary<Type, TypeConverter> ConverterCache = new();

    /// <summary>
    /// Attempts to convert a string to the specified type.
    /// </summary>
    /// <param name="type">The target type.</param>
    /// <param name="str">The string to convert.</param>
    /// <param name="result">When this method returns <c>true</c>, the result of the conversion.</param>
    /// <returns><c>true</c> if the conversion is successful; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="type"/> is null.</exception>
    public static bool TryConvertTo(Type type, string str, out object? result)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Get the converter from cache.
        var converter = ConverterCache.GetOrAdd(type, t => TypeDescriptor.GetConverter(t));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            result = null;
            return false;
        }

        try
        {
            result = converter.ConvertFromInvariantString(str);
            return true;
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to convert an array of strings to an array of the specified type.
    /// </summary>
    /// <param name="type">The target type for each element.</param>
    /// <param name="strings">The array of strings to convert.</param>
    /// <param name="result">When this method returns <c>true</c>, the resulting converted array.</param>
    /// <returns><c>true</c> if the conversion is successful; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="type"/> is null.</exception>
    public static bool TryConvertTo(Type type, string[] strings, out object? result)
    {
        if (strings is null)
        {
            result = null;
            return false;
        }

        var method = GenericTryConvertToMethods.GetOrAdd(type, BuildNonGenericTryConvertLambda);
        var (success, methodResult) = method(strings);
        result = methodResult;
        return success;
    }

    /// <summary>
    /// Converts an expression, if the target type can be converted from string, to a new expression including the conversion.
    /// </summary>
    /// <param name="type">The target type.</param>
    /// <param name="str">The expression representing the string.</param>
    /// <returns>A new expression where the previous expression is converted to the target type, or null if conversion is not possible.</returns>
    public static Expression? ConvertExpressionTo(Type type, Expression str)
    {
        ArgumentNullException.ThrowIfNull(type);

        var converter = ConverterCache.GetOrAdd(type, t => TypeDescriptor.GetConverter(t));
        return converter.CanConvertFrom(typeof(string))
            ? Expression.Convert(Expression.Call(Expression.Constant(converter), ConvertFromInvariantStringMethod, str), type)
            : null;
    }

    private static Func<string[], (bool Success, object Result)> BuildNonGenericTryConvertLambda(Type type)
    {
        // Create a generic method for the target type.
        var methodInfo = TryConvertToInternalMethod.MakeGenericMethod(type);
        var parameter = Expression.Parameter(typeof(string[]));
        var body = Expression.Call(methodInfo, parameter);
        var lambda = Expression.Lambda<Func<string[], (bool Success, object Result)>>(body, parameter);
        return lambda.Compile();
    }

    private static (bool Success, object? Result) TryConvertToInternal<TResult>(string[] strings)
    {
        var converter = ConverterCache.GetOrAdd(typeof(TResult), t => TypeDescriptor.GetConverter(t));
        if (!converter.CanConvertFrom(typeof(string)))
            return (false, null);

        var result = new TResult[strings.Length];
        for (int i = 0, len = strings.Length; i < len; i++)
        {
            try
            {
                object convertedValue = converter.ConvertFromInvariantString(strings[i])
                    ?? throw new StringConversionException(typeof(TResult), new NullReferenceException("Conversion resulted in null."));
                result[i] = (TResult)convertedValue;
            }
            catch (Exception e) when (!e.IsCriticalException())
            {
                return (false, null);
            }
        }
        return (true, result);
    }

    private static TResult[] ConvertToInternal<TResult>(string[] strings)
    {
        var converter = ConverterCache.GetOrAdd(typeof(TResult), t => TypeDescriptor.GetConverter(t));
        TResult[] result = new TResult[strings.Length];
        for (int i = 0, len = strings.Length; i < len; i++)
        {
            try
            {
                object convertedValue = converter.ConvertFromInvariantString(strings[i])
                    ?? throw new StringConversionException(typeof(TResult), new NullReferenceException("Conversion resulted in null."));
                result[i] = (TResult)convertedValue;
            }
            catch (Exception e) when (!e.IsCriticalException())
            {
                throw new StringConversionException(typeof(TResult), e);
            }
        }
        return result;
    }
}
