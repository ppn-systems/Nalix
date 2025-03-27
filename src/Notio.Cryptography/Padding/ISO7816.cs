using System;

namespace Notio.Cryptography.Padding;

/// <summary>
/// Provides ISO/IEC 7816-4 padding and unpadding functionalities.
/// </summary>
public static class ISO7816
{
    /// <summary>
    /// Pads the input byte array to the specified block size using ISO/IEC 7816-4 padding.
    /// </summary>
    /// <param name="data">The input byte array to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding applied.</returns>
    public static byte[] Pad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

        // Add 0x80 as the first padding byte
        paddedData[data.Length] = 0x80;

        // Fill remaining bytes with zeros
        for (int i = data.Length + 1; i < paddedData.Length; i++) paddedData[i] = 0x00;

        return paddedData;
    }

    /// <summary>
    /// Pads the input span to the specified block size using ISO/IEC 7816-4 padding.
    /// </summary>
    /// <param name="data">The input span to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding applied.</returns>
    public static byte[] Pad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        data.CopyTo(paddedData);

        // Add 0x80 as the first padding byte
        paddedData[data.Length] = 0x80;

        // Fill remaining bytes with zeros
        for (int i = data.Length + 1; i < paddedData.Length; i++) paddedData[i] = 0x00;


        return paddedData;
    }

    /// <summary>
    /// Removes ISO/IEC 7816-4 padding from the input byte array.
    /// </summary>
    /// <param name="data">The input byte array to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding removed.</returns>
    public static byte[] Unpad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ISO/IEC 7816-4 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Find the padding marker (0x80)
        int paddingStart = FindPaddingStart(data);
        if (paddingStart == -1)
            throw new InvalidOperationException("Invalid padding: 0x80 marker not found.");

        // Create new array without padding
        byte[] unpaddedData = new byte[paddingStart];
        Buffer.BlockCopy(data, 0, unpaddedData, 0, paddingStart);

        return unpaddedData;
    }

    /// <summary>
    /// Removes ISO/IEC 7816-4 padding from the input span.
    /// </summary>
    /// <param name="data">The input span to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding removed.</returns>
    public static byte[] Unpad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ISO/IEC 7816-4 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Find the padding marker (0x80)
        int paddingStart = FindPaddingStart(data);
        if (paddingStart == -1)
            throw new InvalidOperationException("Invalid padding: 0x80 marker not found.");

        // Create new array without padding
        byte[] unpaddedData = new byte[paddingStart];
        data[..paddingStart].CopyTo(unpaddedData);

        return unpaddedData;
    }

    /// <summary>
    /// Finds the start of the ISO/IEC 7816-4 padding by locating the 0x80 marker.
    /// </summary>
    /// <param name="data">The data to search.</param>
    /// <returns>The index of the 0x80 marker, or -1 if not found.</returns>
    private static int FindPaddingStart(ReadOnlySpan<byte> data)
    {
        // Start from the end and work backwards
        for (int i = data.Length - 1; i >= 0; i--)
        {
            if (data[i] == 0x80)
            {
                // Found the 0x80 byte, now validate all bytes after it are zeros
                if (AnsiX923.IsValidPadding(data, i))
                {
                    return i;
                }
                return -1; // Invalid padding
            }
            else if (data[i] != 0x00)
            {
                // Found a non-zero byte that isn't 0x80, invalid padding
                return -1;
            }
        }

        return -1; // No 0x80 byte found
    }
}
