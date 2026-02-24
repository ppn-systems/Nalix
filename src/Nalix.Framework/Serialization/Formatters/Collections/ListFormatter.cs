// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Nalix.Common.Serialization;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal.Types;

namespace Nalix.Framework.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for lists of elements.
/// </summary>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<System.Collections.Generic.List<T>>
{
    private static readonly int s_elementSize = TypeMetadata.SizeOf<T>();
    private static string DebuggerDisplay => $"ListFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a list of elements into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of elements to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T> value)
    {
        if (value == null)
        {
            writer.Write(SerializerBounds.Null);
            return;
        }

        writer.Write(value.Count);

        if (value.Count == 0)
        {
            return;
        }

        ReadOnlySpan<T> span = CollectionsMarshal.AsSpan(value);
        int totalBytes = span.Length * s_elementSize;

        writer.Expand(totalBytes);
        ref byte destination = ref writer.GetFreeBufferReference();
        ref T source = ref MemoryMarshal.GetReference(span);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref destination,
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref source),
            (uint)totalBytes);

        writer.Advance(totalBytes);
    }

    /// <summary>
    /// Deserializes a list of elements from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of elements, or null if the serialized data represents a null list.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        int length = reader.ReadInt32();

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        System.Collections.Generic.List<T> list = new(length);
        CollectionsMarshal.SetCount(list, length);

        int totalBytes = length * s_elementSize;
        Span<T> span = CollectionsMarshal.AsSpan(list);
        ref byte source = ref reader.GetSpanReference(totalBytes);
        ref T destination = ref MemoryMarshal.GetReference(span);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref destination),
            ref source,
            (uint)totalBytes);

        reader.Advance(totalBytes);

        return list;
    }
}
