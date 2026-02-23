// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Types;

namespace Nalix.Shared.Serialization;

public static partial class LiteSerializer
{
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(
        [System.Diagnostics.CodeAnalysis.NotNull] BufferLease source,
        [System.Diagnostics.CodeAnalysis.NotNull] out System.Int32 bytesRead)
    {
        System.ArgumentNullException.ThrowIfNull(source);
        return Deserialize<T>(source.Memory.Span, out bytesRead);
    }
}