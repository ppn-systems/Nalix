using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Notio.Cryptography.Utilities;

/// <summary>
/// Utilities that are used during compression
/// </summary>
internal static class BitwiseUtils
{
    /// <summary>
    /// Rotate a 32-bit word right by the specified number of bits.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateRight(uint value, int bits) =>
        BitOperations.RotateRight(value, bits);

    /// <summary>
    /// Performs a left bitwise rotation on a 32-bit unsigned integer.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="bits">The number of positions to rotate.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RotateLeft(uint value, int bits) =>
        BitOperations.RotateLeft(value, bits);

    /// <summary>
    /// Unchecked integer exclusive or (XOR) operation.
    /// </summary>
    /// <param name="v"></param>
    /// <param name="w"></param>
    /// <returns>The result of (value XOR w)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint XOr(uint v, uint w) => v ^ w;

    /// <summary>
    /// Unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v"></param>
    /// <param name="w"></param>
    /// <returns>The result of (value + w) modulo 2^32</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(uint v, uint w) => unchecked(v + w);

    /// <summary>
    /// Add 1 to the input parameter using unchecked integer addition. The ChaCha spec defines certain operations to use 32-bit unsigned integer addition modulo 2^32.
    /// </summary>
    /// <remarks>
    /// See <a href="https://tools.ietf.org/html/rfc7539#page-4">ChaCha20 Spec Section 2.1</a>.
    /// </remarks>
    /// <param name="v"></param>
    /// <returns>The result of (value + 1) modulo 2^32</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AddOne(uint v) => unchecked(v + 1);

    /// <summary>
    /// Convert four bytes of the input buffer into an unsigned 32-bit integer, beginning at the inputOffset.
    /// </summary>
    /// <param name="p"></param>
    /// <param name="inputOffset"></param>
    /// <returns>An unsigned 32-bit integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint U8To32Little(byte[] p, int inputOffset)
    {
        unchecked
        {
            return p[inputOffset]
                | (uint)p[inputOffset + 1] << 8
                | (uint)p[inputOffset + 2] << 16
                | (uint)p[inputOffset + 3] << 24;
        }
    }

    /// <summary>
    /// Serialize the input integer into the output buffer. The input integer will be split into 4 bytes and put into four sequential places in the output buffer, starting at the outputOffset.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="input"></param>
    /// <param name="outputOffset"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToBytes(byte[] output, uint input, int outputOffset)
    {
        unchecked
        {
            output[outputOffset] = (byte)input;
            output[outputOffset + 1] = (byte)(input >> 8);
            output[outputOffset + 2] = (byte)(input >> 16);
            output[outputOffset + 3] = (byte)(input >> 24);
        }
    }

    /// <summary>
    /// Reverses the byte order of a 32-bit unsigned integer.
    /// </summary>
    /// <param name = "value" > The value needs to be reversed byte order.</param>
    /// <returns>The value after inverting the byte order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReverseBytes(uint value)
    {
        if (Ssse3.IsSupported)
        {
            Vector128<byte> vec = Vector128.CreateScalarUnsafe(value).AsByte();
            Vector128<byte> mask = Vector128.Create(
                3, 2, 1, (byte)0,
              0, 0, 0, 0,
              0, 0, 0, 0,
              0, 0, 0, 0);
            Vector128<byte> shuffled = Ssse3.Shuffle(vec, mask);
            return shuffled.AsUInt32().GetElement(0);
        }
        else
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }
    }

    /// <summary>
    /// Performs a single round of the SHA-256 hash function.
    /// This method updates the internal state variables of the SHA-256 algorithm based on the provided message word (w)
    /// and constant (k), performing the necessary bitwise operations and transformations.
    /// </summary>
    /// <param name="a">The first internal state variable (a) of the SHA-256 algorithm.</param>
    /// <param name="b">The second internal state variable (b) of the SHA-256 algorithm.</param>
    /// <param name="c">The third internal state variable (bits) of the SHA-256 algorithm.</param>
    /// <param name="d">The fourth internal state variable (d) of the SHA-256 algorithm.</param>
    /// <param name="e">The fifth internal state variable (e) of the SHA-256 algorithm.</param>
    /// <param name="f">The sixth internal state variable (f) of the SHA-256 algorithm.</param>
    /// <param name="g">The seventh internal state variable (g) of the SHA-256 algorithm.</param>
    /// <param name="h">The eighth internal state variable (h) of the SHA-256 algorithm.</param>
    /// <param name="w">The message word (w) used in the current round of the SHA-256 algorithm.</param>
    /// <param name="k">The constant value (k) used in the current round of the SHA-256 algorithm.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Round(
    ref uint a, ref uint b, ref uint c, ref uint d,
    ref uint e, ref uint f, ref uint g, ref uint h,
    uint w, uint k)
    {
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
