using Notio.Common.Exceptions;
using Notio.Shared.Extensions;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Notio.Shared;

/// <summary>
/// Provides a standard way to convert strings to different types.
/// </summary>
public static class FromString
{
    private static readonly MethodInfo ConvertFromInvariantStringMethod
        = new Func<string, object?>(TypeDescriptor.GetConverter(typeof(int)).ConvertFromInvariantString).Method;

    private static readonly MethodInfo TryConvertToInternalMethod
        = typeof(FromString).GetMethod(nameof(TryConvertToInternal), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Method '{nameof(TryConvertToInternal)}' not found.");

    private static readonly ConcurrentDictionary<Type, Func<string[], (bool Success, object Result)>> GenericTryConvertToMethods = new();

    /// <summary>
    /// Attempts to convert a string to the specified type.
    /// </summary>
    /// <param name="type">The type resulting from the conversion.</param>
    /// <param name="str">The string to convert.</param>
    /// <param name="result">When this method returns <see langword="true" />,
    /// the result of the conversion. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true" /> if the conversion is successful;
    /// otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
    public static bool TryConvertTo(Type type, string str, out object? result)
    {
        var converter = TypeDescriptor.GetConverter(type);
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
    /// <param name="type">The type resulting from the conversion of each
    /// element of <paramref name="strings"/>.</param>
    /// <param name="strings">The array to convert.</param>
    /// <param name="result">When this method returns <see langword="true" />,
    /// the result of the conversion. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true" /> if the conversion is successful;
    /// otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
    public static bool TryConvertTo(Type type, string[] strings, out object? result)
    {
        if (strings == null)
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
    /// Converts a expression, if the type can be converted to string, to a new expression including
    /// the conversion to string.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="str">The string.</param>
    /// <returns>A new expression where the previous expression is converted to string.</returns>
    public static Expression? ConvertExpressionTo(Type type, Expression str)
    {
        var converter = TypeDescriptor.GetConverter(type);

        return converter.CanConvertFrom(typeof(string))
            ? Expression.Convert(
                Expression.Call(Expression.Constant(converter), ConvertFromInvariantStringMethod, str),
                type)
            : null;
    }

    private static Func<string[], (bool Success, object Result)> BuildNonGenericTryConvertLambda(Type type)
    {
        var methodInfo = TryConvertToInternalMethod.MakeGenericMethod(type);
        var parameter = Expression.Parameter(typeof(string[]));
        var body = Expression.Call(methodInfo, parameter);
        var lambda = Expression.Lambda<Func<string[], (bool Success, object Result)>>(body, parameter);
        return lambda.Compile();
    }

    private static (bool Success, object? Result) TryConvertToInternal<TResult>(string[] strings)
    {
        var converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
            return (false, null);

        var result = new TResult[strings.Length];
        var i = 0;

        try
        {
            foreach (string str in strings)
            {
                object convertedValue = converter.ConvertFromInvariantString(str) ??
                    throw new StringConversionException(typeof(TResult),
                    new NullReferenceException("Conversion resulted in null."));

                result[i++] = (TResult)convertedValue;
            }

            return (true, result);
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            return (false, null);
        }
    }

    private static TResult[] ConvertToInternal<TResult>(string[] strings)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        TResult[] result = new TResult[strings.Length];
        int i = 0;

        try
        {
            foreach (string str in strings)
            {
                object convertedValue = converter.ConvertFromInvariantString(str) ??
                    throw new StringConversionException(typeof(TResult),
                    new NullReferenceException("Conversion resulted in null."));

                result[i++] = (TResult)convertedValue;
            }

            return result;
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), e);
        }
    }
}