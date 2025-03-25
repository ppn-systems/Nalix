using System;

namespace Notio.Cryptography.Padding;

/// <summary>
/// Provides functionality for using no padding (data must already be block-aligned).
/// This is used with streaming modes (CTR, GCM, CBC-CS) or when data is pre-aligned.
/// </summary>
public static class NoPadding
{
    /// <summary>
    /// Verifies the input byte array is already properly aligned to the block size.
    /// Does not add any padding.
    /// </summary>
    /// <param name="data">The input byte array to verify.</param>
    /// <param name="blockSize">The block size to align to.</param>
    /// <returns>The original data if already aligned.</returns>
    /// <exception cref="ArgumentException">Thrown when data is not already aligned to the block size.</exception>
    public static byte[] Pad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));

        // Return a copy of the input data
        byte[] result = new byte[data.Length];
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
    public static byte[] Pad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));

        // Return a copy of the input data
        byte[] result = new byte[data.Length];
        data.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Returns the original data without any modification.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <param name="blockSize">The block size (used for validation only).</param>
    /// <returns>A copy of the original data.</returns>
    public static byte[] Unpad(byte[] data, int blockSize)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));

        // Return a copy of the input data
        byte[] result = new byte[data.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    /// <summary>
    /// Returns the original data without any modification.
    /// </summary>
    /// <param name="data">The input span.</param>
    /// <param name="blockSize">The block size (used for validation only).</param>
    /// <returns>A copy of the original data.</returns>
    public static byte[] Unpad(ReadOnlySpan<byte> data, int blockSize)
    {
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
            throw new ArgumentException($"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));

        // Return a copy of the input data
        byte[] result = new byte[data.Length];
        data.CopyTo(result);
        return result;
    }
}
