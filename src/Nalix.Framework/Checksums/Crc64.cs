// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Checksums;

/// <summary>
/// Provides methods for computing CRC-64 checksums using the ISO (0xD800000000000000) or ECMA (0xC96C5795D7870F42) polynomials.
/// </summary>
public static class Crc64
{
    #region Constants

    /// <summary>
    /// The ISO polynomial for CRC-64 calculations (0xD800000000000000).
    /// </summary>
    private const System.UInt64 ISO = 0xD800000000000000;

    /// <summary>
    /// The ECMA polynomial for CRC-64 calculations (0xC96C5795D7870F42).
    /// </summary>
    private const System.UInt64 ECMA = 0xC96C5795D7870F42;

    /// <summary>
    /// The initial value for CRC-64 calculations (0xFFFFFFFFFFFFFFFF).
    /// </summary>
    private const System.UInt64 InitialValue = 0xFFFFFFFFFFFFFFFF;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-64 calculations using the ISO polynomial.
    /// </summary>
    private static readonly System.UInt64[] IsoTable = Crc00.GenerateTable64(ISO);

    /// <summary>
    /// Precomputed lookup table for CRC-64 calculations using the ECMA polynomial.
    /// </summary>
    private static readonly System.UInt64[] EcmaTable = Crc00.GenerateTable64(ECMA);

    #endregion Fields

    #region APIs

    /// <summary>
    /// Computes the CRC-64 checksum for a read-only span of bytes.
    /// </summary>
    /// <param name="data">The read-only span of bytes to compute the CRC-64 checksum for.</param>
    /// <param name="useEcma">If <c>true</c>, uses the ECMA polynomial (0xC96C5795D7870F42); otherwise, uses the ISO polynomial (0xD800000000000000). Defaults to <c>false</c>.</param>
    /// <returns>The computed CRC-64 checksum as a 64-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="data"/> is null in the byte array overload.</exception>
    /// <remarks>
    /// The checksum is computed using a precomputed lookup table for efficiency. The final CRC value is inverted (bitwise NOT) before being returned.
    /// </remarks>
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
    /// Computes the CRC-64 checksum for a byte array.
    /// </summary>
    /// <param name="data">The byte array to compute the CRC-64 checksum for.</param>
    /// <param name="useEcma">If <c>true</c>, uses the ECMA polynomial (0xC96C5795D7870F42); otherwise, uses the ISO polynomial (0xD800000000000000). Defaults to <c>false</c>.</param>
    /// <returns>The computed CRC-64 checksum as a 64-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <remarks>
    /// This method converts the byte array to a read-only span and delegates to the <see cref="Compute(System.ReadOnlySpan{System.Byte}, System.Boolean)"/> method.
    /// </remarks>
    public static System.UInt64 Compute(
        System.Byte[] data,
        System.Boolean useEcma = false)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        return Compute((System.ReadOnlySpan<System.Byte>)data, useEcma);
    }

    #endregion APIs
}