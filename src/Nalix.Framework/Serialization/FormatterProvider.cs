// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Primitives;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Framework.Serialization.Formatters.Automatic;
using Nalix.Framework.Serialization.Formatters.Cache;
using Nalix.Framework.Serialization.Formatters.Collections;
using Nalix.Framework.Serialization.Formatters.Primitives;
using Nalix.Framework.Serialization.Internal.Types;

namespace Nalix.Framework.Serialization;

/// <summary>
/// Provides a global registry for registering and retrieving formatters without boxing.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FormatterProvider
{
    #region Fields

    private static int s_cntTotal, s_cntPrimitives, s_cntNullables, s_cntArrays, s_cntNullableArrays, s_cntLists, s_cntEnums, s_cntStrings;
    private static readonly Stopwatch s_sw = Stopwatch.StartNew();

    private static readonly HashSet<Type> s_valueTupleDefinitions =
    [
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>)
    ];

    private static readonly Dictionary<int, Type> s_valueTupleFormatterDefs = new()
    {
        { 2, typeof(ValueTupleFormatter<,>) },
        { 3, typeof(ValueTupleFormatter<,,>) },
        { 4, typeof(ValueTupleFormatter<,,,>) },
        { 5, typeof(ValueTupleFormatter<,,,,>) },
    };

    /// <summary>
    /// Factory cache (type -> factory delegate)
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object>> s_formatterFactories = new();

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes the static <see cref="FormatterProvider"/> class by registering formatters.
    /// </summary>
    static FormatterProvider()
    {
        // ============================================================ //
        // String
        Register(new StringFormatter());
        Register(new StringArrayFormatter());

        // ============================================================ //
        // Integer types
        Register(new UnmanagedFormatter<char>());
        Register(new UnmanagedFormatter<byte>());
        Register(new UnmanagedFormatter<sbyte>());
        Register(new UnmanagedFormatter<short>());
        Register(new UnmanagedFormatter<int>());
        Register(new UnmanagedFormatter<long>());
        Register(new UnmanagedFormatter<ushort>());
        Register(new UnmanagedFormatter<uint>());
        Register(new UnmanagedFormatter<ulong>());
        Register(new UnmanagedFormatter<float>());
        Register(new UnmanagedFormatter<double>());
        Register(new UnmanagedFormatter<bool>());
        Register(new UnmanagedFormatter<decimal>());

        Register(new UnmanagedFormatter<Guid>());
        Register(new UnmanagedFormatter<DateOnly>());
        Register(new UnmanagedFormatter<DateTime>());
        Register(new UnmanagedFormatter<TimeSpan>());
        Register(new UnmanagedFormatter<TimeOnly>());
        Register(new UnmanagedFormatter<DateTimeOffset>());

        // ============================================================ //
        // Integer arrays
        Register(new ArrayFormatter<char>());
        Register(new ArrayFormatter<byte>());
        Register(new ArrayFormatter<sbyte>());
        Register(new ArrayFormatter<short>());
        Register(new ArrayFormatter<int>());
        Register(new ArrayFormatter<long>());
        Register(new ArrayFormatter<ushort>());
        Register(new ArrayFormatter<uint>());
        Register(new ArrayFormatter<ulong>());
        Register(new ArrayFormatter<float>());
        Register(new ArrayFormatter<double>());
        Register(new ArrayFormatter<bool>());

        Register(new ArrayFormatter<Guid>());
        Register(new ArrayFormatter<DateOnly>());
        Register(new ArrayFormatter<DateTime>());
        Register(new ArrayFormatter<TimeSpan>());
        Register(new ArrayFormatter<TimeOnly>());
        Register(new ArrayFormatter<DateTimeOffset>());

        // ============================================================ //
        // Nullable types
        Register(new NullableFormatter<char>());
        Register(new NullableFormatter<byte>());
        Register(new NullableFormatter<sbyte>());
        Register(new NullableFormatter<short>());
        Register(new NullableFormatter<int>());
        Register(new NullableFormatter<long>());
        Register(new NullableFormatter<ushort>());
        Register(new NullableFormatter<uint>());
        Register(new NullableFormatter<ulong>());
        Register(new NullableFormatter<float>());
        Register(new NullableFormatter<double>());
        Register(new NullableFormatter<decimal>());
        Register(new NullableFormatter<bool>());

        // Nullable complex types
        Register(new NullableFormatter<Guid>());
        Register(new NullableFormatter<DateOnly>());
        Register(new NullableFormatter<DateTime>());
        Register(new NullableFormatter<TimeSpan>());
        Register(new NullableFormatter<TimeOnly>());
        Register(new NullableFormatter<DateTimeOffset>());

        Register(new NullableArrayFormatter<char>());
        Register(new NullableArrayFormatter<byte>());
        Register(new NullableArrayFormatter<sbyte>());
        Register(new NullableArrayFormatter<short>());
        Register(new NullableArrayFormatter<int>());
        Register(new NullableArrayFormatter<long>());
        Register(new NullableArrayFormatter<ushort>());
        Register(new NullableArrayFormatter<uint>());
        Register(new NullableArrayFormatter<ulong>());
        Register(new NullableArrayFormatter<float>());
        Register(new NullableArrayFormatter<double>());
        Register(new NullableArrayFormatter<bool>());
        Register(new NullableArrayFormatter<decimal>());

        Register(new NullableArrayFormatter<Guid>());
        Register(new NullableArrayFormatter<DateOnly>());
        Register(new NullableArrayFormatter<DateTime>());
        Register(new NullableArrayFormatter<TimeSpan>());
        Register(new NullableArrayFormatter<TimeOnly>());
        Register(new NullableArrayFormatter<DateTimeOffset>());

        // Custom
        Register(new ArrayFormatter<UInt56>());
        Register(new UnmanagedFormatter<UInt56>());
        Register(new NullableFormatter<UInt56>());
        Register(new NullableArrayFormatter<UInt56>());

        InstanceManager.Instance.GetExistingInstance<ILogger>()?.Info(
            "[SH.FormatterProvider] init-ok in {0} ms. total={1}, primitives={2}, nullables={3}, arrays={4}, nullableArrays={5}, lists={6}, enums={7}, strings={8}",
            s_sw.ElapsedMilliseconds,
            s_cntTotal,
            s_cntPrimitives,
            s_cntNullables,
            s_cntArrays,
            s_cntNullableArrays,
            s_cntLists,
            s_cntEnums,
            s_cntStrings
        );
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided formatter is null.
    /// </exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
        IFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        FormatterCache<T>.Formatter = formatter;

        Type t = typeof(T);
        Type ut = t;
        bool isArray = ut.IsArray;
        bool isNullable = ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(Nullable<>);

        _ = Interlocked.Increment(ref s_cntTotal);
        if (t == typeof(string))
        {
            _ = Interlocked.Increment(ref s_cntStrings); return;
        }

        if (isArray)
        {
            Type elem = ut.GetElementType()!;
            _ = elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? Interlocked.Increment(ref s_cntNullableArrays)
                : Interlocked.Increment(ref s_cntArrays);

            return;
        }

        if (isNullable)
        {
            _ = Interlocked.Increment(ref s_cntNullables); return;
        }
        if (ut.IsEnum)
        {
            _ = Interlocked.Increment(ref s_cntEnums); return;
        }

        if (ut.IsPrimitive ||
            ut == typeof(Guid) ||
            ut == typeof(char) ||
            ut == typeof(decimal) ||
            ut == typeof(DateTime) ||
            ut == typeof(TimeSpan) ||
            ut == typeof(DateTimeOffset))
        {
            _ = Interlocked.Increment(ref s_cntPrimitives);
            return;
        }

        if (ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(List<>))
        {
            _ = Interlocked.Increment(ref s_cntLists);
        }
    }

    /// <summary>
    /// Registers a formatter for complex types, distinguishing between value types and reference types.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided formatter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the type is unsupported (neither a struct nor a class).
    /// </exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterComplex<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
    IFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Type type = typeof(T);

        // For unmanaged value types (structs) that are not enums, set Struct formatter atomically.
        if (TypeMetadata.IsUnmanaged<T>() && !type.IsEnum)
        {
            // Use Interlocked.CompareExchange to ensure only the first successful writer wins.
            // If another thread already set ComplexTypeCache<T>.Struct, we keep the existing one.
            _ = Interlocked.CompareExchange(ref ComplexTypeCache<T>.Struct, formatter, default);

            return;
        }
        else if (type.IsClass)
        {
            // Same for class formatters.
            _ = Interlocked.CompareExchange(ref ComplexTypeCache<T>.Class, formatter, default);

            return;
        }

        throw new SerializationFailureException($"Unsupported type: {type.FullName}");
    }

    /// <summary>
    /// Retrieves the registered formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which to retrieve the formatter.</typeparam>
    /// <returns>The registered formatter for the specified type.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if no formatter is registered for the given type.
    /// </exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IFormatter<T> Get<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        // Fast path: cached
        IFormatter<T> cached = FormatterCache<T>.Formatter;
        if (cached is not null)
        {
            return cached;
        }

        Type t = typeof(T);

        // 1) Array
        IFormatter<T>? f = TryCreateArrayFormatter<T>();
        if (f is not null)
        {
            return CacheOrGetExisting(f);
        }

        // 1.2) Dictionary<,> (also support Nullable<Dictionary<,>>)
        IFormatter<T>? dictFormatter = TryCreateDictionaryFormatter<T>();
        if (dictFormatter is not null)
        {
            return CacheOrGetExisting(dictFormatter);
        }

        // 1.3) Queue<T>
        IFormatter<T>? queueFormatter = TryCreateQueueFormatter<T>();
        if (queueFormatter is not null)
        {
            return CacheOrGetExisting(queueFormatter);
        }

        // 1.4) Stack<T>
        IFormatter<T>? stackFormatter = TryCreateStackFormatter<T>();
        if (stackFormatter is not null)
        {
            return CacheOrGetExisting(stackFormatter);
        }

        // 1.5) HashSet<T>
        IFormatter<T>? hashSetFormatter = TryCreateHashSetFormatter<T>();
        if (hashSetFormatter is not null)
        {
            return CacheOrGetExisting(hashSetFormatter);
        }

        // 1.6) Memory<T> / ReadOnlyMemory<T>
        IFormatter<T>? memoryFormatter = TryCreateMemoryFormatter<T>();
        if (memoryFormatter is not null)
        {
            return CacheOrGetExisting(memoryFormatter);
        }

        // 1.7) ValueTuple<...>
        IFormatter<T>? tupleFormatter = TryCreateValueTupleFormatter<T>();
        if (tupleFormatter is not null)
        {
            return CacheOrGetExisting(tupleFormatter);
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
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type underlying = Nullable.GetUnderlyingType(t)!;
            IFormatter<T> created = (IFormatter<T>)Activator
                .CreateInstance(typeof(NullableFormatter<>).MakeGenericType(underlying))!;
            return FormatterCache<T>.Formatter ??= created;
        }

        // 5) Class (exclude string)
        if (t.IsClass && t != typeof(string))
        {
            if (Attribute.IsDefined(t, typeof(SerializePackableAttribute)))
            {
                f = GetComplex<T>(); // explicit packable → no per-object null marker
            }
            else
            {
                Type ft = typeof(NullableObjectFormatter<>).MakeGenericType(t);
                f = (IFormatter<T>)Activator.CreateInstance(ft)!;
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
    /// <exception cref="SerializationFailureException">
    /// Thrown if no formatter is registered for the specified type.
    /// </exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IFormatter<T> GetComplex<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        IFormatter<T> formatter;
        Type type = typeof(T);

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            throw new SerializationFailureException($"Cannot call GetComplex<T>() on Nullable<T>: {type}");
        }

        if (TypeMetadata.IsUnmanaged<T>() && !type.IsEnum)
        {
            formatter = ComplexTypeCache<T>.Struct;
            if (formatter != null)
            {
                return formatter;
            }

            // Use cached factory delegate instead of reflection
            Func<object> factory = GetFormatterFactory(type, typeof(StructFormatter<>));
            object? @struct = factory() ?? throw new SerializationFailureException($"Failed to create instance of StructFormatter<{type.Name}>.");
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

            Func<object> factory = GetFormatterFactory(type, typeof(ObjectFormatter<>));
            object? @object = factory() ?? throw new SerializationFailureException($"Failed to create instance of ObjectFormatter<{type.Name}>.");
            RegisterComplex((IFormatter<T>)@object);
            return ComplexTypeCache<T>.Class;
        }

        throw new SerializationFailureException($"No formatter registered for type {type}.");
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> CacheOrGetExisting<T>(IFormatter<T> created)
    {
        IFormatter<T>? existing = Interlocked.CompareExchange(ref FormatterCache<T>.Formatter, created, null);
        return existing ?? created;
    }

    /// <summary>
    /// Get (or create and cache) factory delegate for formatter type.
    /// </summary>
    /// <param name="type">Target type</param>
    /// <param name="genericFormatterType">Generic definition, e.g. typeof(StructFormatter&lt;&gt;)</param>
    /// <exception cref="SerializationFailureException">Thrown when the formatter type does not expose a parameterless constructor.</exception>
    private static Func<object> GetFormatterFactory(Type type, Type genericFormatterType)
    {
        return s_formatterFactories.GetOrAdd(type, t =>
        {
            Type constructed = genericFormatterType.MakeGenericType(t);
            ConstructorInfo ctor = constructed.GetConstructor(Type.EmptyTypes) ?? throw new SerializationFailureException($"No parameterless constructor for {constructed}");
            System.Linq.Expressions.NewExpression newExpr = System.Linq.Expressions.Expression.New(ctor);
            System.Linq.Expressions.Expression<Func<object>> lambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(newExpr);

            return lambda.Compile();
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EnumFormatter<T>? TryCreateEnumFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        if (typeof(T).IsEnum)
        {
            EnumFormatter<T> enumFormatter = new();
            Register(enumFormatter);
            return enumFormatter;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T>? TryCreateArrayFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type type = typeof(T);
        if (!type.IsArray)
        {
            return null;
        }

        Type elem = type.GetElementType()!;

        // Nullable<U>[] → NullableArrayFormatter<U>
        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type u = elem.GetGenericArguments()[0];
            Type f = typeof(NullableArrayFormatter<>).MakeGenericType(u);
            return (IFormatter<T>)Activator.CreateInstance(f)!;
        }

        // Enum[] → EnumArrayFormatter<Enum>
        if (elem.IsEnum)
        {
            Type f = typeof(EnumArrayFormatter<>).MakeGenericType(elem);
            return (IFormatter<T>)Activator.CreateInstance(f)!;
        }

        // ValueType[] (managed or unmanaged) → ArrayFormatter<U>
        if (TypeMetadata.IsUnmanaged(elem))
        {
            Type f = typeof(ArrayFormatter<>).MakeGenericType(elem);
            return (IFormatter<T>)Activator.CreateInstance(f)!;
        }

        // ReferenceType[] → ReferenceArrayFormatter<U>
        // Cần có formatter này (hoặc mở rộng ArrayFormatter để support ref-type).
        Type refArrF = typeof(ReferenceArrayFormatter<>).MakeGenericType(elem);
        return (IFormatter<T>)Activator.CreateInstance(refArrF)!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T>? TryCreateListFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(List<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];

        // List<Enum>
        if (elem.IsEnum)
        {
            return (IFormatter<T>)Activator.CreateInstance(typeof(EnumListFormatter<>).MakeGenericType(elem))!;
        }

        // List<Nullable<U>>
        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type u = elem.GetGenericArguments()[0];
            return (IFormatter<T>)Activator.CreateInstance(typeof(NullableValueListFormatter<>).MakeGenericType(u))!;
        }

        // List<value-type non-nullable> (managed or unmanaged)
        if (TypeMetadata.IsUnmanaged(elem) && !elem.IsEnum)
        {
            // Dùng ListFormatter<U> để không ghi null-flag per element
            return (IFormatter<T>)Activator.CreateInstance(typeof(ListFormatter<>).MakeGenericType(elem))!;
        }

        // List<class>
        return (IFormatter<T>)Activator.CreateInstance(typeof(ReferenceListFormatter<>).MakeGenericType(elem))!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T>? TryCreateDictionaryFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);

        Type target = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? t.GetGenericArguments()[0] : t;

        if (!target.IsGenericType ||
            target.GetGenericTypeDefinition() != typeof(Dictionary<,>))
        {
            return null;
        }

        Type[] args = target.GetGenericArguments(); // [TKey, TValue]
        Type keyType = args[0];
        Type valType = args[1];

        Type formatterType = typeof(DictionaryFormatter<,>).MakeGenericType(keyType, valType);

        return (IFormatter<T>)Activator.CreateInstance(formatterType)!;
    }

    private static IFormatter<T>? TryCreateQueueFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);

        if (!t.IsGenericType ||
            t.GetGenericTypeDefinition() != typeof(Queue<>))
        {
            return null;
        }

        Type elementType = t.GetGenericArguments()[0];

        return elementType.IsClass && elementType != typeof(string)
            ? null : (IFormatter<T>)Activator.CreateInstance(typeof(QueueFormatter<>).MakeGenericType(elementType))!;
    }

    private static IFormatter<T>? TryCreateStackFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);

        if (!t.IsGenericType ||
            t.GetGenericTypeDefinition() != typeof(Stack<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];

        return elem.IsClass && elem != typeof(string)
            ? null : (IFormatter<T>)Activator.CreateInstance(typeof(StackFormatter<>).MakeGenericType(elem))!;
    }

    private static IFormatter<T>? TryCreateHashSetFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);

        if (!t.IsGenericType ||
            t.GetGenericTypeDefinition() != typeof(HashSet<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];

        return elem.IsClass && elem != typeof(string)
            ? null : (IFormatter<T>)Activator.CreateInstance(typeof(HashSetFormatter<>).MakeGenericType(elem))!;
    }

    private static IFormatter<T>? TryCreateMemoryFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.All)] T>()
    {
        Type t = typeof(T);
        if (!t.IsGenericType)
        {
            return null;
        }

        Type def = t.GetGenericTypeDefinition();
        if (def != typeof(Memory<>) && def != typeof(ReadOnlyMemory<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];

        if (!TypeMetadata.IsUnmanaged(elem))
        {
            throw new SerializationFailureException($"MemoryFormatter only supports unmanaged element types. T='{elem.Name}' is not unmanaged. For strings, use IFormatter<string> directly.");
        }
        else if (def == typeof(Memory<>))
        {
            return (IFormatter<T>)Activator.CreateInstance(typeof(MemoryFormatter<>).MakeGenericType(elem))!;
        }
        else
        {
            return (IFormatter<T>)Activator.CreateInstance(typeof(ReadOnlyMemoryFormatter<>).MakeGenericType(elem))!;
        }
    }

    private static IFormatter<T>? TryCreateValueTupleFormatter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);

        if (!t.IsGenericType)
        {
            return null;
        }

        Type def = t.GetGenericTypeDefinition();

        if (!s_valueTupleDefinitions.Contains(def))
        {
            return null;
        }

        Type[] typeArgs = t.GetGenericArguments();
        int arity = typeArgs.Length;

        int formatterArity = Math.Min(arity, 5);

        if (!s_valueTupleFormatterDefs.TryGetValue(formatterArity, out Type? formatterDef))
        {
            throw new SerializationFailureException($"ValueTupleFormatter: arity {arity} is not supported.");
        }

        Type formatterType = formatterDef.MakeGenericType(typeArgs[..formatterArity]);
        return (IFormatter<T>)Activator.CreateInstance(formatterType)!;
    }

    #endregion Private Methods
}
