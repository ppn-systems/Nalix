// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of enum values using their underlying primitive type.
/// </summary>
/// <typeparam name="T">The enum type of the array elements.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class EnumArrayFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T[]> where T : struct, System.Enum
{
    private static readonly int _elementSize;
    private static string DebuggerDisplay => $"EnumArrayFormatter<{typeof(T).FullName}>";

    static EnumArrayFormatter()
    {
        System.Type underlyingType = System.Enum.GetUnderlyingType(typeof(T));

        _elementSize = underlyingType switch
        {
            System.Type t when t == typeof(byte) || t == typeof(sbyte) => 1,
            System.Type t when t == typeof(short) || t == typeof(ushort) => 2,
            System.Type t when t == typeof(int) || t == typeof(uint) => 4,
            System.Type t when t == typeof(long) || t == typeof(ulong) => 8,
            _ => throw new SerializationException($"Unsupported enum underlying type: {underlyingType}")
        };
    }

    /// <summary>
    /// Serializes an array of enum values into the provided writer using their underlying primitive type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array of enum values to serialize.</param>
    /// <exception cref="SerializationException">
    /// Thrown if the underlying type size of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            writer.Expand(sizeof(ushort));
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        writer.Expand(sizeof(ushort));
        FormatterProvider.Get<ushort>()
                         .Serialize(ref writer, (ushort)value.Length);

        if (value.Length == 0)
        {
            return;
        }

        int totalBytes = value.Length * _elementSize;
        writer.Expand(totalBytes);

        ref byte dstRef = ref writer.GetFreeBufferReference();
        ref T srcRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(value);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref dstRef,
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref srcRef),
            (uint)totalBytes);

        writer.Advance(totalBytes);
    }

    /// <summary>
    /// Deserializes an array of enum values from the provided reader using their underlying primitive type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of enum values.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the array length is out of range or the underlying type size is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T[] Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>()
                                                .Deserialize(ref reader);

        if (length == 0)
        {
            return [];
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length > SerializerBounds.MaxArray)
        {
            throw new SerializationException("Array length out of range");
        }

        int totalBytes = length * _elementSize;

#if DEBUG
        if (reader.BytesRemaining < totalBytes)
        {
            throw new SerializationException(
                $"Buffer underrun when reading array of {typeof(T)}. Needed {totalBytes} bytes.");
        }
#endif

        T[] result = new T[length];
        ref byte src = ref reader.GetSpanReference(totalBytes);
        ref T dst = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(result);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref dst),
            ref src, (uint)totalBytes);

        reader.Advance(totalBytes);
        return result;
    }
}
