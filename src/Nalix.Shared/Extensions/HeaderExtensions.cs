using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides high-performance helpers for reading header fields from serialized packet data.
/// </summary>
/// <remarks>
/// This API operates directly on <see cref="System.ReadOnlySpan{T}"/> to avoid allocations and supports both
/// unsafe generic reads and explicit little-endian typed reads.<br/>
/// <b>Endianness:</b>,
/// the value is read as the machine's native endianness (typically little-endian on x86/x64/ARM64).
/// For protocol stability, prefer the explicit Little-Endian helpers (e.g.,..).
/// </remarks>
public static class HeaderExtensions
{
    #region Unsafe generic reader

    /// <summary>
    /// Reads an unmanaged value of type <typeparamref name="T"/> from <paramref name="buffer"/> at the specified byte <paramref name="offset"/>.
    /// </summary>
    /// <typeparam name="T">An unmanaged value type (kiểu value không quản lý) to read.</typeparam>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The zero-based byte offset at which the value starts.</param>
    /// <returns>The value read from the buffer.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the value at the offset.</exception>
    /// <remarks>
    /// This method reads using the platform's native endianness. For protocol consistency across platforms,
    /// prefer typed Little-Endian helpers below.
    /// </remarks>
    public static unsafe T ReadValue<T>(this System.ReadOnlySpan<System.Byte> buffer, System.Int32 offset) where T : unmanaged
    {
        System.Int32 size = sizeof(T);
        if ((System.UInt32)(offset + size) > (System.UInt32)buffer.Length)
        {
            throw new System.ArgumentException($"Buffer is too small to contain {typeof(T).Name} at offset {offset}.");
        }

        fixed (System.Byte* p = buffer)
        {
            return *(T*)(p + offset);
        }
    }

    #endregion

    #region Safe typed Little-Endian readers

    /// <summary>Reads a 32-bit unsigned integer (Little-Endian) at offset 0 (MagicNumber).</summary>
    public static System.UInt32 ReadMagicNumberLE(this System.ReadOnlySpan<System.Byte> buffer)
    {
        CheckSize(buffer, PacketHeaderOffset.MagicNumber.AsByte(), sizeof(System.UInt32));
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    /// <summary>Reads a 16-bit unsigned integer (Little-Endian) at offset 4 (OpCode).</summary>
    public static System.UInt16 ReadOpCodeLE(this System.ReadOnlySpan<System.Byte> buffer)
    {
        CheckSize(buffer, PacketHeaderOffset.OpCode.AsByte(), sizeof(System.UInt16));
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..]);
    }

    /// <summary>Reads <see cref="PacketFlags"/> (Little-Endian) at offset 6.</summary>
    public static PacketFlags ReadFlagsLE(this System.ReadOnlySpan<System.Byte> buffer)
    {
        CheckSize(buffer, PacketHeaderOffset.Flags.AsByte(), sizeof(System.UInt16));
        return (PacketFlags)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..]);
    }

    /// <summary>Reads <see cref="PacketPriority"/> (Little-Endian) at offset 8.</summary>
    public static PacketPriority ReadPriorityLE(this System.ReadOnlySpan<System.Byte> buffer)
    {
        CheckSize(buffer, PacketHeaderOffset.Priority.AsByte(), sizeof(System.UInt16));
        return (PacketPriority)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer[8..]);
    }

    /// <summary>Reads <see cref="TransportProtocol"/> (Little-Endian) at offset 10.</summary>
    public static TransportProtocol ReadTransportLE(this System.ReadOnlySpan<System.Byte> buffer)
    {
        CheckSize(buffer, PacketHeaderOffset.Transport.AsByte(), sizeof(System.UInt16));
        return (TransportProtocol)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer[10..]);
    }

    #endregion

    #region Byte block copy

    /// <summary>
    /// Copies a block of bytes from <paramref name="buffer"/> starting at <paramref name="offset"/> with the specified <paramref name="length"/>.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The zero-based offset at which to start copying.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <returns>A new array containing the copied bytes.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the requested range exceeds the buffer bounds.</exception>
    public static unsafe System.Byte[] ReadBytes(
        this System.ReadOnlySpan<System.Byte> buffer, System.Int32 offset, System.Int32 length)
    {
        if ((System.UInt32)(offset + length) > (System.UInt32)buffer.Length)
        {
            throw new System.ArgumentException($"Buffer is too small to contain {length} bytes at offset {offset}.");
        }

        System.Byte[] result = new System.Byte[length];

        fixed (System.Byte* pSrc = buffer)
        fixed (System.Byte* pDst = result)
        {
            System.Buffer.MemoryCopy(pSrc + offset, pDst, length, length);
        }

        return result;
    }

    #endregion

    #region Convenience overloads for byte[]

    /// <summary>Reads OpCode (Little-Endian) from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 ReadOpCodeLE(this System.Byte[] buffer) => buffer.AsReadOnlySpan().ReadOpCodeLE();

    /// <summary>Reads MagicNumber (Little-Endian) from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 ReadMagicNumberLE(this System.Byte[] buffer) => buffer.AsReadOnlySpan().ReadMagicNumberLE();

    /// <summary>Reads <see cref="PacketFlags"/> (Little-Endian) from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketFlags ReadFlagsLE(this System.Byte[] buffer) => buffer.AsReadOnlySpan().ReadFlagsLE();

    /// <summary>Reads <see cref="PacketPriority"/> (Little-Endian) from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketPriority ReadPriorityLE(this System.Byte[] buffer) => buffer.AsReadOnlySpan().ReadPriorityLE();

    /// <summary>Reads <see cref="TransportProtocol"/> (Little-Endian) from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TransportProtocol ReadTransportLE(this System.Byte[] buffer) => buffer.AsReadOnlySpan().ReadTransportLE();

    /// <summary>Copies a range of bytes from a byte array.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] ReadBytes(this System.Byte[] buffer, System.Int32 offset, System.Int32 length)
        => buffer.AsReadOnlySpan().ReadBytes(offset, length);

    #endregion

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void CheckSize(System.ReadOnlySpan<System.Byte> buffer, System.Int32 offset, System.Int32 size)
    {
        if ((System.UInt32)(offset + size) > (System.UInt32)buffer.Length)
        {
            throw new System.ArgumentException($"Buffer is too small to read {size} bytes at offset {offset}.");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.ReadOnlySpan<System.Byte> AsReadOnlySpan(this System.Byte[] buffer)
        => System.MemoryExtensions.AsSpan(buffer);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Byte AsByte(this PacketHeaderOffset position) => (System.Byte)position;

    #endregion
}
