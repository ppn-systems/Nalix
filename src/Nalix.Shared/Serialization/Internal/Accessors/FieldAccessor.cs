// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Accessors;

[EditorBrowsable(EditorBrowsableState.Never)]
internal abstract class FieldAccessor<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    #region Fields

    /// <summary>
    /// Cached MethodInfo to avoid repeated reflection lookups
    /// </summary>
    private static readonly MethodInfo s_createTypedGeneric;

    /// <summary>
    /// Cache of compiled factories per field runtime type
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<int, FieldAccessor<T>>> s_factories;

    #endregion Fields

    static FieldAccessor()
    {
        s_factories = new();
        s_createTypedGeneric = typeof(FieldAccessor<T>).GetMethod(
            nameof(CreateTyped), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(FieldAccessor<T>).FullName, nameof(CreateTyped));
    }

    [DebuggerStepThrough]
    public abstract void Serialize(ref DataWriter writer, T obj);

    [DebuggerStepThrough]
    public abstract void Deserialize(ref DataReader reader, T obj);

    [DebuggerStepThrough]
    public abstract void Deserialize(ref DataReader reader, ref T obj);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldAccessor<T> Create(FieldSchema schema, int index)
    {
        if (schema.Equals(default))
        {
            throw new ArgumentNullException(nameof(schema));
        }
        ArgumentNullException.ThrowIfNull(schema.FieldInfo, nameof(schema.FieldInfo));

        // Normalize and validate target field type
        Type fieldType = schema.FieldType ?? throw new ArgumentException("schema.FieldType is null", nameof(schema));

        if (fieldType.IsByRef || fieldType.IsPointer)
        {
            throw new NotSupportedException($"ByRef/Pointer types are not supported: {fieldType}");
        }

        if (fieldType.ContainsGenericParameters)
        {
            throw new NotSupportedException($"Open generic field type is not supported: {fieldType}");
        }

        try
        {
            // Get or create compiled factory for this fieldType
            Func<int, FieldAccessor<T>> factory = s_factories.GetOrAdd(fieldType, static ft =>
            {
                // Build closed generic method: CreateTyped<TField>(int)
                MethodInfo mi = s_createTypedGeneric.MakeGenericMethod(ft);

                // Compile to delegate to avoid MethodInfo.Invoke overhead
                // Signature: Func<int, FieldAccessor<T>>
                return mi.CreateDelegate<Func<int, FieldAccessor<T>>>();
            });

            return factory(index);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create accessor for field '{schema.Name}' of type '{fieldType}'.", ex);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldAccessorImpl<T, TField> CreateTyped<TField>(int index) => new(index);
}
