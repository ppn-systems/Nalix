// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Exceptions;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization functionality for <see cref="System.String"/> values using UTF-8 encoding.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class StringFormatter : IFormatter<System.String>
{
    private static System.String DebuggerDisplay => $"StringFormatter<SYSTEM.String>";
    private static readonly System.Text.Encoding Utf8 = System.Text.Encoding.UTF8;

    /// <summary>
    /// Serializes a string value into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string value to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void Serialize(ref DataWriter writer, System.String value)
    {
        if (value == null)
        {
            // 65535 biểu diễn null
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        if (value.Length == 0)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, 0);
            return;
        }

        // Tính trước số byte sẽ cần khi encode UTF8
        System.Int32 byteCount = Utf8.GetByteCount(value);
        if (byteCount > SerializerBounds.MaxString)
        {
            throw new SerializationException("The string exceeds the allowed limit.");
        }

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)byteCount);

        if (byteCount > 0)
        {
            writer.Expand(byteCount);
            ref System.Byte destination = ref writer.GetFreeBufferReference();

            fixed (System.Char* src = value)
            {
                fixed (System.Byte* dest = &destination)
                {
                    // Encode trực tiếp vào dest
                    System.Int32 bytesWritten = Utf8.GetBytes(src, value.Length, dest, byteCount);

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
    /// Thrown if the string length exceeds the maximum allowed limit.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe System.String Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider.Get<System.UInt16>()
                                                .Deserialize(ref reader);

        if (length == 0)
        {
            return System.String.Empty;
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length > SerializerBounds.MaxString)
        {
            throw new SerializationException("String length out of range");
        }

        ref System.Byte start = ref reader.GetSpanReference(length);

        System.String result;
        fixed (System.Byte* ptr = &start)
        {
            result = Utf8.GetString(ptr, length);
        }

        reader.Advance(length);
        return result;
    }
}
