namespace Nalix.Cryptography.Utilities;

public static partial class BitwiseUtils
{
    /// <summary>
    /// Performs the "Choose" (Choose) bitwise function used in SHA-family hash algorithms.
    /// Returns a value formed by selecting bits from <paramref name="y"/> or <paramref name="z"/>,
    /// depending on the corresponding bit in <paramref name="x"/>.
    ///
    /// Equivalent to: (x AND y) XOR (NOT x AND z)
    /// </summary>
    /// <param name="x">Selector bits. If a bit is 1, choose from <paramref name="y"/>; otherwise, from <paramref name="z"/>.</param>
    /// <param name="y">Bits to choose when the corresponding bit in <paramref name="x"/> is 1.</param>
    /// <param name="z">Bits to choose when the corresponding bit in <paramref name="x"/> is 0.</param>
    /// <returns>The result of (x &amp; y) ^ (~x &amp; z).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint Choose(uint x, uint y, uint z) => (x & y) ^ (~x & z);

    /// <summary>
    /// Performs the "Majority" (Majority) bitwise function used in SHA-family hash algorithms.
    /// Returns a value where each bit is the majority value of the corresponding bits in
    /// <paramref name="x"/>, <paramref name="y"/>, and <paramref name="z"/>.
    ///
    /// Equivalent to: (x AND y) XOR (x AND z) XOR (y AND z)
    /// </summary>
    /// <param name="x">First input value.</param>
    /// <param name="y">Second input value.</param>
    /// <param name="z">Third input value.</param>
    /// <returns>The result of (x &amp; y) ^ (x &amp; z) ^ (y &amp; z).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint Majority(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);

    /// <summary>
    /// Computes the Σ₀ (Big Sigma 0) function used in the SHA-256 compression function.
    /// This function performs three right rotations: ROTR⁲(x), ROTR¹³(x), ROTR²²(x).
    ///
    /// Σ₀(x) = ROTR²(x) ⊕ ROTR¹³(x) ⊕ ROTR²²(x)
    /// </summary>
    /// <param name="x">The input 32-bit word.</param>
    /// <returns>The result of Σ₀(x).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint SigmaUpper0(uint x) =>
        RotateRight(x, 2) ^ RotateRight(x, 13) ^ RotateRight(x, 22);

    /// <summary>
    /// Computes the Σ₁ (Big Sigma 1) function used in the SHA-256 compression function.
    /// This function performs three right rotations: ROTR⁶(x), ROTR¹¹(x), ROTR²⁵(x).
    ///
    /// Σ₁(x) = ROTR⁶(x) ⊕ ROTR¹¹(x) ⊕ ROTR²⁵(x)
    /// </summary>
    /// <param name="x">The input 32-bit word.</param>
    /// <returns>The result of Σ₁(x).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint SigmaUpper1(uint x) =>
        RotateRight(x, 6) ^ RotateRight(x, 11) ^ RotateRight(x, 25);

    /// <summary>
    /// Compares two byte spans in a fixed-time manner to prevent timing attacks.
    /// </summary>
    /// <param name="left">The first byte span to compare.</param>
    /// <param name="right">The second byte span to compare.</param>
    /// <returns>True if the byte spans are equal; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
    public static bool FixedTimeEquals(System.ReadOnlySpan<byte> left, System.ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length) return false;

        int result = 0;

        for (int i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Increments a counter value stored in a byte array.
    /// </summary>
    /// <param name="counter">The counter to increment.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementCounter(System.Span<byte> counter)
    {
        for (int i = 0; i < counter.Length; i++)
        {
            if (++counter[i] != 0) break;
        }
    }
}
