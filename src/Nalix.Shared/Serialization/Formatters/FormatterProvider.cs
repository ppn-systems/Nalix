using Nalix.Shared.Serialization.Formatters.Automatic;
using Nalix.Shared.Serialization.Formatters.Cache;
using Nalix.Shared.Serialization.Formatters.Collections;
using Nalix.Shared.Serialization.Formatters.Primitives;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization.Formatters;

/// <summary>
/// Provides a global registry for registering and retrieving formatters without boxing.
/// </summary>
public static class FormatterProvider
{
    /// <summary>
    /// Initializes the static <see cref="FormatterProvider"/> class by registering formatters.
    /// </summary>
    static FormatterProvider()
    {
        // ============================================================ //
        // Integer types
        Register<System.Char>(new UnmanagedFormatter<System.Char>());
        Register<System.Byte>(new UnmanagedFormatter<System.Byte>());
        Register<System.SByte>(new UnmanagedFormatter<System.SByte>());
        Register<System.Int16>(new UnmanagedFormatter<System.Int16>());
        Register<System.UInt16>(new UnmanagedFormatter<System.UInt16>());
        Register<System.Int32>(new UnmanagedFormatter<System.Int32>());
        Register<System.UInt32>(new UnmanagedFormatter<System.UInt32>());
        Register<System.Int64>(new UnmanagedFormatter<System.Int64>());
        Register<System.UInt64>(new UnmanagedFormatter<System.UInt64>());
        Register<System.Single>(new UnmanagedFormatter<System.Single>());
        Register<System.Double>(new UnmanagedFormatter<System.Double>());
        Register<System.Boolean>(new UnmanagedFormatter<System.Boolean>());
        Register<System.Decimal>(new UnmanagedFormatter<System.Decimal>());

        Register<System.Guid>(new UnmanagedFormatter<System.Guid>());
        Register<System.TimeSpan>(new UnmanagedFormatter<System.TimeSpan>());
        Register<System.DateTime>(new UnmanagedFormatter<System.DateTime>());
        Register<System.DateTimeOffset>(new UnmanagedFormatter<System.DateTimeOffset>());

        // ============================================================ //
        // Integer arrays
        Register<System.Char[]>(new ArrayFormatter<System.Char>());
        Register<System.Byte[]>(new ArrayFormatter<System.Byte>());
        Register<System.SByte[]>(new ArrayFormatter<System.SByte>());
        Register<System.Int16[]>(new ArrayFormatter<System.Int16>());
        Register<System.UInt16[]>(new ArrayFormatter<System.UInt16>());
        Register<System.Int32[]>(new ArrayFormatter<System.Int32>());
        Register<System.UInt32[]>(new ArrayFormatter<System.UInt32>());
        Register<System.Int64[]>(new ArrayFormatter<System.Int64>());
        Register<System.UInt64[]>(new ArrayFormatter<System.UInt64>());
        Register<System.Single[]>(new ArrayFormatter<System.Single>());
        Register<System.Double[]>(new ArrayFormatter<System.Double>());
        Register<System.Boolean[]>(new ArrayFormatter<System.Boolean>());

        // ============================================================ //
        // String
        Register<System.String>(new StringFormatter());

        // ============================================================ //
        // Nullable types
        Register<System.Nullable<System.Char>>(new NullableFormatter<System.Char>());
        Register<System.Nullable<System.Byte>>(new NullableFormatter<System.Byte>());
        Register<System.Nullable<System.SByte>>(new NullableFormatter<System.SByte>());
        Register<System.Nullable<System.Int16>>(new NullableFormatter<System.Int16>());
        Register<System.Nullable<System.UInt16>>(new NullableFormatter<System.UInt16>());
        Register<System.Nullable<System.Int32>>(new NullableFormatter<System.Int32>());
        Register<System.Nullable<System.UInt32>>(new NullableFormatter<System.UInt32>());
        Register<System.Nullable<System.Int64>>(new NullableFormatter<System.Int64>());
        Register<System.Nullable<System.UInt64>>(new NullableFormatter<System.UInt64>());
        Register<System.Nullable<System.Single>>(new NullableFormatter<System.Single>());
        Register<System.Nullable<System.Double>>(new NullableFormatter<System.Double>());
        Register<System.Nullable<System.Decimal>>(new NullableFormatter<System.Decimal>());
        Register<System.Nullable<System.Boolean>>(new NullableFormatter<System.Boolean>());

        // Nullable complex types
        Register<System.Nullable<System.Guid>>(new NullableFormatter<System.Guid>());
        Register<System.Nullable<System.DateTime>>(new NullableFormatter<System.DateTime>());
        Register<System.Nullable<System.DateTimeOffset>>(new NullableFormatter<System.DateTimeOffset>());
        Register<System.Nullable<System.TimeSpan>>(new NullableFormatter<System.TimeSpan>());
    }

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if the provided formatter is null.
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Register<T>(IFormatter<T> formatter)
        => FormatterCache<T>.Formatter = formatter
        ?? throw new System.ArgumentNullException(nameof(formatter));

