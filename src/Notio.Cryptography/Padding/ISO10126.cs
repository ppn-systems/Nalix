using Notio.Randomization;
using System;

namespace Notio.Cryptography.Padding;

/// <summary>
/// Provides ISO 10126 padding and unpadding functionalities.
/// Note: This standard was withdrawn in 2007 but is included for compatibility.
/// </summary>
public static class ISO10126
{
    #region Pad Methods

    /// <summary>
    /// Pads the input byte array to the specified block size using ISO 10126 padding.
    /// </summary>
    /// <param name="data">The input byte array to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO 10126 padding applied.</returns>
    public static byte[] Pad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

        byte[] randomBytes = new byte[paddingSize - 1];
        if (paddingSize > 1)
        {
            // Fill with random bytes except the last byte
            RandGenerator.Fill(randomBytes);
            Buffer.BlockCopy(randomBytes, 0, paddedData, data.Length, paddingSize - 1);
        }

        // Set the last byte to the padding size
        paddedData[^1] = (byte)paddingSize;

        return paddedData;
    }

    /// <summary>
    /// Pads the input span to the specified block size using ISO 10126 padding.
    /// </summary>
    /// <param name="data">The input span to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO 10126 padding applied.</returns>
    public static byte[] Pad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        data.CopyTo(paddedData);

        byte[] randomBytes = new byte[paddingSize - 1];
        if (paddingSize > 1)
        {
            // Fill with random bytes except the last byte
            RandGenerator.Fill(randomBytes);
            randomBytes.AsSpan().CopyTo(paddedData.AsSpan(data.Length, paddingSize - 1));
        }

        // Set the last byte to the padding size
        paddedData[^1] = (byte)paddingSize;

        return paddedData;
    }

    #endregion

    #region Unpad Methods

    /// <summary>
    /// Removes ISO 10126 padding from the input byte array.
    /// </summary>
    /// <param name="data">The input byte array to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO 10126 padding removed.</returns>
    public static byte[] Unpad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ISO 10126 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Get padding size from the last byte
        int paddingSize = data[^1];

        // Validate padding size
        if (paddingSize <= 0 || paddingSize > blockSize)
            throw new InvalidOperationException("Invalid padding size.");

        // Create new array without padding
        byte[] unpaddedData = new byte[data.Length - paddingSize];
        Buffer.BlockCopy(data, 0, unpaddedData, 0, unpaddedData.Length);

        return unpaddedData;
    }

    /// <summary>
    /// Removes ISO 10126 padding from the input span.
    /// </summary>
    /// <param name="data">The input span to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO 10126 padding removed.</returns>
    public static byte[] Unpad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ISO 10126 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Get padding size from the last byte
        int paddingSize = data[^1];

        // Validate padding size
        if (paddingSize <= 0 || paddingSize > blockSize)
            throw new InvalidOperationException("Invalid padding size.");

        // Create new array without padding
        byte[] unpaddedData = new byte[data.Length - paddingSize];
        data[..^paddingSize].CopyTo(unpaddedData);

        return unpaddedData;
    }

    #endregion
}
