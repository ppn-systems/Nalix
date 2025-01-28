using Notio.Common.Exceptions;
using Notio.Shared.Extensions;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Notio.Shared;

//
// Summary:
//     Provides a standard way to convert strings to different types.
public static class FromString
{
    private static readonly MethodInfo ConvertFromInvariantStringMethod = new Func<string, object?>(TypeDescriptor.GetConverter(typeof(int)).ConvertFromInvariantString).Method;

    private static readonly MethodInfo TryConvertToInternalMethod = typeof(FromString).GetMethod("TryConvertToInternal", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Method 'TryConvertToInternal' not found.");

    private static readonly MethodInfo ConvertToInternalMethod = typeof(FromString).GetMethod("ConvertToInternal", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Method 'TryConvertToInternal' not found.");

    private static readonly ConcurrentDictionary<Type, Func<string[], (bool Success, object Result)>> GenericTryConvertToMethods = new ConcurrentDictionary<Type, Func<string[], (bool, object)>>();

    private static readonly ConcurrentDictionary<Type, Func<string[], object>> GenericConvertToMethods = new ConcurrentDictionary<Type, Func<string[], object>>();

    //
    // Summary:
    //     Determines whether a string can be converted to the specified type.
    //
    // Parameters:
    //   type:
    //     The type resulting from the conversion.
    //
    // Returns:
    //     true if the conversion is possible; otherwise, false.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     type is null.
    public static bool CanConvertTo(Type type)
    {
        return TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
    }

    //
    // Summary:
    //     Determines whether a string can be converted to the specified type.
    //
    // Type parameters:
    //   TResult:
    //     The type resulting from the conversion.
    //
    // Returns:
    //     true if the conversion is possible; otherwise, false.
    public static bool CanConvertTo<TResult>()
    {
        return TypeDescriptor.GetConverter(typeof(TResult)).CanConvertFrom(typeof(string));
    }

    //
    // Summary:
    //     Attempts to convert a string to the specified type.
    //
    // Parameters:
    //   type:
    //     The type resulting from the conversion.
    //
    //   str:
    //     The string to convert.
    //
    //   result:
    //     When this method returns true, the result of the conversion. This parameter is
    //     passed uninitialized.
    //
    // Returns:
    //     true if the conversion is successful; otherwise, false.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     type is null.
    public static bool TryConvertTo<TResult>(string str, out TResult result)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            result = default!; // Use default! to avoid CS8601
            return false;
        }

        try
        {
            object? convertedResult = converter.ConvertFromInvariantString(str);
            if (convertedResult is null)
            {
                result = default!; // Use default! to avoid CS8601
                return false;
            }
            result = (TResult)convertedResult; // No CS8600 as we checked for null
            return true;
        }
        catch (Exception @this) when (!@this.IsCriticalException())
        {
            result = default!; // Use default! to avoid CS8601
            return false;
        }
    }

    //
    // Summary:
    //     Converts a string to the specified type.
    //
    // Parameters:
    //   type:
    //     The type resulting from the conversion.
    //
    //   str:
    //     The string to convert.
    //
    // Returns:
    //     An instance of type.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     type is null.
    //
    //   T:Swan.StringConversionException:
    //     The conversion was not successful.
    public static object ConvertTo(Type type, string str)
    {
        if (type == null)
        {
            throw new ArgumentNullException("type");
        }

        try
        {
            object? result = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(str);
            if (result == null)
            {
                throw new StringConversionException(type, "Conversion returned null.");
            }
            return result;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(type, ex);
        }
    }