    /// <summary>
    /// Registers a formatter for complex types, distinguishing between value types and reference types.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the type is unsupported (neither a struct nor a class).
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void RegisterComplex<T>(IFormatter<T> formatter)
    {
        // Check if the type is a value type and not an enum
        System.Type type = typeof(T);

        if (type.IsValueType && !type.IsEnum)
        {
            ComplexTypeCache<T>.Struct = formatter;
            return;
        }
        else if (type.IsClass)
        {
            ComplexTypeCache<T>.Class = formatter;
            return;
        }

        throw new System.InvalidOperationException($"Unsupported type: {type.FullName}");
    }

    /// <summary>
    /// Retrieves the registered formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which to retrieve the formatter.</typeparam>
    /// <returns>The registered formatter for the specified type.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if no formatter is registered for the given type.
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IFormatter<T> Get<T>()
    {
        // Formatter has been cached → return immediately
        if (FormatterCache<T>.Formatter is not null)
        {
            return FormatterCache<T>.Formatter;
        }

        System.Type targetType = typeof(T);
        IFormatter<T>? formatter;

        // ============================================================
        // CASE 1: Nullable struct (int?, DateTime?, ...)
        // ============================================================
        if (targetType.IsGenericType &&
            targetType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
        {
            if (FormatterCache<T>.Formatter is not null)
            {
                return FormatterCache<T>.Formatter;
            }

            var underlying = System.Nullable.GetUnderlyingType(targetType)!;
            var created = (IFormatter<T>)System.Activator
                .CreateInstance(typeof(NullableFormatter<>).MakeGenericType(underlying))!;

            FormatterCache<T>.Formatter ??= created;
            return FormatterCache<T>.Formatter!;
        }

        // ============================================================
        // CASE 2: Reference type (class) → null marker support
        //  - Subtract string because it has its own StringFormatter
        // ============================================================
        if (targetType.IsClass && targetType != typeof(System.String))
        {
            System.Type formatterType = typeof(NullableObjectFormatter<>).MakeGenericType(targetType);

            formatter = (IFormatter<T>)System.Activator.CreateInstance(formatterType)!;
            FormatterCache<T>.Formatter ??= formatter;
            return FormatterCache<T>.Formatter!;
        }

        // ============================================================
        // CASE 3: Enum formatter
        // ============================================================
        formatter = TryCreateEnumFormatter<T>();
        if (formatter is not null)
        {
            FormatterCache<T>.Formatter ??= formatter;
            return FormatterCache<T>.Formatter!;
        }

        // ============================================================
        // CASE 4: Array formatter
        // ============================================================
        formatter = TryCreateArrayFormatter<T>();
        if (formatter is not null)
        {
            FormatterCache<T>.Formatter ??= formatter;
            return FormatterCache<T>.Formatter!;
        }

        // ============================================================
        // CASE 5: List formatter
        // ============================================================
        formatter = TryCreateListFormatter<T>();
        if (formatter is not null)
        {
            FormatterCache<T>.Formatter ??= formatter;
            return FormatterCache<T>.Formatter!;
        }

        // ============================================================
        // CASE 6: Complex type (struct or class has fields/properties)
        // ============================================================
        try
        {
            formatter = GetComplex<T>();
            FormatterCache<T>.Formatter ??= formatter;
            return FormatterCache<T>.Formatter!;
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException(
                $"No formatter registered for type {targetType.FullName} and could not auto-generate one.", ex);
        }
    }

    /// <summary>
    /// Retrieves the formatter for the specified complex type.
    /// </summary>
    /// <typeparam name="T">The type for which to retrieve a formatter.</typeparam>
    /// <returns>The registered formatter for the given type.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if no formatter is registered for the specified type.
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static IFormatter<T> GetComplex<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>()
    {
        IFormatter<T> formatter;
        System.Type type = typeof(T);

        if (System.Nullable.GetUnderlyingType(typeof(T)) is not null)
        {
            throw new System.InvalidOperationException($"Cannot call GetComplex<T>() on Nullable<T>: {typeof(T)}");
        }

        if (type.IsValueType && !type.IsEnum)
        {
            formatter = ComplexTypeCache<T>.Struct;
            if (formatter != null)
            {
                return formatter;
            }

            System.Object? @struct = System.Activator.CreateInstance(typeof(StructFormatter<>)
                                              .MakeGenericType(type)) ??
                                              throw new System.InvalidOperationException(
                                                $"Failed to create instance of StructFormatter<{type.Name}>.");

            RegisterComplex((IFormatter<T>)@struct);
            return ComplexTypeCache<T>.Struct;
        }
        else if (type.IsClass)
        {
            formatter = ComplexTypeCache<T>.Class;
            if (formatter != null)
            {
                return formatter;
            }

            System.Object? @object = System.Activator.CreateInstance(typeof(ObjectFormatter<>)
                                              .MakeGenericType(type)) ??
                                              throw new System.InvalidOperationException(
                                                  $"Failed to create instance of ObjectFormatter<{type.Name}>.");

            RegisterComplex((IFormatter<T>)@object);
            return ComplexTypeCache<T>.Class;
        }

        throw new System.InvalidOperationException($"No formatter registered for type {typeof(T)}.");
    }

    #region Private Methods

    private static EnumFormatter<T>? TryCreateEnumFormatter<T>()
    {
        if (typeof(T).IsEnum)
        {
            EnumFormatter<T> enumFormatter = new();
            Register(enumFormatter);
            return enumFormatter;
        }
        else
        {
            return null;
        }
    }

    private static IFormatter<T>? TryCreateArrayFormatter<T>()
    {
        System.Type type = typeof(T);
        if (!typeof(T).IsArray && !typeof(T).IsSZArray)
        {
            return null;
        }

        System.Type elementType = type.GetElementType()!;
        System.Type formatterGeneric = elementType.IsEnum
            ? typeof(EnumArrayFormatter<>)
            : elementType.IsValueType && !elementType.IsEnum ? typeof(ArrayFormatter<>) : typeof(ReferenceArrayFormatter<>);

        // T = int[] -> elementType = int -> formatterGeneric<int> = IFormatter<int[]>
        System.Type actualFormatterType = formatterGeneric.MakeGenericType(elementType);

        // object → IFormatter<int[]>
        return (IFormatter<T>)System.Activator.CreateInstance(actualFormatterType)!;
    }

    private static IFormatter<T>? TryCreateListFormatter<T>()
    {
        if (!typeof(T).IsGenericType ||
            typeof(T).GetGenericTypeDefinition() != typeof(System.Collections.Generic.List<>))
        {
            return null;
        }

        System.Type formatterType;
        System.Type elementType = typeof(T).GetGenericArguments()[0];

        if (elementType.IsEnum)
        {
            formatterType = typeof(EnumListFormatter<>).MakeGenericType(elementType);
        }
        else if (elementType.IsValueType &&
                !elementType.IsEnum && TypeMetadata.IsUnmanaged(elementType))
        {
            formatterType = typeof(ListFormatter<>).MakeGenericType(elementType);
        }
        else if (elementType.IsClass ||
                (elementType.IsValueType && !elementType.IsPrimitive && !elementType.IsEnum))
        {
            formatterType = typeof(ReferenceListFormatter<>).MakeGenericType(elementType);
        }
        else
        {
            return null;
        }

        return (IFormatter<T>)System.Activator.CreateInstance(formatterType)!;
    }

    #endregion Private Methods
}