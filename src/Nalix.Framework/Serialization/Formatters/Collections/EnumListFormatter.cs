// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal.Types;

namespace Nalix.Framework.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for <see cref="System.Collections.Generic.List{T}"/>
/// where T is an enum type.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class EnumListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<System.Collections.Generic.List<T>>
    where T : struct, System.Enum
{
    private static readonly int s_elementSize = TypeMetadata.SizeOf<T>();
    private static string DebuggerDisplay => $"EnumListFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a list of enum values into the provided writer using their underlying primitive type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of enum values to serialize.</param>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the enum payload cannot be represented as a raw unmanaged block for <typeparamref name="T"/>.
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

        ushort count = (ushort)value.Count;
        writer.Write(count);

        if (count == 0)
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
    /// Deserializes a list of enum values from the provided reader using their underlying primitive type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of enum values, or null if the serialized data represents a null list.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the list length is out of range or if the enum payload for <typeparamref name="T"/> is invalid.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        ushort count = reader.ReadUInt16();

        if (count == 0)
        {
            return [];
        }

        if (count == SerializerBounds.Null)
        {
            return null!;
        }

        if (count > SerializerBounds.MaxArray)
        {
            throw new SerializationFailureException("Enum list length out of range.");
        }

        System.Collections.Generic.List<T> result = new(count);
        CollectionsMarshal.SetCount(result, count);

        int totalBytes = count * s_elementSize;
        Span<T> span = CollectionsMarshal.AsSpan(result);
        ref byte source = ref reader.GetSpanReference(totalBytes);
        ref T destination = ref MemoryMarshal.GetReference(span);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref destination),
            ref source,
            (uint)totalBytes);

        reader.Advance(totalBytes);

        return result;
    }
}
