// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Nalix.Common.Exceptions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Reflection;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static partial class FieldCache<T>
{
    #region Compiled Delegates Cache

    /// <summary>
    /// Store as object delegates, will be cast at runtime
    /// </summary>
    private static readonly Delegate[] s_getters;

    private static readonly Delegate[] s_setters;

    #endregion Compiled Delegates Cache

    // -------------------------------------------------------------------------
    // Delegate definitions
    // -------------------------------------------------------------------------

    // Getter:  Func<T, TField>      — standard BCL delegate, no overhead
    // Setter:  Action<T, TField>    — standard BCL delegate, no overhead
    // RefSetter: custom delegate because Action<,> cannot carry `ref T`
    private delegate void RefSetter<TVal>(ref T obj, TVal value);

    // -------------------------------------------------------------------------
    // Caches — one slot per field index
    //   s_getters / s_setters  allocated up-front in static ctor (array = fast)
    //   s_refSetterCache       lazy + thread-safe (ConcurrentDictionary)
    // -------------------------------------------------------------------------

    // Defined in FieldCache.cs (kept as-is):
    //   private static readonly Delegate[] s_getters;
    //   private static readonly Delegate[] s_setters;

    /// <summary>
    /// Lazy cache for ref-setters.  Keyed by fieldIndex.
    /// Value is a boxed <see cref="RefSetter{TVal}"/> — allocated once, never again.
    /// </summary>
    private static readonly ConcurrentDictionary<int, object> s_refSetterCache = new();

    // =========================================================================
    // IL-Emit factory methods
    // =========================================================================

    /// <summary>
    /// Emits a strongly-typed getter delegate for <paramref name="field"/>.
    /// <para>
    /// Generated IL (conceptually): <c>(T obj) => obj.&lt;FieldName&gt;</c>
    /// </para>
    /// <para>
    /// For value-type <typeparamref name="T"/> the DynamicMethod receives the
    /// object by reference (<c>T&amp;</c>) so the JIT does not box it.
    /// For reference types a plain <c>T</c> parameter is used.
    /// </para>
    /// </summary>
    /// <remarks>
    /// No <c>Expression.Compile</c>.  No reflection <c>Invoke</c>.  No boxing.
    /// The returned delegate is cast to <c>Func&lt;T, TField&gt;</c> at the
    /// call site and invoked with zero virtual dispatch.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)] // only called once per field
    private static Delegate CreateGetter(FieldInfo field)
    {
        bool ownerIsValueType = typeof(T).IsValueType;

        // Owner param type:
        //   struct  → pass by ref so we never box the struct
        //   class   → plain ref type, no boxing anyway
        Type ownerParamType = ownerIsValueType ? typeof(T).MakeByRefType() : typeof(T);

        // DynamicMethod name is diagnostic only — not part of any public API.
        DynamicMethod dm = new(
            name: $"__get_{typeof(T).Name}_{field.Name}",
            returnType: field.FieldType,
            parameterTypes: [ownerParamType],
            owner: typeof(T),           // skip visibility checks on private fields
            skipVisibility: true
        );

        ILGenerator il = dm.GetILGenerator();

        // IL:
        //   ldarg.0          ; push obj (or &obj for structs)
        //   ldfld <field>    ; load field value onto stack
        //   ret
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);

        // We always create the Func<T, TField> type regardless of ownerIsValueType
        // because the hot-path caller casts to Func<T, TField>.
        // For structs the JIT handles the ref transparently inside the DynamicMethod.
        Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), field.FieldType);
        return dm.CreateDelegate(delegateType);
    }

    /// <summary>
    /// Emits a strongly-typed setter delegate for <paramref name="field"/>.
    /// <para>
    /// Generated IL (conceptually): <c>(T obj, TField value) => obj.&lt;FieldName&gt; = value</c>
    /// </para>
    /// <para>
    /// For value-type <typeparamref name="T"/> use the <c>ref T</c> overload
    /// (<see cref="SetValue{TField}(ref T, int, TField)"/>) instead — this
    /// non-ref overload on a struct setter will mutate a copy.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Delegate CreateSetter(FieldInfo field)
    {
        bool ownerIsValueType = typeof(T).IsValueType;
        Type ownerParamType = ownerIsValueType ? typeof(T).MakeByRefType() : typeof(T);

        DynamicMethod dm = new(
            name: $"__set_{typeof(T).Name}_{field.Name}",
            returnType: typeof(void),
            parameterTypes: [ownerParamType, field.FieldType],
            owner: typeof(T),
            skipVisibility: true
        );

        ILGenerator il = dm.GetILGenerator();

        // IL:
        //   ldarg.0          ; push obj (or &obj)
        //   ldarg.1          ; push value
        //   stfld <field>    ; obj.field = value
        //   ret
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);

        Type delegateType = typeof(Action<,>).MakeGenericType(typeof(T), field.FieldType);
        return dm.CreateDelegate(delegateType);
    }

    /// <summary>
    /// Emits a <see cref="RefSetter{TVal}"/> delegate that mutates a struct in-place
    /// via <c>ref T</c>.
    /// <para>
    /// Generated IL (conceptually):
    /// <c>(ref T obj, TField value) => obj.&lt;FieldName&gt; = value</c>
    /// </para>
    /// <para>
    /// This is the <em>only</em> correct way to set a field on a struct without
    /// copying the struct first.  Class types do not need this overload.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RefSetter<TField> CreateRefSetter<TField>(FieldInfo field)
    {
        // ref T parameter — mandatory for struct mutation
        Type refOwnerType = typeof(T).MakeByRefType();

        DynamicMethod dm = new(
            name: $"__refset_{typeof(T).Name}_{field.Name}",
            returnType: typeof(void),
            parameterTypes: [refOwnerType, typeof(TField)],
            owner: typeof(T),
            skipVisibility: true
        );

        ILGenerator il = dm.GetILGenerator();

        // IL:
        //   ldarg.0          ; push &obj  (managed ref to struct)
        //   ldarg.1          ; push value
        //   stfld <field>    ; (&obj)->field = value
        //   ret
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);

        return dm.CreateDelegate<RefSetter<TField>>();
    }

    // =========================================================================
    // Public value accessors  (hot path — AggressiveInlining)
    // =========================================================================

    /// <summary>
    /// Gets the value of field at <paramref name="fieldIndex"/> from <paramref name="obj"/>.
    /// Zero-boxing, zero virtual dispatch.
    /// </summary>
    /// <typeparam name="TField">The expected field type. Must match exactly.</typeparam>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TField GetValue<TField>(T obj, int fieldIndex)
    {
        // Bounds-check is implicit via array indexer — no extra branch needed.
        ref readonly FieldSchema metadata = ref s_metadata[fieldIndex];

        // Type guard: one compare, predictably true after first call (branch predictor wins).
        if (metadata.FieldType != typeof(TField))
        {
            ThrowTypeMismatch(metadata, typeof(TField));
        }

        // Direct cast — no virtual call, no interface dispatch.
        return ((Func<T, TField>)s_getters[fieldIndex])(obj);
    }

    /// <summary>
    /// Sets the value of field at <paramref name="fieldIndex"/> on a <em>class</em> instance.
    /// For structs, prefer the <c>ref T</c> overload to avoid copying.
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(T obj, int fieldIndex, TField value)
    {
        ref readonly FieldSchema metadata = ref s_metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            ThrowTypeMismatch(metadata, typeof(TField));
        }

        ((Action<T, TField>)s_setters[fieldIndex])(obj, value);
    }

    /// <summary>
    /// Sets the value of field at <paramref name="fieldIndex"/> directly on a
    /// struct via <c>ref T</c> — mutates the original, not a copy.
    /// Safe to use on class instances too (ref is just an alias for the reference).
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(ref T obj, int fieldIndex, TField value)
    {
        ref readonly FieldSchema metadata = ref s_metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            ThrowTypeMismatch(metadata, typeof(TField));
        }

        GetOrCreateRefSetter<TField>(fieldIndex, in metadata)(ref obj, value);
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    /// <summary>
    /// Returns (creating on first call) the cached <see cref="RefSetter{TVal}"/>
    /// for <paramref name="fieldIndex"/>.
    /// <para>
    /// Marked <c>NoInlining</c> so the fast path stays lean — this runs once
    /// per (fieldIndex, TField) combination then the cache is warm forever.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RefSetter<TField> GetOrCreateRefSetter<TField>(
        int fieldIndex,
        in FieldSchema metadata)
    {
        // GetOrAdd is lock-free on the read path after the first insertion.
        return (RefSetter<TField>)s_refSetterCache.GetOrAdd(
            key: fieldIndex,
            valueFactory: static (_, fi) => CreateRefSetter<TField>(fi.FieldInfo),
            factoryArgument: metadata   // avoids closure allocation
        );
    }

    /// <summary>
    /// Cold path — extracted so the JIT can keep <see cref="GetValue{TField}"/>
    /// and <see cref="SetValue{TField}(T,int,TField)"/> fully inlineable.
    /// </summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowTypeMismatch(in FieldSchema metadata, Type requested)
        => throw new SerializationFailureException(
            $"Field '{metadata.Name}' is of type '{metadata.FieldType.FullName}', " +
            $"but accessor was requested for '{requested.FullName}'.");
}
