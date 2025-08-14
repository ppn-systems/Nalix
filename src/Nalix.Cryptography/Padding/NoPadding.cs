// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
    /// <exception cref="System.ArgumentException">Thrown when data is not already aligned to the block size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Pad(System.Byte[] data, System.Int32 blockSize)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        System.Byte[] result = new System.Byte[data.Length];
        System.Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    /// <summary>
    /// Verifies the input span is already properly aligned to the block size.
    /// Does not add any padding.
    /// </summary>
    /// <param name="data">The input span to verify.</param>
    /// <param name="blockSize">The block size to align to.</param>
    /// <returns>The original data if already aligned.</returns>
    /// <exception cref="System.ArgumentException">Thrown when data is not already aligned to the block size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Pad(
        System.ReadOnlySpan<System.Byte> data, System.Int32 blockSize)
    {
        if (blockSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        System.Byte[] result = new System.Byte[data.Length];
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Unpad(System.Byte[] data, System.Int32 blockSize)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        if (blockSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        System.Byte[] result = new System.Byte[data.Length];
        System.Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    /// <summary>
    /// Returns the original data without any modification.
    /// </summary>
    /// <param name="data">The input span.</param>
    /// <param name="blockSize">The block size (used for validation only).</param>
    /// <returns>A copy of the original data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] Unpad(
        System.ReadOnlySpan<System.Byte> data, System.Int32 blockSize)
    {
        if (blockSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        // Verify the data is already aligned to the block size
        if (data.Length % blockSize != 0)
        {
            throw new System.ArgumentException(
                $"Data length ({data.Length}) is not a multiple of the block size ({blockSize}).", nameof(data));
        }

        // Return a copy of the input data
        System.Byte[] result = new System.Byte[data.Length];
        data.CopyTo(result);
        return result;
    }

    #endregion Unpad Methods
}
