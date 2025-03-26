using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Integrity;

/// <summary>
/// A high-performance CRC-8 implementation using polynomial x^8 + x^7 + x^6 + x^4 + x^2 + 1
/// </summary>
public static class Crc8
{
    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte HashToByte(params byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            throw new ArgumentException("Bytes array cannot be null or empty", nameof(bytes));

        return HashToByte(bytes.AsSpan());
    }

    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte HashToByte(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Bytes span cannot be empty", nameof(bytes));

        byte crc = 0xFF;

        // Process bytes in chunks when possible
        if (bytes.Length >= 8)
        {
            int unalignedBytes = bytes.Length % 8;
            int alignedLength = bytes.Length - unalignedBytes;

            for (int i = 0; i < alignedLength; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (int i = alignedLength; i < bytes.Length; i++)
            {
                crc = Crc.TableCrc8[crc ^ bytes[i]];
            }
        }
        else
        {
            // Process small arrays with simple loop
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = Crc.TableCrc8[crc ^ bytes[i]];
            }
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC-8 of the specified byte range
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <param name="start">The start index upon which to compute the CRC</param>
    /// <param name="length">The length of the buffer upon which to compute the CRC</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte HashToByte(byte[] bytes, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes array cannot be empty");

        if (start >= bytes.Length && length > 1)
            throw new ArgumentOutOfRangeException(nameof(start), "Start index is out of range");

        int end = start + length;
        if (end > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Specified length exceeds buffer bounds");

        return HashToByte(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Computes the CRC-8 of the specified memory
    /// </summary>
    /// <param name="data">The memory to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe byte HashToByte<T>(Span<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data span cannot be empty", nameof(data));

        ReadOnlySpan<byte> bytes;
        if (typeof(T) == typeof(byte))
        {
            bytes = MemoryMarshal.Cast<T, byte>(data);
        }
        else
        {
            // Handle non-byte spans by reinterpreting as bytes
            bytes = MemoryMarshal.AsBytes(data);
        }

        return HashToByte(bytes);
    }

    /// <summary>
    /// Process 8 bytes at a time for better performance on larger inputs
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ProcessOctet(byte crc, ReadOnlySpan<byte> octet)
    {
        // Process 8 bytes at once for better CPU cache utilization
        crc = Crc.TableCrc8[crc ^ octet[0]];
        crc = Crc.TableCrc8[crc ^ octet[1]];
        crc = Crc.TableCrc8[crc ^ octet[2]];
        crc = Crc.TableCrc8[crc ^ octet[3]];
        crc = Crc.TableCrc8[crc ^ octet[4]];
        crc = Crc.TableCrc8[crc ^ octet[5]];
        crc = Crc.TableCrc8[crc ^ octet[6]];
        crc = Crc.TableCrc8[crc ^ octet[7]];

        return crc;
    }
}
