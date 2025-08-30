// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Enums;
using Nalix.Common.Core.Primitives;

namespace Nalix.Framework.Identity;

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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromUInt56(UInt56 combined) => NewId(combined);

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte span.
    /// </summary>
    /// <param name="bytes">The byte span containing the identifier data in little-endian format.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="bytes"/> is not exactly <see cref="Size"/> (7) bytes.
    /// </exception>
    /// <remarks>
    /// This method deserializes a <see cref="Snowflake"/> from a byte representation.
    /// The expected layout is: [0-3]=Value (32 bits), [4-5]=MachineId (16 bits), [6]=Type (8 bits),
    /// all in little-endian byte order. The method validates the buffer size before reading.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromBytes(System.ReadOnlySpan<System.Byte> bytes)
    {
        // Input validation - buffer overflow protection
        if (bytes.Length != Size)
        {
            throw new System.ArgumentException(
                $"Input buffer must be exactly {Size} bytes. Received {bytes.Length} bytes.",
                nameof(bytes));
        }

        // Optimized deserialization using BinaryPrimitives (bounds-checked, vectorized)
        System.UInt32 value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        System.UInt16 machineId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
        System.Byte type = bytes[6];

        // Cast directly without additional validation for performance
        return new Snowflake(value, machineId, (SnowflakeType)type);
    }

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data in little-endian format.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="bytes"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="bytes"/> is not exactly <see cref="Size"/> (7) bytes.
    /// </exception>
    /// <remarks>
    /// This overload accepts a byte array and delegates to the span-based <see cref="FromBytes(System.ReadOnlySpan{System.Byte})"/> method.
    /// Prefer using the span-based overload when possible to avoid unnecessary array allocations.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake FromBytes(System.Byte[] bytes)
    {
        return bytes is null
            ? throw new System.ArgumentNullException(nameof(bytes), "Byte array cannot be null.")
            : FromBytes(System.MemoryExtensions.AsSpan(bytes));
    }

    #endregion Deserialize

    #region Serialization

    /// <summary>
    /// Converts this <see cref="Snowflake"/> to its underlying 56-bit representation.
    /// </summary>
    /// <returns>The 56-bit unsigned integer value of this identifier.</returns>
    /// <remarks>
    /// This is the most efficient serialization method as it returns the internal representation directly.
    /// Use this when you need to store or transmit the identifier in a compact binary format.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public UInt56 ToUInt56() => __combined;

    /// <summary>
    /// Converts this <see cref="Snowflake"/> to a 7-byte array.
    /// </summary>
    /// <returns>A new byte array containing the serialized identifier.</returns>
    /// <remarks>
    /// This method allocates a new 7-byte array and writes the identifier in little-endian format.
    /// The layout is: [0-3]=Value, [4-5]=MachineId, [6]=Type.
    /// For better performance, use <see cref="TryWriteBytes(System.Span{System.Byte})"/> with a pre-allocated buffer.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ToByteArray()
    {
        System.Byte[] result = new System.Byte[Size];
        _ = TryWriteBytes(result);
        return result;
    }

    /// <summary>
    /// Attempts to write the serialized <see cref="Snowflake"/> to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes written to <paramref name="destination"/>.
    /// This is always <see cref="Size"/> (7) on success, or 0 on failure.
    /// </param>
    /// <returns>
    /// <c>true</c> if the identifier was successfully serialized; <c>false</c> if <paramref name="destination"/>
    /// is too small (less than <see cref="Size"/> bytes).
    /// </returns>
    /// <remarks>
    /// This method writes the identifier in little-endian format: [0-3]=Value, [4-5]=MachineId, [6]=Type.
    /// The method validates the destination buffer size before writing to prevent buffer overflows.
    /// This is the recommended serialization method for performance-critical scenarios.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryWriteBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> destination,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 bytesWritten)
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
        destination[6] = (System.Byte)Type;

        bytesWritten = Size;
        return true;
    }

    /// <summary>
    /// Attempts to write the serialized <see cref="Snowflake"/> to the specified byte span.
    /// </summary>
    /// <param name="destination">The span to write the serialized bytes to.</param>
    /// <returns>
    /// <c>true</c> if the identifier was successfully serialized; <c>false</c> if <paramref name="destination"/>
    /// is too small (less than <see cref="Size"/> bytes).
    /// </returns>
    /// <remarks>
    /// This overload is identical to <see cref="TryWriteBytes(System.Span{System.Byte}, out System.Int32)"/> but does not
    /// return the number of bytes written. Use this when you don't need the byte count.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryWriteBytes(System.Span<System.Byte> destination)
    {
        // Buffer overflow protection - validate size before writing
        if (destination.Length < Size)
        {
            return false;
        }

        // Optimized serialization using direct property access and BinaryPrimitives
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);
        destination[6] = (System.Byte)Type;

        return true;
    }

    #endregion Serialization
}
