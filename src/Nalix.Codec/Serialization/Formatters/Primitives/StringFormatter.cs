// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.Extensions;
using Nalix.Codec.Internal;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization.Internal;

namespace Nalix.Codec.Serialization.Formatters.Primitives;

/// <summary>
/// Serializes <see cref="string"/> values as a length-prefixed UTF-8 payload.
/// Null, empty, and non-empty strings are encoded with distinct sentinel rules
/// so the reader can round-trip them without extra metadata.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class StringFormatter : IFormatter<string>
{
    private static readonly System.Text.Encoding s_utf8 = System.Text.Encoding.UTF8;
    private static string DebuggerDisplay => "StringFormatter<System.String>";

    /// <summary>
    /// Serializes a string value into the provided writer.
    /// </summary>
    /// <remarks>
    /// The encoded form starts with a 16-bit length prefix. A special sentinel is
    /// used for <see langword="null"/> so null and empty strings remain distinct.
    /// </remarks>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string value to serialize.</param>
    /// <exception cref="SerializationFailureException">Thrown when the encoded UTF-8 payload exceeds the supported maximum length or encoding writes an unexpected byte count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target writer cannot expand and no formatter is available for the length prefix.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, string value)
    {
        // Null is encoded with a sentinel length so the reader can distinguish it from an empty string.
        if (value == null)
        {
            writer.Write(SerializerBounds.Null);
            return;
        }

        // Empty strings are encoded as a zero length with no UTF-8 payload.
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        // Guard the payload size before we expand the writer buffer.
        int byteCount = s_utf8.GetByteCount(value);
        int limit = SerializationStaticOptions.Instance.MaxStringLength;
        if (byteCount > limit)
        {
            throw new SerializationFailureException($"String encoded size {byteCount} exceeds the allowed limit of {limit} (Config: Serialization.MaxStringLength)");
        }

        // Write the length first so the reader knows exactly how many bytes to consume.
        writer.Write(byteCount);

        // Expand once and encode directly into the writer's free buffer to avoid extra copies.
        writer.Expand(byteCount);
        int bytesWritten = s_utf8.GetBytes(
            value.AsSpan(),
            writer.FreeBuffer[..byteCount]);

        if (bytesWritten != byteCount)
        {
            Throw.DataMismatch();
        }

        writer.Advance(byteCount);
    }

    /// <summary>
    /// Deserializes a string value from the provided reader.
    /// </summary>
    /// <remarks>
    /// The reader mirrors the serializer's sentinel rules so <see langword="null"/>,
    /// empty, and populated strings all round-trip correctly.
    /// </remarks>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized string value.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the string length exceeds the maximum allowed limit or the reader does not contain enough bytes.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the string length prefix.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string Deserialize(ref DataReader reader)
    {
        int length = reader.ReadInt32();

        // Zero length means an empty string, not null.
        if (length == 0)
        {
            return string.Empty;
        }

        // The sentinel is reserved for null and must be checked before any range validation.
        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        int limit = SerializationStaticOptions.Instance.MaxStringLength;
        if (length < 0 || length > limit)
        {
            throw new SerializationFailureException($"String length {length} out of range (Config: Serialization.MaxStringLength)");
        }

        // Build a read-only span over the exact UTF-8 byte range and decode it directly.
        ref byte start = ref reader.GetSpanReference(length);
        string result = s_utf8.GetString(
            MemoryMarshal.CreateReadOnlySpan(ref start, length));

        reader.Advance(length);
        return result;
    }
}
