namespace Nalix.Cryptography.Internal;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// Uses hardware intrinsics when available for maximum efficiency.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
internal static partial class BitwiseUtils
{
    #region Bit Rotations

    /// <summary>
    /// Rotate a 32-bit word right by the specified TransportProtocol of bits using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The TransportProtocol of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 RotateRight(System.UInt32 value, System.Int32 bits)
        => (value >> bits) | (value << (32 - bits));

    /// <summary>
    /// Performs a left bitwise rotation on a 32-bit unsigned integer using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The TransportProtocol of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 RotateLeft(System.UInt32 value, System.Int32 bits)
        => (value << bits) | (value >> (32 - bits));

    #endregion Bit Rotations

    #region Arithmetic Operations

    /// <summary>
    /// Unchecked integer exclusive or (XOR) operation.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v XOR w).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 XOr(System.UInt32 v, System.UInt32 w) => v ^ w;

    /// <summary>
    /// Unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v + w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Add(System.UInt32 v, System.UInt32 w) => unchecked(v + w);

    /// <summary>
    /// Push 1 to the input parameter using unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v">The value to increment.</param>
    /// <returns>The result of (v + 1) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 AddOne(System.UInt32 v) => unchecked(v + 1);

    /// <summary>
    /// Unchecked integer subtraction. Performs modular subtraction (v - w) mod 2^32.
    /// </summary>
    /// <param name="v">First operand.</param>
    /// <param name="w">Second operand.</param>
    /// <returns>The result of (v - w) modulo 2^32.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Subtract(System.UInt32 v, System.UInt32 w) => unchecked(v - w);

    #endregion Arithmetic Operations

    #region Byte Conversion

    /// <summary>
    /// Convert four bytes of the input buffer into an unsigned 32-bit integer, beginning at the inputOffset.
    /// </summary>
    /// <param name="p">The source byte array.</param>
    /// <param name="inputOffset">The offset within the array to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.UInt32 U8To32Little(System.Byte[] p, System.Int32 inputOffset)
    {
        if (p == null || p.Length < inputOffset + 4)
        {
            throw new System.ArgumentOutOfRangeException(nameof(p), "Input array is too small.");
        }

        fixed (System.Byte* ptr = &p[inputOffset])
        {
            return System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ptr);
        }
    }

    /// <summary>
    /// Convert four bytes of the input span into an unsigned 32-bit integer, beginning at the offset.
    /// </summary>
    /// <param name="data">The source span.</param>
    /// <param name="offset">The offset within the span to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.UInt32 U8To32Little(System.ReadOnlySpan<System.Byte> data, System.Int32 offset)
    {
        if (data.Length < offset + 4)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Input span is too small.");
        }

        fixed (System.Byte* ptr = &System.Runtime.InteropServices.MemoryMarshal.GetReference(data))
        {
            return System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ptr + offset);
        }
    }

    /// <summary>
    /// Deserialize the input integer into the output buffer. The input integer will be split into 4 bytes
    /// and put into four sequential places in the output buffer, starting at the outputOffset.
    /// </summary>
    /// <param name="output">The destination buffer.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="outputOffset">The offset within the buffer to write to.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void ToBytes(System.Byte[] output, System.UInt32 input, System.Int32 outputOffset)
    {
        if (output == null || output.Length < outputOffset + 4)
        {
            throw new System.ArgumentOutOfRangeException(nameof(output), "Output array is too small.");
        }

        fixed (System.Byte* ptr = &output[outputOffset])
        {
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ptr, input);
        }
    }

    /// <summary>
    /// Deserialize the input integer into the output span. The input integer will be split into 4 bytes
    /// and put into four sequential places in the output span, starting at the offset.
    /// </summary>
    /// <param name="output">The destination span.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="offset">The offset within the span to write to.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void ToBytes(
        System.Span<System.Byte> output,
        System.UInt32 input, System.Int32 offset)
    {
        if (output.Length < offset + 4)
        {
            throw new System.ArgumentOutOfRangeException(nameof(output), "Output span is too small.");
        }

        fixed (System.Byte* ptr = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
        {
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ptr + offset, input);
        }
    }

    #endregion Byte Conversion
}