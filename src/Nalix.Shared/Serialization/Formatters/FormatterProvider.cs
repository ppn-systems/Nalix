// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Primitives;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
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
    #region Fields

    private static System.Int32 _cntTotal, _cntPrimitives, _cntNullables, _cntArrays, _cntNullableArrays, _cntLists, _cntEnums, _cntStrings;
    private static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();


    #endregion Fields

    /// <summary>
    /// Initializes the static <see cref="FormatterProvider"/> class by registering formatters.
    /// </summary>
    static FormatterProvider()
    {
        // ============================================================ //
        // String
        Register<System.String>(new StringFormatter());
        Register<System.String[]>(new StringArrayFormatter());

        // ============================================================ //
        // Integer types
        Register<System.Char>(new UnmanagedFormatter<System.Char>());
        Register<System.Byte>(new UnmanagedFormatter<System.Byte>());
        Register<System.SByte>(new UnmanagedFormatter<System.SByte>());
        Register<System.Int16>(new UnmanagedFormatter<System.Int16>());
        Register<System.Int32>(new UnmanagedFormatter<System.Int32>());
        Register<System.Int64>(new UnmanagedFormatter<System.Int64>());
        Register<System.UInt16>(new UnmanagedFormatter<System.UInt16>());
        Register<System.UInt32>(new UnmanagedFormatter<System.UInt32>());
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
        Register<System.Int32[]>(new ArrayFormatter<System.Int32>());
        Register<System.Int64[]>(new ArrayFormatter<System.Int64>());
        Register<System.UInt16[]>(new ArrayFormatter<System.UInt16>());
        Register<System.UInt32[]>(new ArrayFormatter<System.UInt32>());
        Register<System.UInt64[]>(new ArrayFormatter<System.UInt64>());
        Register<System.Single[]>(new ArrayFormatter<System.Single>());
        Register<System.Double[]>(new ArrayFormatter<System.Double>());
        Register<System.Boolean[]>(new ArrayFormatter<System.Boolean>());

        // ============================================================ //
        // Nullable types
        Register<System.Char?>(new NullableFormatter<System.Char>());
        Register<System.Byte?>(new NullableFormatter<System.Byte>());
        Register<System.SByte?>(new NullableFormatter<System.SByte>());
        Register<System.Int16?>(new NullableFormatter<System.Int16>());
        Register<System.Int32?>(new NullableFormatter<System.Int32>());
        Register<System.Int64?>(new NullableFormatter<System.Int64>());
        Register<System.UInt16?>(new NullableFormatter<System.UInt16>());
        Register<System.UInt32?>(new NullableFormatter<System.UInt32>());
        Register<System.UInt64?>(new NullableFormatter<System.UInt64>());
        Register<System.Single?>(new NullableFormatter<System.Single>());
        Register<System.Double?>(new NullableFormatter<System.Double>());
        Register<System.Decimal?>(new NullableFormatter<System.Decimal>());
        Register<System.Boolean?>(new NullableFormatter<System.Boolean>());

        // Nullable complex types
        Register<System.Guid?>(new NullableFormatter<System.Guid>());
        Register<System.DateTime?>(new NullableFormatter<System.DateTime>());
        Register<System.TimeSpan?>(new NullableFormatter<System.TimeSpan>());
        Register<System.DateTimeOffset?>(new NullableFormatter<System.DateTimeOffset>());

        Register<System.Char?[]>(new NullableArrayFormatter<System.Char>());
        Register<System.Byte?[]>(new NullableArrayFormatter<System.Byte>());
        Register<System.SByte?[]>(new NullableArrayFormatter<System.SByte>());
        Register<System.Int16?[]>(new NullableArrayFormatter<System.Int16>());
        Register<System.Int32?[]>(new NullableArrayFormatter<System.Int32>());
        Register<System.Int64?[]>(new NullableArrayFormatter<System.Int64>());
        Register<System.UInt16?[]>(new NullableArrayFormatter<System.UInt16>());
        Register<System.UInt32?[]>(new NullableArrayFormatter<System.UInt32>());
        Register<System.UInt64?[]>(new NullableArrayFormatter<System.UInt64>());
        Register<System.Single?[]>(new NullableArrayFormatter<System.Single>());
        Register<System.Double?[]>(new NullableArrayFormatter<System.Double>());
        Register<System.Boolean?[]>(new NullableArrayFormatter<System.Boolean>());
        Register<System.Decimal?[]>(new NullableArrayFormatter<System.Decimal>());

        Register<System.Guid?[]>(new NullableArrayFormatter<System.Guid>());
        Register<System.DateTime?[]>(new NullableArrayFormatter<System.DateTime>());
        Register<System.TimeSpan?[]>(new NullableArrayFormatter<System.TimeSpan>());
        Register<System.DateTimeOffset?[]>(new NullableArrayFormatter<System.DateTimeOffset>());

        // Custom
        Register<UInt56[]>(new ArrayFormatter<UInt56>());
        Register<UInt56>(new UnmanagedFormatter<UInt56>());
        Register<UInt56?>(new NullableFormatter<UInt56>());
        Register<UInt56?[]>(new NullableArrayFormatter<UInt56>());

        InstanceManager.Instance.GetExistingInstance<ILogger>()?.Info(
            "[FormatterProvider] init-ok in {0} ms. Total={1} " +
            "| Primitives={2}, Nullables={3}, Arrays={4}, NullableArrays={5}, Lists={6}, Enums={7}, Strings={8} |",
            _sw.ElapsedMilliseconds, _cntTotal,
            _cntPrimitives, _cntNullables, _cntArrays, _cntNullableArrays, _cntLists, _cntEnums, _cntStrings);
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
    public static void Register<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] IFormatter<T> formatter)
    {
        System.ArgumentNullException.ThrowIfNull(formatter);

        FormatterCache<T>.Formatter = formatter;

        System.Type t = typeof(T);
        System.Type ut = t;
        System.Boolean isArray = ut.IsArray;
        System.Boolean isNullable = ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(System.Nullable<>);

        _ = System.Threading.Interlocked.Increment(ref _cntTotal);
        if (t == typeof(System.String))
        {
            _ = System.Threading.Interlocked.Increment(ref _cntStrings); return;
        }

        if (isArray)
        {
            System.Type elem = ut.GetElementType()!;
            if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(System.Nullable<>))
            {
                _ = System.Threading.Interlocked.Increment(ref _cntNullableArrays);
            }
            else
            {
                _ = System.Threading.Interlocked.Increment(ref _cntArrays);
            }

            return;
        }

        if (isNullable)
        {
            _ = System.Threading.Interlocked.Increment(ref _cntNullables); return;
        }
        if (ut.IsEnum)
        {
            _ = System.Threading.Interlocked.Increment(ref _cntEnums); return;
        }

        if (ut.IsPrimitive ||
            ut == typeof(System.Guid) ||
            ut == typeof(System.Char) ||
            ut == typeof(System.Decimal) ||
            ut == typeof(System.DateTime) ||
            ut == typeof(System.TimeSpan) ||
            ut == typeof(System.DateTimeOffset))
        {
            _ = System.Threading.Interlocked.Increment(ref _cntPrimitives);
            return;
        }

        if (ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            _ = System.Threading.Interlocked.Increment(ref _cntLists);
            return;
        }
    }

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
    public static void RegisterComplex<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] IFormatter<T> formatter)
    {
        // Check if the type is a value type and not an enum
        System.Type type = typeof(T);

        if (TypeMetadata.IsUnmanaged<T>() && !type.IsEnum)
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
    public static IFormatter<T> Get<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        // Fast path: cached
        IFormatter<T> cached = FormatterCache<T>.Formatter;
        if (cached is not null)
        {
            return cached;
        }

        System.Type t = typeof(T);

        // 1) Array
        IFormatter<T>? f = TryCreateArrayFormatter<T>();
        if (f is not null)
        {
            return CacheOrGetExisting(f);
        }

        // 2) List<T>
        f = TryCreateListFormatter<T>();
        if (f is not null)
        {
            return CacheOrGetExisting(f);
        }

        // 3) Enum
        f = TryCreateEnumFormatter<T>();
        if (f is not null)
        {
            return CacheOrGetExisting(f);
        }

        // 4) Nullable<TUnderlying>
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Nullable<>))
        {
            var underlying = System.Nullable.GetUnderlyingType(t)!;
            var created = (IFormatter<T>)System.Activator
                .CreateInstance(typeof(NullableFormatter<>).MakeGenericType(underlying))!;
            return FormatterCache<T>.Formatter ??= created;
        }

        // 5) Class (exclude string)
        if (t.IsClass && t != typeof(System.String))
        {
            if (System.Attribute.IsDefined(t, typeof(SerializePackableAttribute)))
            {
                f = GetComplex<T>(); // explicit packable → no per-object null marker
            }
            else
            {
                var ft = typeof(NullableObjectFormatter<>).MakeGenericType(t);
                f = (IFormatter<T>)System.Activator.CreateInstance(ft)!;
            }
            return CacheOrGetExisting(f);
        }

        // 6) Complex auto-gen (struct/class)
        f = GetComplex<T>();
        return CacheOrGetExisting(f);
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
    public static IFormatter<T> GetComplex<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        IFormatter<T> formatter;
        System.Type type = typeof(T);

        if (System.Nullable.GetUnderlyingType(typeof(T)) is not null)
        {
            throw new System.InvalidOperationException($"Cannot call GetComplex<T>() on Nullable<T>: {typeof(T)}");
        }

        if (TypeMetadata.IsUnmanaged<T>() && !type.IsEnum)
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> CacheOrGetExisting<T>(IFormatter<T> created)
    {
        IFormatter<T>? existing = System.Threading.Interlocked.CompareExchange(ref FormatterCache<T>.Formatter, created, null);
        return existing ?? created;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static EnumFormatter<T>? TryCreateEnumFormatter<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T>? TryCreateArrayFormatter<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        System.Type type = typeof(T);
        if (!type.IsArray)
        {
            return null;
        }

        System.Type elem = type.GetElementType()!;

        // Nullable<U>[] → NullableArrayFormatter<U>
        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(System.Nullable<>))
        {
            System.Type u = elem.GetGenericArguments()[0];
            System.Type f = typeof(NullableArrayFormatter<>).MakeGenericType(u);
            return (IFormatter<T>)System.Activator.CreateInstance(f)!;
        }

        // Enum[] → EnumArrayFormatter<Enum>
        if (elem.IsEnum)
        {
            System.Type f = typeof(EnumArrayFormatter<>).MakeGenericType(elem);
            return (IFormatter<T>)System.Activator.CreateInstance(f)!;
        }

        // ValueType[] (managed or unmanaged) → ArrayFormatter<U>
        if (TypeMetadata.IsUnmanaged(elem))
        {
            System.Type f = typeof(ArrayFormatter<>).MakeGenericType(elem);
            return (IFormatter<T>)System.Activator.CreateInstance(f)!;
        }

        // ReferenceType[] → ReferenceArrayFormatter<U>
        // Cần có formatter này (hoặc mở rộng ArrayFormatter để support ref-type).
        System.Type refArrF = typeof(ReferenceArrayFormatter<>).MakeGenericType(elem);
        return (IFormatter<T>)System.Activator.CreateInstance(refArrF)!;
    }


    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T>? TryCreateListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        System.Type t = typeof(T);
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(System.Collections.Generic.List<>))
        {
            return null;
        }

        System.Type elem = t.GetGenericArguments()[0];

        // List<Enum>
        if (elem.IsEnum)
        {
            return (IFormatter<T>)System.Activator.CreateInstance(typeof(EnumListFormatter<>).MakeGenericType(elem))!;
        }

        // List<Nullable<U>>
        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(System.Nullable<>))
        {
            System.Type u = elem.GetGenericArguments()[0];
            return (IFormatter<T>)System.Activator.CreateInstance(typeof(NullableValueListFormatter<>).MakeGenericType(u))!;
        }

        // List<value-type non-nullable> (managed or unmanaged)
        if (TypeMetadata.IsUnmanaged(elem) && !elem.IsEnum)
        {
            // Dùng ListFormatter<U> để không ghi null-flag per element
            return (IFormatter<T>)System.Activator.CreateInstance(typeof(ListFormatter<>).MakeGenericType(elem))!;
        }

        // List<class>
        return (IFormatter<T>)System.Activator.CreateInstance(typeof(ReferenceListFormatter<>).MakeGenericType(elem))!;
    }


    #endregion Private Methods
}