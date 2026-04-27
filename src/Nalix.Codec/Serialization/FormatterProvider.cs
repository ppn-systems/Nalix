// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Codec.Serialization.Formatters.Automatic;
using Nalix.Codec.Serialization.Formatters.Cache;
using Nalix.Codec.Serialization.Formatters.Collections;
using Nalix.Codec.Serialization.Formatters.Primitives;
using Nalix.Codec.Serialization.Internal.Types;
using Nalix.Common.Exceptions;

namespace Nalix.Codec.Serialization;

/// <summary>
/// Provides a global registry for registering and retrieving formatters without boxing.
/// The provider centralizes formatter lookup so serializer code can ask for a
/// formatter once and then reuse the resolved instance on the hot path.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FormatterProvider
{
    #region Fields

    private static readonly DiagnosticListener s_listener = new("Nalix.Environment");

    private static int s_cntTotal, s_cntPrimitives, s_cntNullables, s_cntArrays,
                       s_cntNullableArrays, s_cntLists, s_cntEnums, s_cntStrings;

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
        { 2, typeof(ValueTupleFormatter<,>)    },
        { 3, typeof(ValueTupleFormatter<,,>)   },
        { 4, typeof(ValueTupleFormatter<,,,>)  },
        { 5, typeof(ValueTupleFormatter<,,,,>) },
    };

    // IL-emit factory cache:
    //   Key   = concrete formatter type, such as ArrayFormatter<int>
    //   Value = a compiled parameterless constructor delegate.
    //
    // This cache exists because formatter types are assembled dynamically with
    // MakeGenericType, so we cannot rely on a compile-time generic cache here.
    // By caching the constructor delegate per concrete formatter type, repeated
    // lookups stay O(1) and avoid reflection after the first hit.
    private static readonly ConcurrentDictionary<Type, Func<object>> s_factoryCache = new();

    #endregion Fields

    #region Static Constructor

    static FormatterProvider()
    {
        // String formatters are registered first because they are among the most
        // common and often participate in higher-level composite serializers.
        Register(new StringFormatter());
        Register(new StringArrayFormatter());

        // Primitive unmanaged formatters cover the common scalar types that can
        // be serialized directly without per-element reference tracking.
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

        // Array formatters are registered separately so fixed-size element types
        // can be serialized as contiguous buffers with minimal per-item overhead.
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

        // Nullable<T> formatters preserve the distinction between "no value" and
        // the default value of the underlying type.
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
        Register(new NullableFormatter<Guid>());
        Register(new NullableFormatter<DateOnly>());
        Register(new NullableFormatter<DateTime>());
        Register(new NullableFormatter<TimeSpan>());
        Register(new NullableFormatter<TimeOnly>());
        Register(new NullableFormatter<DateTimeOffset>());

        // NullableArray<T> formatters do the same for collections that may contain
        // missing entries at arbitrary positions.
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

        // ulong is a project-specific numeric type, so it gets the same full
        // formatter coverage as the built-in primitives.
        Register(new ArrayFormatter<ulong>());
        Register(new UnmanagedFormatter<ulong>());
        Register(new NullableFormatter<ulong>());
        Register(new NullableArrayFormatter<ulong>());

        if (s_listener.IsEnabled("init"))
        {
            s_listener.Write("init", new
            {
                ElapsedMs = s_sw.ElapsedMilliseconds,
                Total = s_cntTotal,
                Primitives = s_cntPrimitives,
                Nullables = s_cntNullables,
                Arrays = s_cntArrays,
                NullableArrays = s_cntNullableArrays,
                Lists = s_cntLists,
                Enums = s_cntEnums,
                Strings = s_cntStrings,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    #endregion Static Constructor

    #region IL-Emit Factory

    /// <summary>
    /// Returns a cached <see cref="Func{Object}"/> that calls the parameterless
    /// constructor of <paramref name="concreteFormatterType"/> via a
    /// <see cref="DynamicMethod"/>.
    /// <para>
    /// Generated IL (conceptually):
    /// <code>() => new ConcreteFormatter()</code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>Why DynamicMethod instead of Expression.Compile?</b><br/>
    /// <c>Expression.Lambda.Compile()</c> builds an expression tree first, then
    /// lowers it to IL — roughly 3–5× more work than emitting IL directly.
    /// The DynamicMethod path emits two opcodes (<c>newobj</c> + <c>ret</c>) and
    /// hands the result straight to the JIT with zero intermediate objects.
    ///
    /// <b>Why not Activator.CreateInstance?</b><br/>
    /// <c>Activator.CreateInstance</c> resolves the constructor via reflection on
    /// every call and boxes the return value.  The emitted delegate is a plain
    /// <c>Func&lt;object&gt;</c> — zero reflection, zero boxing after the first call.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)] // cold path — emit once, cache forever
    private static Func<object> BuildCtorFactory(Type concreteFormatterType)
    {
        // Build a tiny factory once so repeated formatter creation stays reflection-free.
        // The goal is to pay the reflection cost only when a new formatter type is
        // first requested, not on every serialization operation.
        ConstructorInfo ctor = concreteFormatterType.GetConstructor(Type.EmptyTypes)
            ?? throw new SerializationFailureException(
                $"No parameterless constructor on '{concreteFormatterType.FullName}'.");

        // Use the concrete formatter type as the dynamic method owner so the JIT
        // can access internal constructors when necessary.
        DynamicMethod dm = new(
            name: $"__new_{concreteFormatterType.Name}",
            returnType: typeof(object),
            parameterTypes: Type.EmptyTypes,
            owner: concreteFormatterType,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        // IL:
        //   newobj <ctor>    ; allocate + call ctor, push ref onto stack
        //   ret              ; return the object reference
        // Emit the minimum possible IL: construct the formatter and return it.
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);

        return dm.CreateDelegate<Func<object>>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object> GetOrAddFactory(Type concreteFormatterType) => s_factoryCache.GetOrAdd(concreteFormatterType, static t => BuildCtorFactory(t));

    /// <summary>
    /// Resolves the concrete formatter type from a generic definition + type args,
    /// then returns a live <see cref="IFormatter{T}"/> instance — zero reflection
    /// invoke, zero boxing after the first call per type.
    /// </summary>
    /// <typeparam name="T">The serialization target type.</typeparam>
    /// <param name="genericFormatterDef">
    /// Open generic formatter, e.g. <c>typeof(ArrayFormatter&lt;&gt;)</c>.
    /// </param>
    /// <param name="typeArg">
    /// Type argument used to close the generic, e.g. <c>typeof(int)</c>.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> EmitCreate<T>(Type genericFormatterDef, Type typeArg)
    {
        // Close the open generic formatter type at runtime, then instantiate the cached constructor.
        Type concrete = genericFormatterDef.MakeGenericType([typeArg]);
        return (IFormatter<T>)GetOrAddFactory(concrete)();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> EmitCreate<T>(Type genericFormatterDef, Type typeArg1, Type typeArg2)
    {
        Type concrete = genericFormatterDef.MakeGenericType([typeArg1, typeArg2]);
        return (IFormatter<T>)GetOrAddFactory(concrete)();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> EmitCreate<T>(Type genericFormatterDef, Type[] typeArgs)
    {
        Type concrete = genericFormatterDef.MakeGenericType(typeArgs);
        return (IFormatter<T>)GetOrAddFactory(concrete)();
    }

    #endregion IL-Emit Factory

    #region APIs

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
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

        FormatterCache<T>.Instance = formatter;

        Type t = typeof(T);
        Type ut = t;
        bool isArray = ut.IsArray;
        bool isNullable = ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(Nullable<>);

        _ = Interlocked.Increment(ref s_cntTotal);

        if (t == typeof(string)) { _ = Interlocked.Increment(ref s_cntStrings); return; }
        if (isArray)
        {
            Type elem = ut.GetElementType()!;
            _ = elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? Interlocked.Increment(ref s_cntNullableArrays)
                : Interlocked.Increment(ref s_cntArrays);
            return;
        }
        if (isNullable) { _ = Interlocked.Increment(ref s_cntNullables); return; }
        if (ut.IsEnum) { _ = Interlocked.Increment(ref s_cntEnums); return; }
        if (ut.IsPrimitive ||
            ut == typeof(Guid) ||
            ut == typeof(char) ||
            ut == typeof(decimal) ||
            ut == typeof(DateTime) ||
            ut == typeof(TimeSpan) ||
            ut == typeof(DateTimeOffset)) { _ = Interlocked.Increment(ref s_cntPrimitives); return; }
        if (ut.IsGenericType && ut.GetGenericTypeDefinition() == typeof(List<>))
        { _ = Interlocked.Increment(ref s_cntLists); }
    }

    /// <summary>
    /// Registers a formatter for complex types (struct / class).
    /// Uses <see cref="Interlocked.CompareExchange{T}"/> so the first writer wins.
    /// </summary>
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

        if (type.IsValueType && !type.IsEnum)
        {
            _ = Interlocked.CompareExchange(ref ComplexTypeCache<T>.Struct, formatter, default);
            return;
        }

        if (type.IsClass)
        {
            _ = Interlocked.CompareExchange(ref ComplexTypeCache<T>.Class, formatter, default);
            return;
        }

        throw new SerializationFailureException($"Unsupported type: {type.FullName}");
    }

    /// <summary>
    /// Retrieves the registered formatter for <typeparamref name="T"/>, creating
    /// one on-demand if needed (fully cached after first call).
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IFormatter<T> Get<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        // ── Fast path ─────────────────────────────────────────────────────
        IFormatter<T>? cached = FormatterCache<T>.Instance;
        if (cached is not null)
        {
            return cached;
        }

        // ── Slow path: resolve once, then cache ───────────────────────────
        IFormatter<T>? f;

        if ((f = TryCreateEnumFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateArrayFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateDictionaryFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateQueueFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateStackFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateHashSetFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateMemoryFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateValueTupleFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        if ((f = TryCreateListFormatter<T>()) is not null)
        {
            return CacheOrGetExisting(f);
        }

        Type t = typeof(T);

        // Nullable<TUnderlying>
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type underlying = Nullable.GetUnderlyingType(t)!;
            return CacheOrGetExisting(EmitCreate<T>(typeof(NullableFormatter<>), underlying));
        }

        // Class (non-string)
        if (t.IsClass && t != typeof(string))
        {
            return CacheOrGetExisting(EmitCreate<T>(typeof(NullableObjectFormatter<>), t));
        }

        // Struct / auto-generated complex
        return CacheOrGetExisting(GetComplex<T>());
    }

    /// <summary>
    /// Retrieves (or emits) the formatter for a complex struct or class type.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IFormatter<T> GetComplex<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type type = typeof(T);

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            throw new SerializationFailureException(
                $"Cannot call GetComplex<T>() on Nullable<T>: {type}");
        }

        if (type.IsValueType && !type.IsEnum)
        {
            IFormatter<T> existing = ComplexTypeCache<T>.Struct;
            if (existing is not null)
            {
                return existing;
            }

            // ── IL Emit: new StructFormatter<T>() ────────────────────────
            RegisterComplex(EmitCreate<T>(typeof(StructFormatter<>), type));
            return ComplexTypeCache<T>.Struct;
        }

        if (type.IsClass)
        {
            IFormatter<T> existing = ComplexTypeCache<T>.Class;
            if (existing is not null)
            {
                return existing;
            }

            // ── IL Emit: new ObjectFormatter<T>() ────────────────────────
            RegisterComplex(EmitCreate<T>(typeof(ObjectFormatter<>), type));
            return ComplexTypeCache<T>.Class;
        }

        throw new SerializationFailureException($"No formatter registered for type {type}.");
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> CacheOrGetExisting<T>(IFormatter<T> created)
    {
        IFormatter<T>? existing = Interlocked.CompareExchange(
            ref FormatterCache<T>.Instance, created, null);
        return existing ?? created;
    }

    // ------------------------------------------------------------------
    // All TryCreate* methods now call EmitCreate<T>(...) instead of
    // Activator.CreateInstance — zero reflection invoke, zero boxing.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EnumFormatter<T>? TryCreateEnumFormatter<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        if (!typeof(T).IsEnum)
        {
            return null;
        }

        EnumFormatter<T> f = new();
        Register(f);
        return f;
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

        // Nullable<U>[]
        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return EmitCreate<T>(typeof(NullableArrayFormatter<>), elem.GetGenericArguments()[0]);
        }

        // Enum[]
        if (elem.IsEnum)
        {
            return EmitCreate<T>(typeof(EnumArrayFormatter<>), elem);
        }

        // Unmanaged[]
        if (TypeMetadata.IsUnmanaged(elem))
        {
            return EmitCreate<T>(typeof(ArrayFormatter<>), elem);
        }

        // Class[]
        return EmitCreate<T>(typeof(ReferenceArrayFormatter<>), elem);
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

        if (elem.IsEnum)
        {
            return EmitCreate<T>(typeof(EnumListFormatter<>), elem);
        }

        if (elem.IsGenericType && elem.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return EmitCreate<T>(typeof(NullableValueListFormatter<>), elem.GetGenericArguments()[0]);
        }

        if (TypeMetadata.IsUnmanaged(elem) && !elem.IsEnum)
        {
            return EmitCreate<T>(typeof(ListFormatter<>), elem);
        }

        return EmitCreate<T>(typeof(ReferenceListFormatter<>), elem);
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

        if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(Dictionary<,>))
        {
            return null;
        }

        Type[] args = target.GetGenericArguments();
        return EmitCreate<T>(typeof(DictionaryFormatter<,>), args[0], args[1]);
    }

    private static IFormatter<T>? TryCreateQueueFormatter<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Queue<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];
        if (elem.IsClass && elem != typeof(string))
        {
            return null;
        }

        return EmitCreate<T>(typeof(QueueFormatter<>), elem);
    }

    private static IFormatter<T>? TryCreateStackFormatter<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Stack<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];
        if (elem.IsClass && elem != typeof(string))
        {
            return null;
        }

        return EmitCreate<T>(typeof(StackFormatter<>), elem);
    }

    private static IFormatter<T>? TryCreateHashSetFormatter<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
    {
        Type t = typeof(T);
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(HashSet<>))
        {
            return null;
        }

        Type elem = t.GetGenericArguments()[0];
        if (elem.IsClass && elem != typeof(string))
        {
            return null;
        }

        return EmitCreate<T>(typeof(HashSetFormatter<>), elem);
    }

    private static IFormatter<T>? TryCreateMemoryFormatter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
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
            throw new SerializationFailureException(
                $"MemoryFormatter only supports unmanaged element types. " +
                $"'{elem.Name}' is not unmanaged.");
        }

        return def == typeof(Memory<>)
            ? EmitCreate<T>(typeof(MemoryFormatter<>), elem)
            : EmitCreate<T>(typeof(ReadOnlyMemoryFormatter<>), elem);
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
        int formatterArity = Math.Min(typeArgs.Length, 5);

        if (!s_valueTupleFormatterDefs.TryGetValue(formatterArity, out Type? formatterDef))
        {
            throw new SerializationFailureException(
                $"ValueTupleFormatter: arity {typeArgs.Length} is not supported.");
        }

        return EmitCreate<T>(formatterDef, typeArgs[..formatterArity]);
    }

    #endregion Private Methods
}

