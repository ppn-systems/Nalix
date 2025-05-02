using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Padding;

/// <summary>
/// Provides ANSI X.923 padding and unpadding functionalities.
/// </summary>
public static class AnsiX923
{
    #region Pad Methods

    /// <summary>
    /// Pads the input byte array to the specified block size using ANSI X.923 padding.
    /// </summary>
    /// <param name="data">The input byte array to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ANSI X.923 padding applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Pad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

        // Fill with zeros except the last byte
        for (int i = data.Length; i < paddedData.Length - 1; i++)
        {
            paddedData[i] = 0x00;
        }

        // Set the last byte to the padding size
        paddedData[^1] = (byte)paddingSize;

        return paddedData;
    }

    /// <summary>
    /// Pads the input span to the specified block size using ANSI X.923 padding.
    /// </summary>
    /// <param name="data">The input span to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ANSI X.923 padding applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Pad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int paddingSize = blockSize - (data.Length % blockSize);
        byte[] paddedData = new byte[data.Length + paddingSize];

        // Copy original data
        data.CopyTo(paddedData);

        // Fill with zeros except the last byte
        for (int i = data.Length; i < paddedData.Length - 1; i++)
        {
            paddedData[i] = 0x00;
        }

        // Set the last byte to the padding size
        paddedData[^1] = (byte)paddingSize;

        return paddedData;
    }

    #endregion Pad Methods

    #region Unpad Methods

    /// <summary>
    /// Removes ANSI X.923 padding from the input byte array.
    /// </summary>
    /// <param name="data">The input byte array to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ANSI X.923 padding removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Unpad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ANSI X.923 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Get padding size from the last byte
        int paddingSize = data[^1];
        if (paddingSize <= 0 || paddingSize > blockSize)
            throw new InvalidOperationException("Invalid padding size.");

        // Validate padding - all bytes before the last padding byte must be 0x00
        if (!IsValidPadding(data.AsSpan(), paddingSize))
            throw new InvalidOperationException("Invalid padding.");

        // Create new array without padding
        byte[] unpaddedData = new byte[data.Length - paddingSize];
        Buffer.BlockCopy(data, 0, unpaddedData, 0, unpaddedData.Length);

        return unpaddedData;
    }

    /// <summary>
    /// Removes ANSI X.923 padding from the input span.
    /// </summary>
    /// <param name="data">The input span to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ANSI X.923 padding removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Unpad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for ANSI X.923 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        // Get padding size from the last byte
        int paddingSize = data[^1];
        if (paddingSize <= 0 || paddingSize > blockSize)
            throw new InvalidOperationException("Invalid padding size.");

        // Validate padding - all bytes before the last padding byte must be 0x00
        if (!IsValidPadding(data, paddingSize))
            throw new InvalidOperationException("Invalid padding.");

        // Create new array without padding
        byte[] unpaddedData = new byte[data.Length - paddingSize];
        data[..^paddingSize].CopyTo(unpaddedData);

        return unpaddedData;
    }

    #endregion Unpad Methods

    #region Internal Methods

    /// <summary>
    /// Validates the ANSI X.923 padding in a constant-time manner.
    /// </summary>
    /// <param name="data">The padded data to validate.</param>
    /// <param name="paddingSize">The padding size obtained from the last byte.</param>
    /// <returns>True if the padding is valid, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValidPadding(ReadOnlySpan<byte> data, int paddingSize)
    {
        bool isValid = true;

        // Check that all padding bytes (except the last one) are zeros
        // Constant-time comparison to avoid timing attacks
        for (int i = data.Length - paddingSize; i < data.Length - 1; i++)
            isValid &= (data[i] == 0x00);

        return isValid;
    }

    #endregion Internal Methods
}
