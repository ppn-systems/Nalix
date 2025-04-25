using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Padding;

/// <summary>
/// Provides PKCS7 padding and unpadding functionalities with Span support.
/// </summary>
public static class PKCS7
{
    #region Pad Methods

    /// <summary>
    /// Pads the input span to the specified block size using PKCS7 padding.
    /// </summary>
    /// <param name="data">The input span to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with PKCS7 padding applied.</returns>
    public static byte[] Pad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int size = blockSize - data.Length % blockSize;
        byte[] dataP = new byte[data.Length + size];
        data.CopyTo(dataP);

        dataP.AsSpan(data.Length).Fill((byte)size);

        return dataP;
    }

    #endregion Pad Methods

    #region Unpad Methods

    /// <summary>
    /// Removes PKCS7 padding from the input span.
    /// </summary>
    /// <param name="data">The input span to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with PKCS7 padding removed.</returns>
    public static byte[] Unpad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
            throw new ArgumentException("The data length is invalid for PKCS7 padding.", nameof(data));
        if (blockSize <= 0 || blockSize > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");

        int size = data[^1];

        if (size <= 0 || size > blockSize || !IsValidPadding(data, size))
            throw new InvalidOperationException("Invalid padding.");

        return data[..^size].ToArray();
    }

    #endregion Unpad Methods

    #region Private Methods

    /// <summary>
    /// Checks if the given data has valid PKCS7 padding.
    /// </summary>
    /// <param name="data">The input span to check.</param>
    /// <param name="paddingSize">The expected padding size.</param>
    /// <returns>True if the padding is valid, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidPadding(ReadOnlySpan<byte> data, int paddingSize) =>
        paddingSize > 0 && paddingSize <= data.Length && !(data[^paddingSize..].IndexOfAnyExcept((byte)paddingSize) >= 0);

    #endregion Private Methods
}
