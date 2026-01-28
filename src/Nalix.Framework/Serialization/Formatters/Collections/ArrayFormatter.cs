// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal.Types;

namespace Nalix.Framework.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of unmanaged types.
/// </summary>
/// <typeparam name="T">The unmanaged type of the array elements.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ArrayFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T[]> where T : unmanaged
{
    private static string DebuggerDisplay => $"ArrayFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes an array of unmanaged values into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            // Convention: -1 indicates a null array
            writer.Expand(sizeof(ushort));
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);

            return;
        }

        writer.Expand(sizeof(ushort));
        FormatterProvider.Get<ushort>()
                         .Serialize(ref writer, unchecked((ushort)value.Length));

        if (value.Length == 0)
        {
            return;
        }

        int totalBytes = value.Length * TypeMetadata.SizeOf<T>();

        writer.Expand(totalBytes);
        ref byte destination = ref writer.GetFreeBufferReference();

        // Copy block memory
        fixed (T* src = value)
        {
            fixed (byte* dst = &destination)
            {
                System.Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
            }
        }

        writer.Advance(totalBytes);
    }

    /// <summary>
    /// Deserializes an array of unmanaged values from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of unmanaged values, or null if applicable.</returns>
    /// <exception cref="SerializationException">Thrown when the encoded array length is outside supported bounds or the reader does not contain enough bytes.</exception>
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

        int total = length * TypeMetadata.SizeOf<T>();

        ref byte src = ref reader.GetSpanReference(total);

        T[] result = new T[length];
        ref T dst = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(result);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref dst),
            ref src, (uint)total);

        reader.Advance(total);
        return result;
    }
}
