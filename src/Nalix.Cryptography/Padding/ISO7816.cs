using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Padding;

/// <summary>
/// Provides ISO/IEC 7816-4 padding and unpadding functionalities.
/// </summary>
public static class ISO7816
{
    #region Pad Methods

    /// <summary>
    /// Pads the input byte array to the specified block size using ISO/IEC 7816-4 padding.
    /// </summary>
    /// <param name="data">The input byte array to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Pad(Byte[] data, Int32 blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize is <= 0 or > Byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        Int32 paddingSize = blockSize - (data.Length % blockSize);
        Byte[] paddedData = new Byte[data.Length + paddingSize];

        // Copy original data
        Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

        // Add 0x80 as the first padding byte
        paddedData[data.Length] = 0x80;

        // Fill remaining bytes with zeros
        for (Int32 i = data.Length + 1; i < paddedData.Length; i++)
        {
            paddedData[i] = 0x00;
        }

        return paddedData;
    }

    /// <summary>
    /// Pads the input span to the specified block size using ISO/IEC 7816-4 padding.
    /// </summary>
    /// <param name="data">The input span to pad.</param>
    /// <param name="blockSize">The block size to pad to.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Pad(ReadOnlySpan<Byte> data, Int32 blockSize)
    {
        if (blockSize is <= 0 or > Byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        Int32 paddingSize = blockSize - (data.Length % blockSize);
        Byte[] paddedData = new Byte[data.Length + paddingSize];

        // Copy original data
        data.CopyTo(paddedData);

        // Add 0x80 as the first padding byte
        paddedData[data.Length] = 0x80;

        // Fill remaining bytes with zeros
        for (Int32 i = data.Length + 1; i < paddedData.Length; i++)
        {
            paddedData[i] = 0x00;
        }

        return paddedData;
    }

    #endregion Pad Methods

    #region Unpad Methods

    /// <summary>
    /// Removes ISO/IEC 7816-4 padding from the input byte array.
    /// </summary>
    /// <param name="data">The input byte array to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Unpad(Byte[] data, Int32 blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length % blockSize != 0)
        {
            throw new ArgumentException("The data length is invalid for ISO/IEC 7816-4 padding.", nameof(data));
        }

        if (blockSize is <= 0 or > Byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        // Find the padding marker (0x80)
        Int32 paddingStart = FindPaddingStart(data);
        if (paddingStart == -1)
        {
            throw new InvalidOperationException("Invalid padding: 0x80 marker not found.");
        }

        // Create new array without padding
        Byte[] unpaddedData = new Byte[paddingStart];
        Buffer.BlockCopy(data, 0, unpaddedData, 0, paddingStart);

        return unpaddedData;
    }

    /// <summary>
    /// Removes ISO/IEC 7816-4 padding from the input span.
    /// </summary>
    /// <param name="data">The input span to unpad.</param>
    /// <param name="blockSize">The block size to unpad from.</param>
    /// <returns>A new byte array with ISO/IEC 7816-4 padding removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Unpad(ReadOnlySpan<Byte> data, Int32 blockSize)
    {
        if (data.Length == 0 || data.Length % blockSize != 0)
        {
            throw new ArgumentException("The data length is invalid for ISO/IEC 7816-4 padding.", nameof(data));
        }

        if (blockSize is <= 0 or > Byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be between 1 and 255.");
        }

        // Find the padding marker (0x80)
        Int32 paddingStart = FindPaddingStart(data);
        if (paddingStart == -1)
        {
            throw new InvalidOperationException("Invalid padding: 0x80 marker not found.");
        }

        // Create new array without padding
        Byte[] unpaddedData = new Byte[paddingStart];
        data[..paddingStart].CopyTo(unpaddedData);

        return unpaddedData;
    }

    #endregion Unpad Methods

    #region Private Methods

    /// <summary>
    /// Finds the start of the ISO/IEC 7816-4 padding by locating the 0x80 marker.
    /// </summary>
    /// <param name="data">The data to search.</param>
    /// <returns>The index of the 0x80 marker, or -1 if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int32 FindPaddingStart(ReadOnlySpan<Byte> data)
    {
        for (Int32 i = data.Length - 1; i >= 0; i--)
        {
            if (data[i] == 0x80)
            {
                if (AnsiX923.HasValidPadding(data, i))
                {
                    return i;
                }
            }
            else if (data[i] != 0x00)
            {
                break;
            }
        }
        return -1;
    }

    #endregion Private Methods
}
