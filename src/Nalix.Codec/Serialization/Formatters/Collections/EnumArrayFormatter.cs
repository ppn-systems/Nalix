// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization.Internal;
using Nalix.Codec.Serialization.Internal.Types;

namespace Nalix.Codec.Serialization.Formatters.Collections;

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
    private static readonly int s_elementSize = TypeMetadata.SizeOf<T>();
    private static string DebuggerDisplay => $"EnumArrayFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes an array of enum values into the provided writer using their underlying primitive type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array of enum values to serialize.</param>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the underlying type size of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            writer.Write(SerializerBounds.Null);
            return;
        }

        writer.Write(value.Length);

        if (value.Length == 0)
        {
            return;
        }

        long totalBytesLong = (long)value.Length * s_elementSize;
        if (totalBytesLong > int.MaxValue)
        {
            throw new SerializationFailureException(
                $"Enum array data size overflow: {totalBytesLong} bytes exceeds int.MaxValue.");
        }

        int totalBytes = (int)totalBytesLong;
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
    /// <exception cref="SerializationFailureException">
    /// Thrown if the array length is out of range or the underlying type size is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T[] Deserialize(ref DataReader reader)
    {
        int length = reader.ReadInt32();

        if (length == 0)
        {
            return [];
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length < 0 || length > SerializationStaticOptions.Instance.MaxArrayLength)
        {
            throw new SerializationFailureException("Array length out of range");
        }

        long totalBytesLong = (long)length * s_elementSize;
        if (totalBytesLong > int.MaxValue)
        {
            throw new SerializationFailureException(
                $"Enum array data size overflow: {totalBytesLong} bytes exceeds int.MaxValue.");
        }

        int totalBytes = (int)totalBytesLong;

#if DEBUG
        if (reader.BytesRemaining < totalBytes)
        {
            throw new SerializationFailureException(
                $"Buffer underrun when reading array of {typeof(T)}. Needed {totalBytes} bytes.");
        }
#endif

        T[] result = System.GC.AllocateUninitializedArray<T>(length);
        ref byte src = ref reader.GetSpanReference(totalBytes);
        ref T dst = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(result);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref dst),
            ref src, (uint)totalBytes);

        reader.Advance(totalBytes);
        return result;
    }
}

