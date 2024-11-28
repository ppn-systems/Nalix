using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of enum values using their underlying primitive type.
/// </summary>
/// <typeparam name="T">The enum type of the array elements.</typeparam>
[System.Diagnostics.DebuggerStepThrough]
public sealed class EnumArrayFormatter<T> : IFormatter<T[]> where T : struct, System.Enum
{
    private static readonly System.Int32 _elementSize;

    static EnumArrayFormatter()
    {
        _elementSize = System.Runtime.InteropServices.Marshal.SizeOf(System.Enum.GetUnderlyingType(typeof(T)));

        if (_elementSize is 0 or > 8)
        {
            throw new SerializationException($"Unsupported enum underlying type size: {_elementSize}");
        }
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
    public unsafe void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)value.Length);

        if (value.Length == 0)
        {
            return;
        }

        System.Int32 totalBytes = value.Length * _elementSize;
        writer.Expand(totalBytes);

        ref System.Byte dstRef = ref writer.GetFreeBufferReference();
        ref T srcRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(value);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref dstRef,
            ref System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref srcRef),
            (System.UInt32)totalBytes);

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
    public unsafe T[] Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider.Get<System.UInt16>()
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

        System.Int32 totalBytes = length * _elementSize;

#if DEBUG
        if (reader.BytesRemaining < totalBytes)
        {
            throw new SerializationException(
                $"Buffer underrun when reading array of {typeof(T)}. Needed {totalBytes} bytes.");
        }
#endif

        T[] result = new T[length];
        ref System.Byte src = ref reader.GetSpanReference(totalBytes);
        ref T dst = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(result);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref dst),
            ref src, (System.UInt32)totalBytes);

        reader.Advance(totalBytes);
        return result;
    }
}
