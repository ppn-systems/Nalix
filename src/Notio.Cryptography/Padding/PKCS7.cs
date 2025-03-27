using System;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Padding;

/// <summary>
/// Provides PKCS7 padding and unpadding functionalities with Span support.
/// </summary>
public static class PKCS7
{
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

        int paddingSize = blockSize - data.Length % blockSize;
        byte[] paddedData = new byte[data.Length + paddingSize];
        data.CopyTo(paddedData);

        paddedData.AsSpan(data.Length).Fill((byte)paddingSize);

        return paddedData;
    }

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

        int paddingSize = data[^1];

        if (paddingSize <= 0 || paddingSize > blockSize || !IsValidPadding(data, paddingSize))
            throw new InvalidOperationException("Invalid padding.");

        return data[..^paddingSize].ToArray();
    }

    /// <summary>
    /// Checks if the given data has valid PKCS7 padding.
    /// </summary>
    /// <param name="data">The input span to check.</param>
    /// <param name="paddingSize">The expected padding size.</param>
    /// <returns>True if the padding is valid, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidPadding(ReadOnlySpan<byte> data, int paddingSize)
    {
        byte expected = (byte)paddingSize;
        bool isValid = true;

        for (int i = data.Length - paddingSize; i < data.Length; i++)
            isValid &= data[i] == expected;

        return isValid;
    }
}
