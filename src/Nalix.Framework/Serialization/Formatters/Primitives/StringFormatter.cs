// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization functionality for <see cref="string"/> values using UTF-8 encoding.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class StringFormatter : IFormatter<string>
{
    private static readonly System.Text.Encoding Utf8 = System.Text.Encoding.UTF8;
    private static string DebuggerDisplay => "StringFormatter<System.String>";

    /// <summary>
    /// Serializes a string value into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string value to serialize.</param>
    /// <exception cref="SerializationException">Thrown when the encoded UTF-8 payload exceeds the supported maximum length or encoding writes an unexpected byte count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target writer cannot expand and no formatter is available for the length prefix.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void Serialize(ref DataWriter writer, string value)
    {
        if (value == null)
        {
            // 65535 biểu diễn null
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        if (value.Length == 0)
        {
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, 0);
            return;
        }

        // Tính trước số byte sẽ cần khi encode UTF8
        int byteCount = Utf8.GetByteCount(value);
        if (byteCount > SerializerBounds.MaxString)
        {
            throw new SerializationException("The string exceeds the allowed limit.");
        }

        FormatterProvider.Get<ushort>()
                         .Serialize(ref writer, (ushort)byteCount);

        if (byteCount > 0)
        {
            writer.Expand(byteCount);
            ref byte destination = ref writer.GetFreeBufferReference();

            fixed (char* src = value)
            {
                fixed (byte* dest = &destination)
                {
                    // Encode trực tiếp vào dest
                    int bytesWritten = Utf8.GetBytes(src, value.Length, dest, byteCount);

                    if (bytesWritten != byteCount)
                    {
                        throw new SerializationException(
                            $"UTF8 encoding mismatch:  expected {byteCount} bytes, got {bytesWritten} bytes.");
                    }
                }
            }

            writer.Advance(byteCount);
        }
    }

    /// <summary>
    /// Deserializes a string value from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized string value.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the string length exceeds the maximum allowed limit or the reader does not contain enough bytes.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the string length prefix.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe string Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>()
                                                .Deserialize(ref reader);

        if (length == 0)
        {
            return string.Empty;
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length > SerializerBounds.MaxString)
        {
            throw new SerializationException("String length out of range");
        }

        string result;
        ref byte start = ref reader.GetSpanReference(length);

        fixed (byte* ptr = &start)
        {
            result = Utf8.GetString(ptr, length);
        }

        reader.Advance(length);
        return result;
    }
}
