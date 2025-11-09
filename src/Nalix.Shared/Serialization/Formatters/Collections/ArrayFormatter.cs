// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization.Formatters.Collections;

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
    private static System.String DebuggerDisplay => $"ArrayFormatter<{typeof(T).FullName}>";

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
            FormatterProvider
                .Get<System.UInt16>()
                .Serialize(ref writer, SerializerBounds.Null);

            return;
        }

        FormatterProvider
            .Get<System.UInt16>()
            .Serialize(ref writer, unchecked((System.UInt16)value.Length));

        if (value.Length == 0)
        {
            return;
        }

        System.Int32 totalBytes = value.Length * TypeMetadata.SizeOf<T>();

        writer.Expand(totalBytes);
        ref System.Byte destination = ref writer.GetFreeBufferReference();

        // Copy block memory
        fixed (T* src = value)
        {
            fixed (System.Byte* dst = &destination)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe T[] Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider
            .Get<System.UInt16>()
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

        System.Int32 total = length * TypeMetadata.SizeOf<T>();

        ref System.Byte src = ref reader.GetSpanReference(total);

        T[] result = new T[length];
        ref T dst = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(result);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref dst),
            ref src, (System.UInt32)total);

        reader.Advance(total);
        return result;
    }
}
