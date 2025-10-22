// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Nalix.Common.Identity;
using Nalix.Common.Primitives;

namespace Nalix.Framework.Identifiers;

public readonly partial struct Snowflake
{
    #region Deserialize

    /// <summary>
    /// Creates a <see cref="Snowflake"/> identifier from a 56-bit combined value.
    /// </summary>
    /// <param name="combined">The 56-bit unsigned integer containing all identifier components.</param>
    /// <returns>A <see cref="Snowflake"/> instance constructed from the combined value.</returns>
    /// <remarks>
    /// This is the most efficient deserialization method as it directly uses the pre-composed value
    /// without any decomposition or validation. Use this when the value is known to be valid.
    /// </remarks>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromUInt56(UInt56 combined) => NewId(combined);

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte span.
    /// </summary>
    /// <param name="bytes">The byte span containing the identifier data in little-endian format.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bytes"/> is not exactly <see cref="Size"/> (7) bytes.
    /// </exception>
    /// <remarks>
    /// This method deserializes a <see cref="Snowflake"/> from a byte representation.
    /// The expected layout is: [0-3]=Value (32 bits), [4-5]=MachineId (16 bits), [6]=Type (8 bits),
    /// all in little-endian byte order. The method validates the buffer size before reading.
    /// </remarks>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromBytes(ReadOnlySpan<byte> bytes)
    {
        // Input validation - buffer overflow protection
        if (bytes.Length != Size)
        {
            throw new ArgumentException(
                $"Input buffer must be exactly {Size} bytes. Received {bytes.Length} bytes.",
                nameof(bytes));
        }

        // Optimized deserialization using BinaryPrimitives (bounds-checked, vectorized)
        uint value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        ushort machineId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
        byte type = bytes[6];

        // Cast directly without additional validation for performance
        return new Snowflake(value, machineId, (SnowflakeType)type);
    }

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data in little-endian format.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="bytes"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bytes"/> is not exactly <see cref="Size"/> (7) bytes.
    /// </exception>
    /// <remarks>
    /// This overload accepts a byte array and delegates to the span-based <see cref="FromBytes(ReadOnlySpan{byte})"/> method.
    /// Prefer using the span-based overload when possible to avoid unnecessary array allocations.
    /// </remarks>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromBytes(byte[] bytes)
    {
        return bytes is null
            ? throw new ArgumentNullException(nameof(bytes), "Byte array cannot be null.")
            : FromBytes(MemoryExtensions.AsSpan(bytes));
    }

    #endregion Deserialize

    #region Serialization

    /// <inheritdoc/>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public UInt56 ToUInt56() => __combined;

    /// <inheritdoc/>
    [EditorBrowsable(
        EditorBrowsableState.Never)]
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public byte[] ToByteArray()
    {
        byte[] result = new byte[Size];
        _ = TryWriteBytes(result);
        return result;
    }

    /// <inheritdoc/>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBytes(
        [NotNull] Span<byte> destination,
        [NotNullWhen(true)] out int bytesWritten)
    {
        // Buffer overflow protection - validate size before writing
        if (destination.Length < Size)
        {
            bytesWritten = 0;
            return false;
        }

        // Optimized serialization using direct property access and BinaryPrimitives
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);
        destination[6] = (byte)Type;

        bytesWritten = Size;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBytes(Span<byte> destination)
    {
        // Buffer overflow protection - validate size before writing
        if (destination.Length < Size)
        {
            return false;
        }

        // Optimized serialization using direct property access and BinaryPrimitives
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);
        destination[6] = (byte)Type;

        return true;
    }

    #endregion Serialization
}
