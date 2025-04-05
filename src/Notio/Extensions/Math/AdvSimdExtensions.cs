using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Notio.Extensions;

/// <summary>
/// Provides SIMD-accelerated cryptographic helper functions.
/// </summary>
public static class AdvSimdExtensions
{
    // Pre-computed shuffle mask for byte reversal
    private static readonly Vector128<byte> _reverseBytesMask = Vector128
        .Create(3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, (byte)12);

    /// <summary>
    /// Reverses the byte order within each 32-bit word in the given vector.
    /// </summary>
    /// <param name="value">The input vector of bytes.</param>
    /// <returns>A vector with reversed byte order in each word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> ReverseElement8InBytesInWord(Vector128<byte> value)
        => Ssse3.Shuffle(value, _reverseBytesMask);

    /// <summary>
    /// Performs a right rotation on a 128-bit vector of 32-bit unsigned integers.
    /// </summary>
    /// <param name="value">The input vector.</param>
    /// <param name="count">The Number of bits to rotate.</param>
    /// <returns>A vector with each 32-bit element rotated right by the specified count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> RotateRight(Vector128<uint> value, byte count)
    {
        // Use direct bit operations to eliminate branch overhead
        // Important for SHA-256 performance: optimize for counts 7, 17, 18, and 19
        if (count == 7)
            return Sse2.Or(Sse2.ShiftRightLogical(value, 7), Sse2.ShiftLeftLogical(value, 25));
        else if (count == 17)
            return Sse2.Or(Sse2.ShiftRightLogical(value, 17), Sse2.ShiftLeftLogical(value, 15));
        else if (count == 18)
            return Sse2.Or(Sse2.ShiftRightLogical(value, 18), Sse2.ShiftLeftLogical(value, 14));
        else if (count == 19)
            return Sse2.Or(Sse2.ShiftRightLogical(value, 19), Sse2.ShiftLeftLogical(value, 13));
        else
            return RotateRightGeneric(value, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector128<uint> RotateRightGeneric(Vector128<uint> value, byte count)
    {
        // Stack allocation avoids heap allocation of the array
        uint* temp = stackalloc uint[4];

        // Store vector to stack-allocated memory
        Sse2.Store(temp, value);

        // Perform rotation on each element
        for (int i = 0; i < 4; i++)
        {
            temp[i] = temp[i] >> count | temp[i] << 32 - count;
        }

        // Load rotated values back into vector
        return Sse2.LoadVector128(temp);
    }
}
