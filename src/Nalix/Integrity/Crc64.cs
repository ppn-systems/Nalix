using System;

namespace Nalix.Integrity;

/// <summary>
/// Provides methods for computing CRC-64 checksums using the ISO and ECMA polynomials.
/// </summary>
public static class Crc64
{
    private const ulong ISO = 0xD800000000000000;
    private const ulong ECMA = 0xC96C5795D7870F42;
    private const ulong InitialValue = 0xFFFFFFFFFFFFFFFF;

    private static readonly ulong[] IsoTable = Crc00.GenerateTable64(ISO);
    private static readonly ulong[] EcmaTable = Crc00.GenerateTable64(ECMA);

    /// <summary>
    /// Computes the CRC-64 checksum of the given data.
    /// </summary>
    /// <param name="data">The input data to compute the checksum for.</param>
    /// <param name="useEcma">If true, uses the ECMA polynomial; otherwise, uses the ISO polynomial.</param>
    /// <returns>The computed CRC-64 checksum.</returns>
    public static ulong Compute(ReadOnlySpan<byte> data, bool useEcma = false)
    {
        ulong[] table = useEcma ? EcmaTable : IsoTable;
        ulong crc = InitialValue;

        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ crc >> 8;

        return ~crc;
    }

    /// <summary>
    /// Computes the CRC-64 checksum of the given byte array.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <param name="useEcma">If true, uses the ECMA polynomial; otherwise, uses the ISO polynomial.</param>
    /// <returns>The computed CRC-64 checksum.</returns>
    public static ulong Compute(byte[] data, bool useEcma = false)
        => Compute((ReadOnlySpan<byte>)data, useEcma);
}
