// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;
using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;

namespace Nalix.Codec.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for <see cref="System.Collections.Generic.List{T}"/>
/// where T is a reference type.
/// </summary>
/// <typeparam name="T">The reference type of list elements.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReferenceListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<System.Collections.Generic.List<T>>
{
    private static readonly IFormatter<T> s_elementFormatter = FormatterProvider.Get<T>();
    private static string DebuggerDisplay => $"ReferenceListFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a list of reference type elements into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of reference type elements to serialize.</param>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the underlying formatter for type <typeparamref name="T"/> encounters an error during serialization.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T> value)
    {
        if (value is null)
        {
            writer.Write(SerializerBounds.Null);
            return;
        }

        int count = value.Count;
        writer.Write(count);

        ReadOnlySpan<T> span = CollectionsMarshal.AsSpan(value);
        for (int i = 0; i < span.Length; i++)
        {
            s_elementFormatter.Serialize(ref writer, span[i]);
        }
    }

    /// <summary>
    /// Deserializes a list of reference type elements from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of reference type elements, or null if the serialized data represents a null list.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the list length is out of range or if the underlying formatter for type <typeparamref name="T"/> encounters an error during deserialization.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        int count = reader.ReadInt32();

        if (count == 0)
        {
            return [];
        }

        if (count == SerializerBounds.Null)
        {
            return null!;
        }

        if (count < 0 || count > SerializerBounds.MaxArray)
        {
            throw new SerializationFailureException("Reference list length out of range.");
        }

        System.Collections.Generic.List<T> list = new(count);
        CollectionsMarshal.SetCount(list, count);
        Span<T> span = CollectionsMarshal.AsSpan(list);
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = s_elementFormatter.Deserialize(ref reader);
        }

        return list;
    }
}
