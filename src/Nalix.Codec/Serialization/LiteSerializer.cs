// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.Internal;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization.Formatters.Automatic;
using Nalix.Codec.Serialization.Formatters.Primitives;
using Nalix.Codec.Serialization.Internal;
using Nalix.Codec.Serialization.Internal.Types;

#pragma warning disable IDE0021 // Use expression body for constructor

namespace Nalix.Codec.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
[DebuggerStepThrough]
public static class LiteSerializer
{
    #region Constructors

    static LiteSerializer()
    {
        /*
         * [Initialization]
         * The static constructor ensures that the FormatterProvider is initialized
         * before any serialization operations are performed. This is crucial for
         * the formatter resolution mechanism to work correctly.
         */
        Register(new Bytes32Formatter());
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Registers a formatter for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the formatter is being registered.</typeparam>
    /// <param name="formatter">The formatter to register.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided formatter is null.
    /// </exception>
    public static void Register<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(
        IFormatter<T> formatter) => FormatterProvider.Register(formatter);

    /// <summary>
    /// Serializes an object into a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required formatter dependency is null during registration or serialization dispatch.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no formatter is available for a fixed-size or variable-length serializable type.
    /// </exception>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public static byte[] Serialize<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(in T value)
    {
        /*
         * [Fast Path: Unmanaged Types]
         * If the type is unmanaged (blittable), we can simply write its bytes 
         * directly into a fresh buffer. No formatter is needed.
         */
        if (TypeMetadata.IsUnmanaged<T>())
        {
            byte[] array = GC.AllocateUninitializedArray<byte>(TypeMetadata.SizeOf<T>());
            Unsafe.WriteUnaligned(
                ref MemoryMarshal.GetArrayDataReference(array), value);

            return array;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            /*
             * [Optimization: Unmanaged Arrays]
             * For arrays of unmanaged types (e.g. byte[], int[]), we write:
             * [4-byte length][Bulk data copy]
             * This avoids per-element overhead and uses bulk Memory Copy.
             */
            if (value is null)
            {
                return SerializerBounds.NullArrayMarker.ToArray();
            }

            Array array = (Array)(object)value;
            int length = array.Length;
            if (length is 0)
            {
                return SerializerBounds.EmptyArrayMarker.ToArray();
            }

            long dataSizeLong = (long)size * length;
            if (dataSizeLong > int.MaxValue - 4)
            {
                throw CodecErrors.SerializationOverflow;
            }

            int dataSize = (int)dataSizeLong;
            byte[] buffer = GC.AllocateUninitializedArray<byte>(dataSize + 4);
            ref byte ptr = ref MemoryMarshal.GetArrayDataReference(buffer);

            Unsafe.WriteUnaligned(ref ptr, length);
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(ref ptr, 4),
                ref MemoryMarshal.GetArrayDataReference(array), (uint)dataSize);

            return buffer;
        }
        else if (kind is TypeKind.FixedSizeSerializable)
        {
            IFormatter<T> formatter = ResolveRootFormatter<T>(value);
            DataWriter writer = new(2048);

            try
            {
                formatter.Serialize(ref writer, value);
                return writer.WrittenCount == 0 ? Array.Empty<byte>() : writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        else if (kind is TypeKind.None)
        {
            IFormatter<T> formatter = ResolveRootFormatter<T>(value);
            DataWriter writer = new(2048);

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
            throw new SerializationFailureException($"TYPE {typeof(T).FullName} is not serializable.");
        }
    }

    /// <summary>
    /// Serializes an object into the provided span.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="buffer">The target span to write the serialized data into.</param>
    /// <returns>The number of bytes written into the buffer.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if serialization fails or the buffer is too small.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no formatter is available for a fixed-size serializable type.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the type is not supported for span-based serialization.
    /// </exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Serialize<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(in T value, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Primitive or unmanaged struct
        if (TypeMetadata.IsUnmanaged<T>())
        {
            int size = TypeMetadata.SizeOf<T>();
            if (buffer.Length < size)
            {
                throw CodecErrors.SerializationBufferTooSmall;
            }

            Unsafe.WriteUnaligned(
                ref MemoryMarshal.GetArrayDataReference(buffer), value);

            return size;
        }

        // Reference or Nullable/Complex types
        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int fixedSize);

        if (kind is TypeKind.FixedSizeSerializable)
        {
            int required = fixedSize;

            if (buffer.Length < required)
            {
                throw CodecErrors.SerializationBufferTooSmall;
            }

            IFormatter<T> formatter = ResolveRootFormatter<T>(value);
            DataWriter writer = new(buffer);
            try
            {
                formatter.Serialize(ref writer, value);

                return writer.WrittenCount;
            }
            finally
            {
                writer.Dispose();
            }
        }

        throw new SerializationFailureException(
            $"Array-based serialization is not supported for type {typeof(T)}. Use Serialize<T>(in T) to get byte[] instead.");
    }

