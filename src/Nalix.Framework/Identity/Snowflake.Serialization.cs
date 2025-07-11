// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Framework.Identity;

public readonly partial struct Snowflake
{
    #region Deserialize Methods

    /// <summary>
    /// Creates an identifier from a 56-bit combined value. Upper 8 bits must be zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Snowflake FromUInt56(System.UInt64 combined)
    {
        if ((combined & ~__unit56) != 0UL)
        {
            throw new System.ArgumentOutOfRangeException(nameof(combined), "Must fit in 56 bits.");
        }

        System.UInt32 v = (System.UInt32)(combined & 0xFFFFFFFFUL);
        System.UInt16 m = (System.UInt16)((combined >> 32) & 0xFFFFUL);
        System.Byte t = (System.Byte)((combined >> 48) & 0xFFUL);
        return NewId(v, m, (SnowflakeType)t);
    }

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Snowflake FromBytes(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new System.ArgumentException("Input must be exactly 7 bytes.", nameof(bytes));
        }

        // Read using BinaryPrimitives (fast, optimized)
        System.UInt32 value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        System.UInt16 machineId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
        System.Byte type = bytes[6];

        return new Snowflake(value, machineId, (SnowflakeType)type);
    }

    /// <summary>
    /// Creates a <see cref="Snowflake"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Snowflake"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Snowflake FromBytes(System.Byte[] bytes) => FromBytes(System.MemoryExtensions.AsSpan(bytes));

    #endregion Deserialize Methods

    #region Serialization Methods

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.UInt64 ToUInt56() => ((System.UInt64)_type << 48) | ((System.UInt64)MachineId << 32) | Value;

    /// <inheritdoc/>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] ToByteArray()
    {
        System.Byte[] result = new System.Byte[Size];
        _ = TryWriteBytes(result, out _);
        return result;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryWriteBytes(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> destination,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 bytesWritten)
    {
        if (destination.Length < Size)
        {
            bytesWritten = 0;
            return false;
        }

        // Write little-endian directly for best performance
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);
        destination[6] = _type;

        bytesWritten = Size;
        return true;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryWriteBytes(System.Span<System.Byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);
        destination[6] = _type;

        return true;
    }

    #endregion Serialization Methods
}
