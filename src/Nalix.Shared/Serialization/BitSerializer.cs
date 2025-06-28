using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
public static class BitSerializer
{
    #region Constants

    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes All =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All;

    // Magic numbers cho special cases
    private static readonly byte[] NullArrayMarker = [255, 255, 255, 255];

    private static readonly byte[] EmptyArrayMarker = [0, 0, 0, 0];

    #endregion Constants

    /// <summary>
    /// Serializes an object into a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    public static System.Byte[] Serialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(in T value)
    {
        System.ArgumentNullException.ThrowIfNull(value, nameof(value));

        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            System.Byte[] array = System.GC.AllocateUninitializedArray<System.Byte>(TypeMetadata.SizeOf<T>());
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), value);

            return array;
        }

        IFormatter<T> formatter = FormatterProvider.GetComplex<T>();
        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);

        if (kind == TypeKind.None)
        {
            DataWriter writer = new(256);

            try
            {
                formatter.Serialize(ref writer, value);

                return writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        else if (kind == TypeKind.UnmanagedSZArray)
        {
            if (value == null)
            {
                return NullArrayMarker;
            }

            System.Array srcArray = (System.Array)(System.Object)value;
            System.Int32 length = srcArray.Length;
            if (length == 0) return EmptyArrayMarker;

            System.Int32 dataSize = size * length;
            System.Byte[] destArray = System.GC.AllocateUninitializedArray<System.Byte>(dataSize + 4);
            ref System.Byte head = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(destArray);

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref head, length);
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref head, 4),
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(srcArray), (System.UInt32)dataSize);

            return destArray;
        }
        else if (kind == TypeKind.FixedSizeSerializable)
        {
            DataWriter writer = new(size);

            try
            {
                formatter.Serialize(ref writer, value);

                return writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        else
        {
            throw new SerializationException($"Type {typeof(T).FullName} is not serializable.");
        }
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The byte array containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if deserialization encounters an error or if there is insufficient data in the buffer.
    /// </exception>
    public static System.Int32 Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        System.ReadOnlySpan<byte> buffer, ref T value)
    {
        if (buffer.IsEmpty)
            throw new System.ArgumentException("Buffer cannot be empty", nameof(buffer));

        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw new SerializationException($"Expected {TypeMetadata.SizeOf<T>()} bytes, found {buffer.Length} bytes.");
            }
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);

        if (kind == TypeKind.UnmanagedSZArray)
        {
            if (buffer.Length >= 4 &&
                buffer[0] == NullArrayMarker[0] && buffer[1] == NullArrayMarker[1] &&
                buffer[2] == NullArrayMarker[2] && buffer[3] == NullArrayMarker[3])
            {
                value = default!;
                return 4;
            }

            if (buffer.Length >= 4 &&
                buffer[0] == EmptyArrayMarker[0] && buffer[1] == EmptyArrayMarker[1] &&
                buffer[2] == EmptyArrayMarker[2] && buffer[3] == EmptyArrayMarker[3])
            {
                value = (T)(System.Object)System.Array.CreateInstance(typeof(T).GetElementType()!, 0);
                return 4;
            }

            if (buffer.Length < 4)
                throw new SerializationException("Buffer too small to contain array length.");

            int length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            int dataSize = size * length;
            if (buffer.Length < dataSize + 4)
                throw new SerializationException($"Expected {dataSize + 4} bytes, found {buffer.Length} bytes.");

            System.Array arr = System.Array.CreateInstance(typeof(T).GetElementType()!, length);
            ref byte dest = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(arr);

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dest,
                ref System.Runtime.CompilerServices.Unsafe.Add(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer), 4), (System.UInt32)dataSize);

            value = (T)(System.Object)arr;
            return dataSize + 4;
        }

        IFormatter<T> formatter = FormatterProvider.GetComplex<T>();
        DataReader reader = new(buffer);
        value = formatter.Deserialize(ref reader);
        return reader.BytesRead;
    }
}