    /// <summary>
    /// Serializes an object into the provided span.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="buffer">The target span to write the serialized data into.</param>
    /// <returns>The number of bytes written into the buffer.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if serialization fails or the buffer is too small.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no formatter is available for the resolved formatter-based serialization path.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the type is not supported for span-based serialization.
    /// </exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Serialize<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(in T value, Span<byte> buffer)
    {
        // ── Case 1: Primitive / unmanaged struct ──────────────────────────────────
        // T is a plain value type with no references (e.g. int, float, custom struct).
        // Write the value directly into the span using unaligned write for performance.
        if (TypeMetadata.IsUnmanaged<T>())
        {
            int size = TypeMetadata.SizeOf<T>();

            if (buffer.Length < size)
            {
                throw CodecErrors.SerializationBufferTooSmall;
            }

            Unsafe.WriteUnaligned(
                ref MemoryMarshal.GetReference(buffer), value);

            return size;
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int fixedSize);

        // ── Case 2: Unmanaged single-dimensional array ────────────────────────────
        // T is something like int[], byte[], float[].
        // Layout: [4-byte length prefix][element data...]
        // Special cases: null  -> NullArrayMarker  [255,255,255,255]
        //                empty -> EmptyArrayMarker [0,0,0,0]
        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (value is null)
            {
                // Write null-array marker (4 bytes: 0xFF 0xFF 0xFF 0xFF)
                if (buffer.Length < 4)
                {
                    throw CodecErrors.SerializationBufferTooSmall;
                }

                SerializerBounds.NullArrayMarker.CopyTo(buffer);
                return 4;
            }

            Array array = (Array)(object)value;
            int length = array.Length;

            if (length == 0)
            {
                // Write empty-array marker (4 bytes: 0x00 0x00 0x00 0x00)
                if (buffer.Length < 4)
                {
                    throw CodecErrors.SerializationBufferTooSmall;
                }

                SerializerBounds.EmptyArrayMarker.CopyTo(buffer);
                return 4;
            }

            long dataSizeLong = (long)fixedSize * length;
            if (buffer.Length < dataSizeLong + 4)
            {
                throw CodecErrors.SerializationBufferTooSmall;
            }

            int dataSize = (int)dataSizeLong;
            int totalSize = dataSize + 4; // Safe now because of the check above

            // Write the element count as a 4-byte little-endian prefix
            Unsafe.WriteUnaligned(
                ref MemoryMarshal.GetReference(buffer), length);

            // Bulk-copy all element bytes directly into the span (after the prefix)
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(
                    ref MemoryMarshal.GetReference(buffer), 4),
                ref MemoryMarshal.GetArrayDataReference(array),
                (uint)dataSize);

