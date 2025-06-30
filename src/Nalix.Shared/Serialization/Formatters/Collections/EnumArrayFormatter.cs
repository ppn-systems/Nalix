using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of enum values using their underlying primitive type.
/// </summary>
/// <typeparam name="T">The enum type of the array elements.</typeparam>
public sealed class EnumArrayFormatter<T> : IFormatter<T[]> where T : struct, System.Enum
{
    private static readonly System.Int32 _elementSize;
    private static readonly System.TypeCode _underlyingTypeCode;

    static EnumArrayFormatter()
    {
        _underlyingTypeCode = System.Type.GetTypeCode(System.Enum.GetUnderlyingType(typeof(T)));
        _elementSize = System.Runtime.InteropServices.Marshal.SizeOf(System.Enum.GetUnderlyingType(typeof(T)));

        if (_elementSize == 0 || _elementSize > 8)
            throw new SerializationException($"Unsupported enum underlying type size: {_elementSize}");
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

        if (value.Length == 0) return;

        System.Int32 totalBytes = value.Length * _elementSize;
        writer.Expand(totalBytes);
        System.Span<System.Byte> dest = writer.GetSpan(totalBytes);

        System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(
            value, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            System.IntPtr srcPtr = handle.AddrOfPinnedObject();
            fixed (System.Byte* dst = dest)
            {
                System.Buffer.MemoryCopy((void*)srcPtr, dst, totalBytes, totalBytes);
            }
        }
        finally
        {
            handle.Free();
        }

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

        if (length == 0) return [];
        if (length == SerializerBounds.Null) return null!;
        if (length > SerializerBounds.MaxArray) throw new SerializationException("Array length out of range");

        System.Int32 totalBytes = length * _elementSize;
        System.ReadOnlySpan<System.Byte> src = reader.GetSpan(totalBytes);
        T[] result = new T[length];

        System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(
            result, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            System.IntPtr dstPtr = handle.AddrOfPinnedObject();
            fixed (System.Byte* pSrc = src)
            {
                System.Buffer.MemoryCopy(pSrc, (void*)dstPtr, totalBytes, totalBytes);
            }
        }
        finally
        {
            handle.Free();
        }

        reader.Advance(totalBytes);
        return result;
    }
}
