// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
[System.Diagnostics.DebuggerStepThrough]
public static class LiteSerializer
{
    #region Constants

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes All = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All;

    #endregion Constants

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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
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
                return SerializerBounds.NullArrayMarker;
            }

            System.Array array = (System.Array)(System.Object)value;
            System.Int32 length = array.Length;
            if (length is 0)
            {
                return SerializerBounds.EmptyArrayMarker;
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
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
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
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
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
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

    #region Lease APIs

    /// <summary>
    /// Serializes a value into a newly rented <see cref="BufferLease"/> and returns the lease.
    /// The lease will own the payload; <see cref="BufferLease.Length"/> is set to bytes written.
    /// </summary>
    /// <typeparam name="T">Type to serialize.</typeparam>
    /// <param name="value">Input value to serialize.</param>
    /// <param name="zeroOnDispose">When true, clears the used payload before returning to the pool.</param>
    /// <returns>A <see cref="BufferLease"/> owning the serialized payload.</returns>
    /// <exception cref="SerializationException">If serialization fails.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static BufferLease Serialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] in T value,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean zeroOnDispose = false)
    {
        // Unmanaged non-nullable fast path: exact-size write
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            System.Int32 size = TypeMetadata.SizeOf<T>();
            BufferLease lease = BufferLease.Rent(size, zeroOnDispose);
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(lease.SpanFull), value);
            lease.CommitLength(size);

            return value == null
                ? throw new SerializationException(
                    $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : lease;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 sizeHint);

        // Unmanaged single-dimension arrays: marker(-1/null,0/empty) + raw block
        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (value is null)
            {
                BufferLease lz = BufferLease.Rent(4, zeroOnDispose);
                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                    ref System.Runtime.InteropServices.MemoryMarshal.GetReference(lz.SpanFull), -1); // NullArrayMarker
                lz.CommitLength(4);

                return value == null
                    ? throw new SerializationException(
                        $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                    : lz;
            }

            System.Array array = (System.Array)(System.Object)value!;
            System.Int32 length = array.Length;

            if (length == 0)
            {
                BufferLease lz = BufferLease.Rent(4, zeroOnDispose);
                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                    ref System.Runtime.InteropServices.MemoryMarshal.GetReference(lz.SpanFull), 0); // EmptyArrayMarker
                lz.CommitLength(4);

                return lz;
            }

            System.Int32 dataSize = checked(sizeHint * length);
            System.Int32 total = checked(dataSize + 4);

            BufferLease lease = BufferLease.Rent(total, zeroOnDispose);
            ref System.Byte dst = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(lease.SpanFull);

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref dst, length);
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref dst, 4),
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array),
                (System.UInt32)dataSize);

            lease.CommitLength(total);
            return lease;
        }

        // Formatter path (fixed-size-serializable or variable)
        IFormatter<T> formatter = FormatterProvider.Get<T>();

        // Start with a reasonable capacity (fixed-size uses the hint; variable uses small slab).
        System.Int32 capacity = (kind is TypeKind.FixedSizeSerializable && sizeHint > 0) ? sizeHint : 512;

        // Retry loop: if capacity is insufficient, double and retry once or twice.
        for (System.Int32 attempt = 0; attempt < 3; attempt++)
        {
            BufferLease lease = BufferLease.Rent(capacity, zeroOnDispose);
            DataWriter writer = new(lease.SpanFull);
            try
            {
                formatter.Serialize(ref writer, value);
                // If DataWriter succeeds within span bounds:
                lease.CommitLength(writer.WrittenCount);

                return value == null
                    ? throw new SerializationException(
                        $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                    : lease;
            }
            catch (System.Exception ex) when (ex is SerializationException or System.IndexOutOfRangeException or System.ArgumentOutOfRangeException)
            {
                // capacity too small or formatter overflowed the writer; grow and retry
                writer.Dispose();
                lease.Dispose();
                capacity = checked(System.Math.Max(capacity * 2, writer.WrittenCount > 0 ? writer.WrittenCount : capacity * 2));
                continue;
            }
            finally
            {
                writer.Dispose();
            }
        }

        // Final attempt with one-shot exact sizing by serializing into a temporary large lease
        {
            BufferLease probe = BufferLease.Rent(capacity, zeroOnDispose);
            DataWriter w = new(probe.SpanFull);
            try
            {
                formatter.Serialize(ref w, value);
                probe.CommitLength(w.WrittenCount);

                return value == null
                    ? throw new SerializationException(
                        $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                    : probe;
            }
            finally
            {
                w.Dispose();
            }
        }
    }

    /// <summary>
    /// Serializes <paramref name="value"/> into an existing <see cref="BufferLease"/> capacity.
    /// Updates <see cref="BufferLease.Length"/> on success.
    /// </summary>
    /// <typeparam name="T">Type to serialize.</typeparam>
    /// <param name="value">Input value.</param>
    /// <param name="target">Target lease whose capacity is used as the output buffer.</param>
    /// <returns>Number of bytes written.</returns>
    /// <exception cref="SerializationException">If capacity is insufficient or serialization fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Serialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] in T value,
        [System.Diagnostics.CodeAnalysis.NotNull] BufferLease target)
    {
        System.ArgumentNullException.ThrowIfNull(target);

        // Unmanaged fast path
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            System.Int32 size = TypeMetadata.SizeOf<T>();
            if (target.Capacity < size)
            {
                throw new SerializationException($"Buffer too small. Required: {size}, Actual: {target.Capacity}");
            }

            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(target.SpanFull), value);
            target.CommitLength(size);

            return value == null
                ? throw new SerializationException(
                    $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : size;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out System.Int32 sizeHint);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (value is null)
            {
                if (target.Capacity < 4)
                {
                    throw new SerializationException("Buffer too small for null-array marker.");
                }

                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                    ref System.Runtime.InteropServices.MemoryMarshal.GetReference(target.SpanFull), -1);
                target.CommitLength(4);

                return value == null
                    ? throw new SerializationException(
                        $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                    : 4;
            }

            System.Array array = (System.Array)(System.Object)value!;
            System.Int32 length = array.Length;

            if (length == 0)
            {
                if (target.Capacity < 4)
                {
                    throw new SerializationException("Buffer too small for empty-array marker.");
                }

                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                    ref System.Runtime.InteropServices.MemoryMarshal.GetReference(target.SpanFull), 0);
                target.CommitLength(4);

                return 4;
            }

            System.Int32 dataSize = checked(sizeHint * length);
            System.Int32 total = checked(dataSize + 4);

            if (target.Capacity < total)
            {
                throw new SerializationException($"Buffer too small. Required: {total}, Actual: {target.Capacity}");
            }

            ref System.Byte dst = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(target.SpanFull);
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref dst, length);
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref dst, 4),
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array),
                (System.UInt32)dataSize);

            target.CommitLength(total);
            return total;
        }

        // Formatter path
        DataWriter writer = new(target.SpanFull);
        try
        {
            FormatterProvider.Get<T>().Serialize(ref writer, value);
            if (writer.WrittenCount > target.Capacity)
            {
                throw new SerializationException($"Buffer too small. Required: {writer.WrittenCount}, Actual: {target.Capacity}");
            }

            target.CommitLength(writer.WrittenCount);

            return value == null
                ? throw new SerializationException(
                    $"Deserialization of non-nullable unmanaged type '{typeof(T)}' resulted in null value.")
                : writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Deserializes a value directly from the valid payload of a <see cref="BufferLease"/>.
    /// </summary>
    /// <typeparam name="T">Type to deserialize.</typeparam>
    /// <param name="source">Source lease; uses <see cref="BufferLease.Memory"/> as input.</param>
    /// <param name="value">Destination variable receiving the result.</param>
    /// <returns>Number of bytes read.</returns>
    /// <exception cref="SerializationException">If deserialization fails or buffer is insufficient.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 Deserialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] BufferLease source,
        [System.Diagnostics.CodeAnalysis.NotNull] ref T value)
    {
        System.ArgumentNullException.ThrowIfNull(source);
        return Deserialize<T>(source.Memory.Span, ref value);
    }

    /// <summary>
    /// Deserializes a value directly from the valid payload of a <see cref="BufferLease"/>.
    /// </summary>
    /// <typeparam name="T">Type to deserialize.</typeparam>
    /// <param name="source">Source lease; uses <see cref="BufferLease.Memory"/> as input.</param>
    /// <param name="bytesRead">Outputs number of bytes consumed.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="SerializationException">If deserialization fails or buffer is insufficient.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public static T Deserialize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] BufferLease source,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 bytesRead)
    {
        System.ArgumentNullException.ThrowIfNull(source);
        return Deserialize<T>(source.Memory.Span, out bytesRead);
    }

    #endregion Lease APIs

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