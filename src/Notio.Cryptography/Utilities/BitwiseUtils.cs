using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Utilities;

/// <summary>
/// High-performance bitwise utilities for cryptographic operations.
/// Uses hardware intrinsics when available for maximum efficiency.
/// </summary>
public static class BitwiseUtils
{
    /// <summary>
    /// Rotate a 32-bit word right by the specified number of bits using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateRight(uint value, int bits)
    {
        // Use BitOperations.RotateRight which will use hardware intrinsics when available
        return BitOperations.RotateRight(value, bits);
    }

    /// <summary>
    /// Performs a left bitwise rotation on a 32-bit unsigned integer using hardware intrinsics when available.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateLeft(uint value, int bits)
    {
        // Use BitOperations.RotateLeft which will use hardware intrinsics when available
        return BitOperations.RotateLeft(value, bits);
    }

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

    /// <summary>
    /// Convert four bytes of the input buffer into an unsigned 32-bit integer, beginning at the inputOffset.
    /// Uses optimized hardware intrinsics when available.
    /// </summary>
    /// <param name="p">The source byte array.</param>
    /// <param name="inputOffset">The offset within the array to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint U8To32Little(byte[] p, int inputOffset)
    {
        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte pRef = ref p[inputOffset];
            return Unsafe.ReadUnaligned<uint>(ref pRef);
        }
        else
        {
            // Fallback path for big-endian architectures
            return (uint)(p[inputOffset]) |
                  ((uint)(p[inputOffset + 1]) << 8) |
                  ((uint)(p[inputOffset + 2]) << 16) |
                  ((uint)(p[inputOffset + 3]) << 24);
        }
    }

    /// <summary>
    /// Convert four bytes of the input span into an unsigned 32-bit integer, beginning at the offset.
    /// Uses optimized hardware intrinsics when available.
    /// </summary>
    /// <param name="data">The source span.</param>
    /// <param name="offset">The offset within the span to read from.</param>
    /// <returns>An unsigned 32-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint U8To32Little(ReadOnlySpan<byte> data, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte dataRef = ref MemoryMarshal.GetReference(data[offset..]);
            return Unsafe.ReadUnaligned<uint>(ref dataRef);
        }
        else
        {
            // Fallback path for big-endian architectures
            return (uint)(data[offset]) |
                  ((uint)(data[offset + 1]) << 8) |
                  ((uint)(data[offset + 2]) << 16) |
                  ((uint)(data[offset + 3]) << 24);
        }
    }

    /// <summary>
    /// Serialize the input integer into the output buffer. The input integer will be split into 4 bytes 
    /// and put into four sequential places in the output buffer, starting at the outputOffset.
    /// Uses optimized hardware intrinsics when available.
    /// </summary>
    /// <param name="output">The destination buffer.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="outputOffset">The offset within the buffer to write to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToBytes(byte[] output, uint input, int outputOffset)
    {
        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte outputRef = ref output[outputOffset];
            Unsafe.WriteUnaligned(ref outputRef, input);
        }
        else
        {
            output[outputOffset] = (byte)input;
            output[outputOffset + 1] = (byte)(input >> 8);
            output[outputOffset + 2] = (byte)(input >> 16);
            output[outputOffset + 3] = (byte)(input >> 24);
        }
    }

    /// <summary>
    /// Serialize the input integer into the output span. The input integer will be split into 4 bytes 
    /// and put into four sequential places in the output span, starting at the offset.
    /// Uses optimized hardware intrinsics when available.
    /// </summary>
    /// <param name="output">The destination span.</param>
    /// <param name="input">The input value to be serialized.</param>
    /// <param name="offset">The offset within the span to write to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToBytes(Span<byte> output, uint input, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte outputRef = ref MemoryMarshal.GetReference(output[offset..]);
            Unsafe.WriteUnaligned(ref outputRef, input);
        }
        else
        {
            output[offset] = (byte)input;
            output[offset + 1] = (byte)(input >> 8);
            output[offset + 2] = (byte)(input >> 16);
            output[offset + 3] = (byte)(input >> 24);
        }
    }

    /// <summary>
    /// Performs a single round of the SHA-256 hash function with SIMD optimizations where available.
    /// This method updates the internal state variables of the SHA-256 algorithm based on the provided message word (w)
    /// and constant (k), performing the necessary bitwise operations and transformations.
    /// </summary>
    /// <param name="a">The first internal state variable (a) of the SHA-256 algorithm.</param>
    /// <param name="b">The second internal state variable (b) of the SHA-256 algorithm.</param>
    /// <param name="c">The third internal state variable (c) of the SHA-256 algorithm.</param>
    /// <param name="d">The fourth internal state variable (d) of the SHA-256 algorithm.</param>
    /// <param name="e">The fifth internal state variable (e) of the SHA-256 algorithm.</param>
    /// <param name="f">The sixth internal state variable (f) of the SHA-256 algorithm.</param>
    /// <param name="g">The seventh internal state variable (G) of the SHA-256 algorithm.</param>
    /// <param name="h">The eighth internal state variable (H) of the SHA-256 algorithm.</param>
    /// <param name="w">The message word (w) used in the current round of the SHA-256 algorithm.</param>
    /// <param name="k">The constant value (k) used in the current round of the SHA-256 algorithm.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Round(
        ref uint a, ref uint b, ref uint c, ref uint d,
        ref uint e, ref uint f, ref uint g, ref uint h,
        uint w, uint k)
    {
        // Traditional implementation with hardware acceleration through BitOperations
        uint s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
        uint ch = (e & f) ^ (~e & g);
        uint temp1 = h + s1 + ch + k + w;

        uint s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
        uint maj = (a & b) ^ (a & c) ^ (b & c);
        uint temp2 = s0 + maj;

        h = g;
        g = f;
        f = e;
        e = d + temp1;
        d = c;
        c = b;
        b = a;
        a = temp1 + temp2;
    }
}
