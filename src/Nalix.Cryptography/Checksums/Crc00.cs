namespace Nalix.Cryptography.Checksums;

/// <summary>
/// Provides methods for computing Cyclic Redundancy Check (CRC) checksums.
/// </summary>
internal static class Crc00
{
    /// <summary>
    /// Generates a lookup table for CRC-8/MODBUS.
    /// </summary>
    internal static System.Byte[] GenerateTable8(System.Byte poly)
    {
        System.Byte[] table = new System.Byte[256];

        for (System.Int16 i = 0; i < 256; i++)
        {
            System.Byte crc = (System.Byte)i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (System.Byte)((crc & 0x80) != 0 ? (crc << 1) ^ poly : crc << 1);
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a lookup table for CRC-16/MODBUS.
    /// </summary>
    internal static System.UInt16[] GenerateTable16(System.UInt16 poly)
    {
        System.UInt16[] table = new System.UInt16[256];

        for (System.Int16 i = 0; i < 256; i++)
        {
            System.UInt16 crc = (System.UInt16)i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (System.UInt16)((crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1);
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates the CRC-32 lookup table based on the polynomial.
    /// </summary>
    /// <returns>A lookup table for CRC-32 computation.</returns>
    internal static System.UInt32[] GenerateTable32(System.UInt32 poly)
    {
        System.UInt32[] table = new System.UInt32[256];

        for (System.UInt32 i = 0; i < 256; i++)
        {
            System.UInt32 crc = i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a CRC-64 lookup table based on the given polynomial.
    /// </summary>
    /// <param name="poly">The polynomial to use for table generation.</param>
    /// <returns>A lookup table for CRC-64 computation.</returns>
    internal static System.UInt64[] GenerateTable64(System.UInt64 poly)
    {
        System.UInt64[] table = new System.UInt64[256];
        for (System.Int32 i = 0; i < 256; i++)
        {
            System.UInt64 crc = (System.UInt64)i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            }

            table[i] = crc;
        }
        return table;
    }
}