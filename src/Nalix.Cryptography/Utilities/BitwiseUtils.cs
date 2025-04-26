using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Cryptography.Utilities;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// Uses hardware intrinsics when available for maximum efficiency.
/// </summary>
[SkipLocalsInit]
public static partial class BitwiseUtils
{
    #region Bit Rotations

    /// <summary>
    /// Rotate a 32-bit word right by the specified Number of bits using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The Number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateRight(uint value, int bits)
        => BitOperations.RotateRight(value, bits);

    /// <summary>
    /// Performs a left bitwise rotation on a 32-bit unsigned integer using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The Number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateLeft(uint value, int bits)
        => BitOperations.RotateLeft(value, bits);

    #endregion Bit Rotations

    #region Arithmetic Operations

    /// <summary>
    /// Unchecked integer exclusive or (XOR) operation.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v XOR w).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint XOr(uint v, uint w) => v ^ w;

    /// <summary>
    /// Unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v + w) modulo 2^32.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(uint v, uint w) => unchecked(v + w);

    /// <summary>
    /// Add 1 to the input parameter using unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">The value to increment.</param>
    /// <returns>The result of (v + 1) modulo 2^32.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AddOne(uint v) => unchecked(v + 1);

    /// <summary>
    /// Unchecked integer subtraction. Performs modular subtraction (v - w) mod 2^32.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v - w) modulo 2^32.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Subtract(uint v, uint w) => unchecked(v - w);

    #endregion Arithmetic Operations

    #region Byte Conversion

    /// <summary>
    /// Convert four bytes of the input buffer into an unsigned 32-bit integer, beginning at the inputOffset.
    /// </summary>
    /// <param name="p">The source byte array.</param>
    /// <param name="inputOffset">The offset within the array to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint U8To32Little(byte[] p, int inputOffset)
    {
        if (p == null || p.Length < inputOffset + 4)
            throw new ArgumentOutOfRangeException(nameof(p), "Input array is too small.");

        fixed (byte* ptr = &p[inputOffset])
            return Unsafe.ReadUnaligned<uint>(ptr);
    }

    /// <summary>
    /// Convert four bytes of the input span into an unsigned 32-bit integer, beginning at the offset.
    /// </summary>
    /// <param name="data">The source span.</param>
    /// <param name="offset">The offset within the span to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint U8To32Little(ReadOnlySpan<byte> data, int offset)
    {
        if (data.Length < offset + 4)
            throw new ArgumentOutOfRangeException(nameof(data), "Input span is too small.");

        fixed (byte* ptr = &MemoryMarshal.GetReference(data))
            return Unsafe.ReadUnaligned<uint>(ptr + offset);
    }

    /// <summary>
    /// Serialize the input integer into the output buffer. The input integer will be split into 4 bytes
    /// and put into four sequential places in the output buffer, starting at the outputOffset.
    /// </summary>
    /// <param name="output">The destination buffer.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="outputOffset">The offset within the buffer to write to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ToBytes(byte[] output, uint input, int outputOffset)
    {
        if (output == null || output.Length < outputOffset + 4)
            throw new ArgumentOutOfRangeException(nameof(output), "Output array is too small.");

        fixed (byte* ptr = &output[outputOffset])
            Unsafe.WriteUnaligned(ptr, input);
    }

    /// <summary>
    /// Serialize the input integer into the output span. The input integer will be split into 4 bytes
    /// and put into four sequential places in the output span, starting at the offset.
    /// </summary>
    /// <param name="output">The destination span.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="offset">The offset within the span to write to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ToBytes(Span<byte> output, uint input, int offset)
    {
        if (output.Length < offset + 4)
            throw new ArgumentOutOfRangeException(nameof(output), "Output span is too small.");

        fixed (byte* ptr = &MemoryMarshal.GetReference(output))
            Unsafe.WriteUnaligned(ptr + offset, input);
    }

    #endregion Byte Conversion
}
