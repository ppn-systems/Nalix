namespace Nalix.Cryptography.Checksums;

/// <summary>
/// Provides methods for computing CRC-64 checksums using the ISO and ECMA polynomials.
/// </summary>
public static class Crc64
{
    #region Constants

    private const System.UInt64 ISO = 0xD800000000000000;
    private const System.UInt64 ECMA = 0xC96C5795D7870F42;
    private const System.UInt64 InitialValue = 0xFFFFFFFFFFFFFFFF;

    #endregion Constants

    #region Fields

    private static readonly System.UInt64[] IsoTable = Crc00.GenerateTable64(ISO);
    private static readonly System.UInt64[] EcmaTable = Crc00.GenerateTable64(ECMA);

    #endregion Fields

    #region APIs

    /// <summary>
    /// Computes the CRC-64 checksum of the given data.
    /// </summary>
    /// <param name="data">The input data to compute the checksum for.</param>
    /// <param name="useEcma">If true, uses the ECMA polynomial; otherwise, uses the ISO polynomial.</param>
    /// <returns>The computed CRC-64 checksum.</returns>
    public static System.UInt64 Compute(
        System.ReadOnlySpan<System.Byte> data,
        System.Boolean useEcma = false)
    {
        System.UInt64[] table = useEcma ? EcmaTable : IsoTable;
        System.UInt64 crc = InitialValue;

        foreach (System.Byte b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    /// <summary>
    /// Computes the CRC-64 checksum of the given byte array.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <param name="useEcma">If true, uses the ECMA polynomial; otherwise, uses the ISO polynomial.</param>
    /// <returns>The computed CRC-64 checksum.</returns>
    public static System.UInt64 Compute(
        System.Byte[] data,
        System.Boolean useEcma = false)
        => Compute((System.ReadOnlySpan<System.Byte>)data, useEcma);

    #endregion APIs
}