// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Pad(System.ReadOnlySpan<System.Byte> data, System.Int32 blockSize)
    {
        if (blockSize is <= 0 or > System.Byte.MaxValue)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        System.Int32 size = blockSize - (data.Length % blockSize);
        System.Byte[] dataP = new System.Byte[data.Length + size];
        data.CopyTo(dataP);

        System.MemoryExtensions.AsSpan(dataP, data.Length).Fill((System.Byte)size);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Unpad(
        System.ReadOnlySpan<System.Byte> data, System.Int32 blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
        {
            throw new System.ArgumentException("The data length is invalid for PKCS7 padding.", nameof(data));
        }

        if (blockSize is <= 0 or > System.Byte.MaxValue)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        System.Int32 size = data[^1];

        return size <= 0 || size > blockSize || !HasValidPadding(data, size)
            ? throw new System.InvalidOperationException("Invalid padding.")
            : data[..^size].ToArray();
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
    private static System.Boolean HasValidPadding(System.ReadOnlySpan<System.Byte> data, System.Int32 paddingSize) =>
        paddingSize > 0 && paddingSize <= data.Length &&
        System.MemoryExtensions.IndexOfAnyExcept(data[^paddingSize..], (System.Byte)paddingSize) == -1;

    #endregion Private Methods
}
