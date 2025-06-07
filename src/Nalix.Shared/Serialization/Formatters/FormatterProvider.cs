using Nalix.Shared.Serialization.Formatters.Automatic;
using Nalix.Shared.Serialization.Formatters.Cache;
using Nalix.Shared.Serialization.Formatters.Collections;
using Nalix.Shared.Serialization.Formatters.Primitives;

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
        Register(new UnmanagedFormatter<char>());
        Register(new UnmanagedFormatter<byte>());
        Register(new UnmanagedFormatter<sbyte>());
        Register(new UnmanagedFormatter<short>());
        Register(new UnmanagedFormatter<ushort>());
        Register(new UnmanagedFormatter<int>());
        Register(new UnmanagedFormatter<uint>());
        Register(new UnmanagedFormatter<long>());
        Register(new UnmanagedFormatter<ulong>());
        Register(new UnmanagedFormatter<float>());
        Register(new UnmanagedFormatter<double>());
        Register(new UnmanagedFormatter<bool>());
        Register(new UnmanagedFormatter<decimal>());

        // ============================================================ //
        // Integer arrays
        Register<char[]>(new ArrayFormatter<char>());
        Register<byte[]>(new ArrayFormatter<byte>());
        Register<sbyte[]>(new ArrayFormatter<sbyte>());
        Register<short[]>(new ArrayFormatter<short>());
        Register<ushort[]>(new ArrayFormatter<ushort>());
        Register<int[]>(new ArrayFormatter<int>());
        Register<uint[]>(new ArrayFormatter<uint>());
        Register<long[]>(new ArrayFormatter<long>());
        Register<ulong[]>(new ArrayFormatter<ulong>());
        Register<float[]>(new ArrayFormatter<float>());
        Register<double[]>(new ArrayFormatter<double>());
        Register<bool[]>(new ArrayFormatter<bool>());

        // ============================================================ //
        // String
        Register(new StringFormatter());

        // ============================================================ //
        // Nullable types
        Register(new NullableFormatter<char>());
        Register(new NullableFormatter<byte>());
        Register(new NullableFormatter<sbyte>());
        Register(new NullableFormatter<short>());
        Register(new NullableFormatter<ushort>());
        Register(new NullableFormatter<int>());
        Register(new NullableFormatter<uint>());
        Register(new NullableFormatter<long>());
        Register(new NullableFormatter<ulong>());
        Register(new NullableFormatter<float>());
        Register(new NullableFormatter<double>());
        Register(new NullableFormatter<decimal>());
        Register(new NullableFormatter<bool>());
    }

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if the provided formatter is null.
    /// </exception>
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
    public static IFormatter<T> Get<T>()
    {
        IFormatter<T> formatter = FormatterCache<T>.Formatter;
        if (formatter != null) return formatter;

        // Auto-register for enums
        if (typeof(T).IsEnum)
        {
            return FormatterEnum<T>();
        }

        throw new System.InvalidOperationException($"No formatter registered for type {typeof(T)}.");
    }

    /// <summary>
    /// Retrieves the formatter for the specified complex type.
    /// </summary>
    /// <typeparam name="T">The type for which to retrieve a formatter.</typeparam>
    /// <returns>The registered formatter for the given type.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if no formatter is registered for the specified type.
    /// </exception>
    public static IFormatter<T> GetComplex<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>()
    {
        IFormatter<T> formatter;
        System.Type type = typeof(T);

        if (type.IsValueType && !type.IsEnum)
        {
            formatter = ComplexTypeCache<T>.Struct;
            if (formatter != null) return formatter;

            object? @struct = System.Activator.CreateInstance(typeof(StructFormatter<>)
                                              .MakeGenericType(type)) ??
                                              throw new System.InvalidOperationException(
                                                $"Failed to create instance of StructFormatter<{type.Name}>.");

            RegisterComplex((IFormatter<T>)@struct);
            return ComplexTypeCache<T>.Struct;
        }
        else if (type.IsClass)
        {
            formatter = ComplexTypeCache<T>.Class;
            if (formatter != null) return formatter;

            object? @object = System.Activator.CreateInstance(typeof(ObjectFormatter<>)
                                              .MakeGenericType(type)) ??
                                              throw new System.InvalidOperationException(
                                                  $"Failed to create instance of ObjectFormatter<{type.Name}>.");

            RegisterComplex((IFormatter<T>)@object);
            return ComplexTypeCache<T>.Class;
        }

        throw new System.InvalidOperationException($"No formatter registered for type {typeof(T)}.");
    }

    #region Private Methods

    private static EnumFormatter<T> FormatterEnum<T>()
    {
        EnumFormatter<T> enumFormatter = new();
        Register(enumFormatter);
        return enumFormatter;
    }

    #endregion Private Methods
}
