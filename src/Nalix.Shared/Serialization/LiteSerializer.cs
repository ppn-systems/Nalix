// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
[System.Diagnostics.DebuggerStepThrough]
public static partial class LiteSerializer
{
    #region APIs

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if the provided formatter is null.
    /// </exception>
    public static void Register<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] IFormatter<T> formatter) => FormatterProvider.Register<T>(formatter);

    /// <summary>
    /// Serializes an object into a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public static System.Byte[] Serialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.MaybeNull] in T value)
    {
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            System.Byte[] array = System.GC.AllocateUninitializedArray<System.Byte>(TypeMetadata.SizeOf<T>());
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), value);

            return value == null
                ? throw new SerializationException(
                    $"Serialize of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : array;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (value is null)
            {
                return SerializerBounds.NullArrayMarker.ToArray();
            }

            System.Array array = (System.Array)(System.Object)value;
            System.Int32 length = array.Length;
            if (length is 0)
            {
                return SerializerBounds.EmptyArrayMarker.ToArray();
            }

            System.Int32 dataSize = size * length;
            System.Byte[] buffer = System.GC.AllocateUninitializedArray<System.Byte>(dataSize + 4);
            ref System.Byte ptr = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(buffer);

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, length);
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref ptr, 4),
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), (System.UInt32)dataSize);

            return buffer;
        }
        else if (kind is TypeKind.FixedSizeSerializable)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Serializing fixed-size type {typeof(T).FullName} with size {size} bytes.");

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            DataWriter writer = (size > 512) ? new(size) : new(512);

            try
            {
                formatter.Serialize(ref writer, value);

                System.Diagnostics.Debug.WriteLine(
                    $"Serialized fixed-size type {typeof(T).FullName} into {writer.WrittenCount} bytes.");

                return writer.WrittenCount == 0 ? System.Array.Empty<System.Byte>() : writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        else if (kind is TypeKind.None)
        {
            IFormatter<T> formatter = FormatterProvider.Get<T>();
            DataWriter writer = (size > 512) ? new(size) : new(512);

            try
            {
                formatter.Serialize(ref writer, value);

                System.Diagnostics.Debug.WriteLine(
                    $"Serialized fixed-size type {typeof(T).FullName} into {writer.WrittenCount} bytes.");

                return writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        else
        {
            throw new SerializationException($"TYPE {typeof(T).FullName} is not serializable.");
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
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the type is not supported for span-based serialization.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Serialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.MaybeNull] in T value,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);

        // Primitive or unmanaged struct
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            System.Int32 size = TypeMetadata.SizeOf<T>();
            if (buffer.Length < size)
            {
                throw new SerializationException($"Buffer too small. Required: {size}, Actual: {buffer.Length}");
            }

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(buffer), value);

            return size;
        }

        // Reference or Nullable/Complex types
        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 fixedSize);

        if (kind is TypeKind.FixedSizeSerializable)
        {
            if (buffer.Length < fixedSize)
            {
                throw new SerializationException($"Buffer too small. Required: {fixedSize}, Actual: {buffer.Length}");
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            DataWriter writer = new(buffer);

            formatter.Serialize(ref writer, value);

            return value == null
                ? throw new SerializationException(
                    $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : writer.WrittenCount;
        }

        throw new System.NotSupportedException(
            $"Array-based serialization is not supported for type {typeof(T)}. Use Serialize<T>(in T) to get byte[] instead.");
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
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the type is not supported for span-based serialization.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Serialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.MaybeNull] in T value,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer)
    {
        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);
        if (kind == TypeKind.FixedSizeSerializable)
        {
            if (buffer.Length < size)
            {
                throw new SerializationException("Buffer too small.");
            }

            DataWriter writer = new(buffer);
            FormatterProvider.Get<T>().Serialize(ref writer, value);
            return writer.WrittenCount;
        }

        throw new System.NotSupportedException($"System.Span<byte> serialization not supported for {typeof(T)}.");
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
    /// <exception cref="System.ArgumentException">Thrown if the buffer is empty.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> buffer,
        [System.Diagnostics.CodeAnalysis.NotNull] ref T value)
    {
        if (buffer.IsEmpty)
        {
            throw new System.ArgumentException(
                $"Cannot deserialize type '{typeof(T)}' from an empty buffer.",
                nameof(buffer)
            );
        }

        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw new SerializationException(
                    $"Insufficient buffer size for unmanaged type '{typeof(T)}'. " +
                    $"Expected {TypeMetadata.SizeOf<T>()} bytes but got {buffer.Length} bytes."
                );
            }
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            return value == null
                ? throw new SerializationException(
                    $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (IsNullArrayMarker(buffer))
            {
                value = (T)(System.Object)null!;
                return 4;
            }

            System.Type elementType = typeof(T).GetElementType()
                ?? throw new SerializationException(
                    $"TYPE '{typeof(T)}' is expected to be an array, but element type could not be resolved."
                );

            if (IsEmptyArrayMarker(buffer))
            {
                value = (T)(System.Object)System.Array.CreateInstance(elementType, 0);
                return 4;
            }

            if (buffer.Length < 4)
            {
                throw new SerializationException(
                    $"Buffer too small to contain array length prefix for type '{typeof(T)}'."
                );
            }

            System.Int32 length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int32>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            System.Int32 dataSize = size * length;
            if (buffer.Length < dataSize + 4)
            {
                throw new SerializationException(
                    $"Insufficient buffer size for array data. Expected {dataSize + 4} bytes " +
                    $"(including length prefix), but got {buffer.Length} bytes."
                );
            }

            System.Array arr = System.Array.CreateInstance(elementType, length);
            ref System.Byte dest = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(arr);

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dest,
                ref System.Runtime.CompilerServices.Unsafe.Add(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer), 4), (System.UInt32)dataSize);

            value = (T)(System.Object)arr;
            return dataSize + 4;
        }

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        DataReader reader = new(buffer);
        value = formatter.Deserialize(ref reader);
        return value == null
            ? throw new SerializationException($"Deserialization of type '{typeof(T)}' resulted in null value.")
            : reader.BytesRead;
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
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public static T Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> buffer,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 value)
    {
        if (buffer.IsEmpty)
        {
            throw new System.ArgumentException($"Cannot deserialize type '{typeof(T)}' from an empty buffer.", nameof(buffer));
        }

        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw new SerializationException(
                    $"Insufficient buffer size for unmanaged type '{typeof(T)}'. " +
                    $"Expected {TypeMetadata.SizeOf<T>()} bytes but got {buffer.Length} bytes."
                );
            }
            value = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            return System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (IsNullArrayMarker(buffer))
            {
                value = 4;
                return default!;
            }

            System.Type elementType = typeof(T).GetElementType()
                ?? throw new SerializationException(
                    $"TYPE '{typeof(T)}' is expected to be an array, but element type could not be resolved."
                );

            if (IsEmptyArrayMarker(buffer))
            {
                value = 4;
                return (T)(System.Object)System.Array.CreateInstance(elementType, 0);
            }

            if (buffer.Length < 4)
            {
                throw new SerializationException(
                    $"Buffer too small to contain array length prefix for type '{typeof(T)}'."
                );
            }

            System.Int32 length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int32>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            System.Int32 dataSize = size * length;
            if (buffer.Length < dataSize + 4)
            {
                throw new SerializationException(
                    $"Insufficient buffer size for array data. Expected {dataSize + 4} bytes " +
                    $"(including length prefix), but got {buffer.Length} bytes."
                );
            }

            System.Array arr = System.Array.CreateInstance(elementType, length);
            ref System.Byte dest = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(arr);

            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref dest,
                ref System.Runtime.CompilerServices.Unsafe.Add(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer), 4), (System.UInt32)dataSize);

            value = dataSize + 4;
            return (T)(System.Object)arr;
        }

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        DataReader reader = new(buffer);

        value = reader.BytesRead;
        return formatter.Deserialize(ref reader);
    }

    #endregion APIs

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