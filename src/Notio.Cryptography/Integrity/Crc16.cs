using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Integrity;

/// <summary>
/// High-performance implementation of CRC16 checksum calculation.
/// </summary>
public static class Crc16
{
    /// <summary>
    /// Calculates the CRC16 for the entire byte array provided.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort HashToUnit16(params byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return HashToUnit16(bytes.AsSpan());
    }

    /// <summary>
    /// Calculates the CRC16 for a chunk of data in a byte array.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <param name="start">The index to start processing.</param>
    /// <param name="length">The number of bytes to process.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort HashToUnit16(byte[] bytes, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty.");

        if (start < 0 || start >= bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(start));

        if (length < 0 || start + length > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return HashToUnit16(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Computes the CRC16 for a span of bytes with optimized processing.
    /// </summary>
    /// <param name="bytes">Span of input bytes.</param>
    /// <returns>CRC16 value as ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort HashToUnit16(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Byte span cannot be empty", nameof(bytes));

        ushort crc = 0xFFFF; // Initial value

        // Process 8 bytes at once for larger inputs
        if (bytes.Length >= 8)
        {
            int blockCount = bytes.Length / 8;
            int remaining = bytes.Length % 8;

            // Process 8-byte blocks
            for (int i = 0; i < blockCount * 8; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (int i = bytes.Length - remaining; i < bytes.Length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ bytes[i]) & 0xFF]);
            }
        }
        else
        {
            // For small inputs, use the simple loop
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ bytes[i]) & 0xFF]);
            }
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC16 for any unmanaged generic data type.
    /// </summary>
    /// <typeparam name="T">Any unmanaged data type</typeparam>
    /// <param name="data">The data to compute the CRC16 for</param>
    /// <returns>The CRC16 value as a ushort</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort HashToUnit16<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data span cannot be empty", nameof(data));

        // Reinterpret the generic type as a byte span
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        return HashToUnit16(bytes);
    }

    /// <summary>
    /// Verifies if the provided data matches the expected CRC16 value.
    /// </summary>
    /// <param name="data">The data to verify</param>
    /// <param name="expectedCrc">The expected CRC16 value</param>
    /// <returns>True if the CRC matches, otherwise false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(ReadOnlySpan<byte> data, ushort expectedCrc)
        => HashToUnit16(data) == expectedCrc;

    /// <summary>
    /// Processes 8 bytes at once for improved performance on larger inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ProcessOctet(ushort crc, ReadOnlySpan<byte> octet)
    {
        // Process 8 bytes in sequence - helps with instruction pipelining
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[0]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[1]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[2]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[3]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[4]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[5]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[6]) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc.TableCrc16[(crc ^ octet[7]) & 0xFF]);

        return crc;
    }
}