    //
    // Summary:
    //     Converts a string to the specified type.
    //
    // Parameters:
    //   str:
    //     The string to convert.
    //
    // Type parameters:
    //   TResult:
    //     The type resulting from the conversion.
    //
    // Returns:
    //     An instance of TResult.
    //
    // Exceptions:
    //   T:Swan.StringConversionException:
    //     The conversion was not successful.
    public static TResult ConvertTo<TResult>(string str)
    {
        try
        {
            object? result = TypeDescriptor.GetConverter(typeof(TResult)).ConvertFromInvariantString(str) ??
                throw new StringConversionException(typeof(TResult), "Conversion returned null.");

            return (TResult)result;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }

    //
    // Summary:
    //     Attempts to convert an array of strings to an array of the specified type.
    //
    // Parameters:
    //   type:
    //     The type resulting from the conversion of each element of strings.
    //
    //   strings:
    //     The array to convert.
    //
    //   result:
    //     When this method returns true, the result of the conversion. This parameter is
    //     passed uninitialized.
    //
    // Returns:
    //     true if the conversion is successful; otherwise, false.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     type is null.
    public static bool TryConvertTo(Type type, string[] strings, out object? result)
    {
        if (strings == null)
        {
            result = null;
            return false;
        }

        (bool, object) tuple = GenericTryConvertToMethods.GetOrAdd(type, BuildNonGenericTryConvertLambda)(strings);
        bool item = tuple.Item1;
        object item2 = tuple.Item2;
        result = item2;
        return item;
    }

    //
    // Summary:
    //     Attempts to convert an array of strings to an array of the specified type.
    //
    // Parameters:
    //   strings:
    //     The array to convert.
    //
    //   result:
    //     When this method returns true, the result of the conversion. This parameter is
    //     passed uninitialized.
    //
    // Type parameters:
    //   TResult:
    //     The type resulting from the conversion of each element of strings.
    //
    // Returns:
    //     true if the conversion is successful; otherwise, false.
    public static bool TryConvertTo<TResult>(string[] strings, out TResult[]? result)
    {
        if (strings == null)
        {
            result = null;
            return false;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            result = null;
            return false;
        }

        try
        {
            result = new TResult[strings.Length];
            int num = 0;
            foreach (string text in strings)
            {
                object? convertedResult = converter.ConvertFromInvariantString(text);
                if (convertedResult is null)
                {
                    result = null;
                    return false;
                }
                result[num++] = (TResult)convertedResult;
            }

            return true;
        }
        catch (Exception @this) when (!@this.IsCriticalException())
        {
            result = null;
            return false;
        }
    }

    //
    // Summary:
    //     Converts an array of strings to an array of the specified type.
    //
    // Parameters:
    //   type:
    //     The type resulting from the conversion of each element of strings.
    //
    //   strings:
    //     The array to convert.
    //
    // Returns:
    //     An array of type.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     type is null.
    //
    //   T:Swan.StringConversionException:
    //     The conversion of at least one of the elements of stringswas not successful.
    public static object? ConvertTo(Type type, string[] strings)
    {
        if (strings == null)
        {
            return null;
        }

        return GenericConvertToMethods.GetOrAdd(type, BuildNonGenericConvertLambda)(strings);
    }

    //
    // Summary:
    //     Converts an array of strings to an array of the specified type.
    //
    // Parameters:
    //   strings:
    //     The array to convert.
    //
    // Type parameters:
    //   TResult:
    //     The type resulting from the conversion of each element of strings.
    //
    // Returns:
    //     An array of TResult.
    //
    // Exceptions:
    //   T:Swan.StringConversionException:
    //     The conversion of at least one of the elements of stringswas not successful.
    public static TResult[]? ConvertTo<TResult>(string[] strings)
    {
        if (strings == null)
        {
            return null;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        TResult[] array = new TResult[strings.Length];
        int num = 0;
        try
        {
            foreach (string text in strings)
            {
                array[num++] = (TResult)converter.ConvertFromInvariantString(text);
            }

            return array;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }

    //
    // Summary:
    //     Converts a expression, if the type can be converted to string, to a new expression
    //     including the conversion to string.
    //
    // Parameters:
    //   type:
    //     The type.
    //
    //   str:
    //     The string.
    //
    // Returns:
    //     A new expression where the previous expression is converted to string.
    public static Expression? ConvertExpressionTo(Type type, Expression str)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(type);
        if (!converter.CanConvertFrom(typeof(string)))
        {
            return null;
        }

        return Expression.Convert(Expression.Call(Expression.Constant(converter), ConvertFromInvariantStringMethod, str), type);
    }

    private static Func<string[], (bool Success, object Result)> BuildNonGenericTryConvertLambda(Type type)
    {
        MethodInfo method = TryConvertToInternalMethod.MakeGenericMethod(type);
        ParameterExpression parameterExpression = Expression.Parameter(typeof(string[]));
        return Expression.Lambda<Func<string[], (bool, object)>>(Expression.Call(method, parameterExpression), new ParameterExpression[1] { parameterExpression }).Compile();
    }

    private static (bool Success, object? Result) TryConvertToInternal<TResult>(string[] strings)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        if (!converter.CanConvertFrom(typeof(string)))
        {
            return (false, null);
        }

        TResult[] array = new TResult[strings.Length];
        int num = 0;
        try
        {
            foreach (string text in strings)
            {
                object? convertedResult = converter.ConvertFromInvariantString(text);
                if (convertedResult is null)
                {
                    return (false, null);
                }
                array[num++] = (TResult)convertedResult; // No CS8600 as we checked for null
            }

            return (true, array);
        }
        catch (Exception @this) when (!@this.IsCriticalException())
        {
            return (false, null);
        }
    }

    private static Func<string[], object> BuildNonGenericConvertLambda(Type type)
    {
        MethodInfo method = ConvertToInternalMethod.MakeGenericMethod(type);
        ParameterExpression parameterExpression = Expression.Parameter(typeof(string[]));
        return Expression.Lambda<Func<string[], object>>(Expression.Call(method, parameterExpression), new ParameterExpression[1] { parameterExpression }).Compile();
    }

    private static object ConvertToInternal<TResult>(string[] strings)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
        TResult[] array = new TResult[strings.Length];
        int num = 0;
        try
        {
            foreach (string text in strings)
            {
                object? convertedResult = converter.ConvertFromInvariantString(text) ??
                    throw new StringConversionException(typeof(TResult), "Conversion returned null.");

                array[num++] = (TResult)convertedResult;
            }

            return array;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            throw new StringConversionException(typeof(TResult), ex);
        }
    }
}