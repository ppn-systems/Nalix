// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Primitives;

namespace Nalix.Framework.Identity;

public readonly partial struct Snowflake
{
    #region Deserialize

    /// <summary>
    /// Creates an identifier from a 56-bit combined value. Upper 8 bits must be zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Snowflake FromUInt56(UInt56 combined) => NewId(combined);

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
        System.Byte type = bytes[6];
        System.UInt32 value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        System.UInt16 machineId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));

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

    #endregion Deserialize

    #region Serialization

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public UInt56 ToUInt56()
    {
        return __combined;
    }

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
        destination[6] = (System.Byte)Type;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);

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

        destination[6] = (System.Byte)Type;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MachineId);

        return true;
    }

    #endregion Serialization
}
