namespace Notio.Integrity;

/// <summary>
/// Provides methods for computing Cyclic Redundancy Check (CRC) checksums.
/// </summary>
internal static class Crc00
{
    /// <summary>
    /// Generates a lookup table for CRC-8/MODBUS.
    /// </summary>
    internal static byte[] GenerateTable8(byte poly)
    {
        byte[] table = new byte[256];

        for (int i = 0; i < 256; i++)
        {
            byte crc = (byte)i;
            for (int j = 0; j < 8; j++)
                crc = (byte)((crc & 0x80) != 0 ? crc << 1 ^ poly : crc << 1);

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a lookup table for CRC-16/MODBUS.
    /// </summary>
    internal static ushort[] GenerateTable16(ushort poly)
    {
        ushort[] table = new ushort[256];

        for (int i = 0; i < 256; i++)
        {
            ushort crc = (ushort)i;
            for (int j = 0; j < 8; j++)
                crc = (ushort)((crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1);

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates the CRC-32 lookup table based on the polynomial.
    /// </summary>
    /// <returns>A lookup table for CRC-32 computation.</returns>
    internal static uint[] GenerateTable32(uint poly)
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1;

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a CRC-64 lookup table based on the given polynomial.
    /// </summary>
    /// <param name="poly">The polynomial to use for table generation.</param>
    /// <returns>A lookup table for CRC-64 computation.</returns>
    internal static ulong[] GenerateTable64(ulong poly)
    {
        var table = new ulong[256];
        for (int i = 0; i < 256; i++)
        {
            ulong crc = (ulong)i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1;

            table[i] = crc;
        }
        return table;
    }
}
