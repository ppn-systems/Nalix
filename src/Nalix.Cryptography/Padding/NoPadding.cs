using System;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Padding;

/// <summary>
/// Provides functionality for using no padding (data must already be block-aligned).
/// This is used with streaming modes (CTR, GCM, CBC-CS) or when data is pre-aligned.
/// </summary>
public static class NoPadding
{
    #region Pad Methods

    /// <summary>
    /// Verifies the input byte array is already properly aligned to the block size.
    /// Does not add any padding.
    /// </summary>
    /// <param name="data">The input byte array to verify.</param>
    /// <param name="blockSize">The block size to align to.</param>
    /// <returns>The original data if already aligned.</returns>
    /// <exception cref="ArgumentException">Thrown when data is not already aligned to the block size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Pad(Byte[] data, Int32 blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        Byte[] result = new Byte[data.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    /// <summary>
    /// Verifies the input span is already properly aligned to the block size.
    /// Does not add any padding.
    /// </summary>
    /// <param name="data">The input span to verify.</param>
    /// <param name="blockSize">The block size to align to.</param>
    /// <returns>The original data if already aligned.</returns>
    /// <exception cref="ArgumentException">Thrown when data is not already aligned to the block size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Pad(ReadOnlySpan<Byte> data, Int32 blockSize)
    {
        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        Byte[] result = new Byte[data.Length];
        data.CopyTo(result);
        return result;
    }

    #endregion Pad Methods

    #region Unpad Methods

    /// <summary>
    /// Returns the original data without any modification.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <param name="blockSize">The block size (used for validation only).</param>
    /// <returns>A copy of the original data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Unpad(Byte[] data, Int32 blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        Byte[] result = new Byte[data.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    /// <summary>
    /// Returns the original data without any modification.
    /// </summary>
    /// <param name="data">The input span.</param>
    /// <param name="blockSize">The block size (used for validation only).</param>
    /// <returns>A copy of the original data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] Unpad(ReadOnlySpan<Byte> data, Int32 blockSize)
    {
        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        Byte[] result = new Byte[data.Length];
        data.CopyTo(result);
        return result;
    }

    #endregion Unpad Methods
}
