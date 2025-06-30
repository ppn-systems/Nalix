using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Types;
using System;

namespace Nalix.Shared.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
public static class LiteSerializer
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
            DataWriter writer = (size > 0) ? new(size) : new(512);

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

            System.Array array = (System.Array)(System.Object)value;
            System.Int32 length = array.Length;
            if (length == 0) return EmptyArrayMarker;

            System.Int32 dataSize = size * length;
            System.Byte[] buffer = System.GC.AllocateUninitializedArray<System.Byte>(dataSize + 4);
            ref System.Byte ptr = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(buffer);

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, length);
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref ptr, 4),
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), (System.UInt32)dataSize);

            return buffer;
        }
        else if (kind == TypeKind.FixedSizeSerializable)
        {
            System.Byte[] buffer = GC.AllocateUninitializedArray<byte>(size);
            DataWriter writer;

            if (size == 0)
            {
                writer = new(buffer);
            }
            else
            {
                writer = new(512);
            }

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
    /// Serializes an object into the provided span.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="buffer">The target span to write the serialized data into.</param>
    /// <returns>The number of bytes written into the buffer.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if serialization fails or the buffer is too small.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the type is not supported for span-based serialization.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Serialize<T>(in T value, Span<byte> buffer)
    {
        System.Int32 size;

        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            size = TypeMetadata.SizeOf<T>();
            if (buffer.Length < size)
                throw new SerializationException($"Buffer too small. Required: {size}, Actual: {buffer.Length}");

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer), value);

            return size;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out size);

        if (kind == TypeKind.FixedSizeSerializable)
        {
            if (buffer.Length < size)
                throw new SerializationException($"Buffer too small. Required: {size}, Actual: {buffer.Length}");

            IFormatter<T> formatter = FormatterProvider.GetComplex<T>();
            DataWriter writer = new(buffer.ToArray());

            formatter.Serialize(ref writer, value);
            return writer.BytesWritten;
        }

        throw new NotSupportedException($"Span-based serialization is not supported for type {typeof(T)}.");
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        System.ReadOnlySpan<System.Byte> buffer, ref T value)
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
            if (IsNullArrayMarker(buffer))
            {
                value = default!;
                return 4;
            }

            Type elementType = typeof(T).GetElementType() ?? throw new SerializationException("Invalid array type.");

            if (IsEmptyArrayMarker(buffer))
            {
                value = (T)(System.Object)System.Array.CreateInstance(elementType, 0);
                return 4;
            }

            if (buffer.Length < 4)
                throw new SerializationException("Buffer too small to contain array length.");

            int length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            int dataSize = size * length;
            if (buffer.Length < dataSize + 4)
                throw new SerializationException($"Expected {dataSize + 4} bytes, found {buffer.Length} bytes.");

            System.Array arr = System.Array.CreateInstance(elementType, length);
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

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsNullArrayMarker(System.ReadOnlySpan<System.Byte> buffer) =>
        buffer.Length >= 4 &&
        System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int32>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer)) == -1;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsEmptyArrayMarker(System.ReadOnlySpan<System.Byte> buffer) =>
        buffer.Length >= 4 &&
        System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int32>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer)) == 0;

    #endregion Private Methods
}
