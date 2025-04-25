using System;
using System.Diagnostics;
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

    #region Byte Conversion (Little Endian)

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
        Debug.Assert(p.Length >= inputOffset + 4);

        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte pRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(p), inputOffset);
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
        Debug.Assert(data.Length >= offset + 4);

        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte dataRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(data), offset);
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
        Debug.Assert(output.Length >= outputOffset + 4);

        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte outputRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(output), outputOffset);
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
        Debug.Assert(output.Length >= offset + 4);

        if (BitConverter.IsLittleEndian)
        {
            // Use unsafe direct memory access for maximum performance
            ref byte outputRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(output), offset);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> data, int offset) =>
        ((uint)data[offset]) |
        ((uint)data[offset + 1] << 8) |
        ((uint)data[offset + 2] << 16) |
        ((uint)data[offset + 3] << 24);

    #endregion Byte Conversion (Little Endian)
}