            return totalSize;
        }

        // ── Case 3: Fixed-size serializable (implements IFixedSizeSerializable) ───
        // T declares a compile-time-known byte size via IFixedSizeSerializable.Size.
        // We can safely wrap the caller's span in a DataWriter without the risk of Expand().
        if (kind is TypeKind.FixedSizeSerializable)
        {
            if (buffer.Length < fixedSize)
            {
                throw CodecErrors.SerializationBufferTooSmall;
            }

            // DataWriter(Span<byte>) wraps the span directly — zero allocation, no pool renting.
            // The formatter must not write more than fixedSize bytes, so Expand() is never called.

            DataWriter writer = new(buffer);
            ResolveRootFormatter<T>(value).Serialize(ref writer, value);
            return writer.WrittenCount;
        }

        // ── Case 4: TypeKind.None — variable-length / composite types ────────────
        // Cannot determine required buffer size at compile time.
        // Wrap the caller's span directly in DataWriter — zero allocation, no intermediate copy.
        // If the formatter writes more than buffer.Length, DataWriter will throw InvalidOperationException
        // (because Span-based DataWriter cannot Expand()).
        if (kind is TypeKind.None)
        {
            IFormatter<T> formatter = ResolveRootFormatter<T>(value);

            // DataWriter(Span<byte>) wraps the span directly — no renting, no pool.
            // Expand() is disabled: if formatter overflows, it throws InvalidOperationException.
            DataWriter writer = new(buffer);

            try
            {
                formatter.Serialize(ref writer, value);
                return writer.WrittenCount;
            }
            finally
            {
                writer.Dispose();
            }
        }

        throw new SerializationFailureException(
            $"Span<byte> serialization is not supported for variable-length type '{typeof(T).FullName}'. Use Serialize<T>(in T value) to obtain a byte[] instead.");
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The byte array containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if deserialization encounters an error or if there is insufficient data in the buffer.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown if the buffer is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no formatter is available for formatter-based deserialization.
    /// </exception>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static int Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] buffer, ref T value)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (!UsesFormatterReader<T>())
        {
            return Deserialize<T>((ReadOnlySpan<byte>)buffer, ref value);
        }

        if (buffer.Length == 0)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        IFormatter<T> formatter = RootFormatterCache<T>.Formatter;
        IFillableFormatter<T>? fillable = RootFormatterCache<T>.Fillable;

        DataReader reader = new(buffer);
        if (value is not null && fillable is not null)
        {
            fillable.Fill(ref reader, value);
        }
        else
        {
            value = formatter.Deserialize(ref reader);
        }

        return reader.BytesRead;
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The byte array containing serialized data.</param>
    /// <param name="value">The number of bytes read during deserialization.</param>
    /// <returns>The deserialized object.</returns>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public static T Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] buffer, out int value)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (!UsesFormatterReader<T>())
        {
            return Deserialize<T>((ReadOnlySpan<byte>)buffer, out value);
        }

        if (buffer.Length == 0)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        IFormatter<T> formatter = ResolveRootFormatterForRead<T>();
        DataReader reader = new(buffer);
        T result = formatter.Deserialize(ref reader);
        value = reader.BytesRead;
        return result;
    }

    /// <summary>
    /// Deserializes an object from a read-only memory buffer.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The memory buffer containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static int Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlyMemory<byte> buffer, ref T value)
    {
        if (!UsesFormatterReader<T>())
        {
            return Deserialize<T>(buffer.Span, ref value);
        }

        if (buffer.IsEmpty)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        IFormatter<T> formatter = RootFormatterCache<T>.Formatter;
        IFillableFormatter<T>? fillable = RootFormatterCache<T>.Fillable;

        DataReader reader = new(buffer);
        if (value is not null && fillable is not null)
        {
            fillable.Fill(ref reader, value);
        }
        else
        {
            value = formatter.Deserialize(ref reader);
        }

        return reader.BytesRead;
    }

    /// <summary>
    /// Deserializes an object from a read-only memory buffer.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The memory buffer containing serialized data.</param>
    /// <param name="value">The number of bytes read during deserialization.</param>
    /// <returns>The deserialized object.</returns>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public static T Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlyMemory<byte> buffer, out int value)
    {
        if (!UsesFormatterReader<T>())
        {
            return Deserialize<T>(buffer.Span, out value);
        }

        if (buffer.IsEmpty)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        IFormatter<T> formatter = ResolveRootFormatterForRead<T>();
        DataReader reader = new(buffer);
        T result = formatter.Deserialize(ref reader);
        value = reader.BytesRead;
        return result;
    }

    /// <summary>
    /// Deserializes an object from a read-only span of bytes.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The span containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static int Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> buffer, ref T value)
    {
        if (buffer.IsEmpty)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        if (TypeMetadata.IsUnmanaged<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw CodecErrors.SerializationEndOfStream;
            }
            value = Unsafe.ReadUnaligned<T>(
                ref MemoryMarshal.GetReference(buffer));

            return TypeMetadata.SizeOf<T>();
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (IsNullArrayMarker(buffer))
            {
                value = (T)(object)null!;
                return 4;
            }

            Type elementType = typeof(T).GetElementType()
                ?? throw new SerializationFailureException(
                    $"TYPE '{typeof(T)}' is expected to be an array, but element type could not be resolved."
                );

            if (IsEmptyArrayMarker(buffer))
            {
                value = (T)(object)Array.CreateInstance(elementType, 0);
                return 4;
            }

            if (buffer.Length < 4)
            {
                throw CodecErrors.SerializationEndOfStream;
            }

            int length = Unsafe.ReadUnaligned<int>(
                ref MemoryMarshal.GetReference(buffer));

            if (length < 0 || length > SerializationStaticOptions.Instance.MaxArrayLength)
            {
                throw new SerializationFailureException(
                    $"Array length {length} is out of allowed range [0, {SerializationStaticOptions.Instance.MaxArrayLength}] for type '{typeof(T)}'. (Config: Serialization.MaxArrayLength)");
            }

            // Calculate total data size and verify the buffer has enough bytes 
            // BEFORE attempting any heap allocation to prevent OOM/DoS.
            long dataSizeLong = (long)size * length;
            if (buffer.Length < dataSizeLong + 4)
            {
                throw CodecErrors.SerializationEndOfStream;
            }

            int dataSize = (int)dataSizeLong;

            // Allocation: Using GC.AllocateUninitializedArray is safer and faster for large blocks
            // as it avoids the zero-fill overhead (which can be a DoS vector for large MaxArray).
            Array arr;
            if (typeof(T) == typeof(byte[]))
            {
                arr = GC.AllocateUninitializedArray<byte>(length);
            }
            else
            {
                arr = Array.CreateInstance(elementType, length);
            }

            ref byte dest = ref MemoryMarshal.GetArrayDataReference(arr);

            Unsafe.CopyBlockUnaligned(
                ref dest,
                ref Unsafe.Add(
                ref MemoryMarshal.GetReference(buffer), 4), (uint)dataSize);

            value = (T)(object)arr;
            return dataSize + 4;
        }

        IFormatter<T> formatter = RootFormatterCache<T>.Formatter;
        IFillableFormatter<T>? fillable = RootFormatterCache<T>.Fillable;

        DataReader reader = new(buffer);
        if (value is not null && fillable is not null)
        {
            fillable.Fill(ref reader, value);
        }
        else
        {
            value = formatter.Deserialize(ref reader);
        }

        return reader.BytesRead;
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The byte array containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if deserialization encounters an error or if there is insufficient data in the buffer.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown if the buffer is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no formatter is available for formatter-based deserialization.
    /// </exception>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public static T Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> buffer, out int value)
    {
        if (buffer.IsEmpty)
        {
            throw CodecErrors.SerializationEmptyBuffer;
        }

        if (TypeMetadata.IsUnmanaged<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw CodecErrors.SerializationEndOfStream;
            }
            value = TypeMetadata.SizeOf<T>();

            return Unsafe.ReadUnaligned<T>(
                ref MemoryMarshal.GetReference(buffer));
        }

        TypeKind kind = TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int size);

        if (kind is TypeKind.UnmanagedSZArray)
        {
            if (IsNullArrayMarker(buffer))
            {
                value = 4;
                return default;
            }

            Type elementType = typeof(T).GetElementType()
                ?? throw new SerializationFailureException(
                    $"TYPE '{typeof(T)}' is expected to be an array, but element type could not be resolved."
                );

            if (IsEmptyArrayMarker(buffer))
            {
                value = 4;
                return (T)(object)Array.CreateInstance(elementType, 0);
            }

            if (buffer.Length < 4)
            {
                throw CodecErrors.SerializationEndOfStream;
            }

            int length = Unsafe.ReadUnaligned<int>(
                ref MemoryMarshal.GetReference(buffer));

            if (length < 0 || length > SerializationStaticOptions.Instance.MaxArrayLength)
            {
                throw new SerializationFailureException(
                    $"Array length {length} is out of allowed range [0, {SerializationStaticOptions.Instance.MaxArrayLength}] for type '{typeof(T)}'. (Config: Serialization.MaxArrayLength)");
            }

            // Safety check: Ensure the buffer actually contains the promised data size
            // BEFORE we allocate memory on the heap.
            long dataSizeLong = (long)size * length;
            if (buffer.Length < dataSizeLong + 4)
            {
                throw CodecErrors.SerializationEndOfStream;
            }

            int dataSize = (int)dataSizeLong;

            // Allocation: Using uninitialized arrays to avoid zero-filling overhead.
            Array arr;
            if (typeof(T) == typeof(byte[]))
            {
                arr = GC.AllocateUninitializedArray<byte>(length);
            }
            else
            {
                arr = Array.CreateInstance(elementType, length);
            }

            ref byte dest = ref MemoryMarshal.GetArrayDataReference(arr);

            Unsafe.CopyBlockUnaligned(
                ref dest,
                ref Unsafe.Add(
                ref MemoryMarshal.GetReference(buffer), 4), (uint)dataSize);

            value = dataSize + 4;
            return (T)(object)arr;
        }

        IFormatter<T> formatter = ResolveRootFormatterForRead<T>();
        DataReader reader = new(buffer);
        T result = formatter.Deserialize(ref reader);
        value = reader.BytesRead;
        return result;
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNullArrayMarker(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 4 && Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(buffer)) == SerializerBounds.Null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEmptyArrayMarker(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 4 && Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(buffer)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UsesFormatterReader<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => !TypeMetadata.IsUnmanaged<T>() &&
           TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out _) is not TypeKind.UnmanagedSZArray;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> ResolveRootFormatter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(in T value)
    {
        /*
         * [Formatter Resolution]
         * We use a static generic cache (RootFormatterCache<T>) to resolve 
         * formatters. This eliminates dictionary lookups on every 
         * serialization call.
         */
        if (RootFormatterCache<T>.ThrowsOnNull && value is null)
        {
            throw new SerializationFailureException(
                $"Cannot serialize null reference type '{typeof(T).FullName}' without an explicit nullable wrapper.");
        }

        return RootFormatterCache<T>.Formatter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IFormatter<T> ResolveRootFormatterForRead<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => RootFormatterCache<T>.Formatter;

    private static class RootFormatterCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    {
        public static readonly bool ThrowsOnNull;
        public static readonly IFormatter<T> Formatter;
        public static readonly IFillableFormatter<T>? Fillable;

        static RootFormatterCache()
        {
            IFormatter<T> formatter = FormatterProvider.Get<T>();

            if (typeof(T).IsClass &&
                typeof(T) != typeof(string) &&
                formatter.GetType().IsGenericType &&
                formatter.GetType().GetGenericTypeDefinition() == typeof(NullableObjectFormatter<>))
            {
                ThrowsOnNull = true;
                Formatter = FormatterProvider.GetComplex<T>();
            }
            else
            {
                ThrowsOnNull = false;
                Formatter = formatter;
            }

            Fillable = Formatter as IFillableFormatter<T>;
        }
    }

    #endregion Private Methods
}
