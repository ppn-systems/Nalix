namespace Nalix.Cryptography.Utils;

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
    public static System.UInt32 Choose(System.UInt32 x, System.UInt32 y, System.UInt32 z)
        => (x & y) ^ (~x & z);

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
    public static System.UInt32 Majority(System.UInt32 x, System.UInt32 y, System.UInt32 z)
        => (x & y) ^ (x & z) ^ (y & z);

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
    public static System.UInt32 SigmaUpper0(System.UInt32 x) =>
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
    public static System.UInt32 SigmaUpper1(System.UInt32 x) =>
        RotateRight(x, 6) ^ RotateRight(x, 11) ^ RotateRight(x, 25);

    /// <summary>
    /// Computes the σ₀ (Small Sigma 0) function used in SHA-256 message schedule expansion.
    /// This function performs: ROTR⁷(x) ⊕ ROTR¹⁸(x) ⊕ SHR³(x).
    ///
    /// σ₀(x) = ROTR⁷(x) ⊕ ROTR¹⁸(x) ⊕ (x >>> 3)
    /// </summary>
    /// <param name="x">The input 32-bit word.</param>
    /// <returns>The result of σ₀(x).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Sigma0(System.UInt32 x) => RotateRight(x, 7) ^ RotateRight(x, 18) ^ (x >> 3);

    /// <summary>
    /// Computes the σ₁ (Small Sigma 1) function used in SHA-256 message schedule expansion.
    /// This function performs: ROTR¹⁷(x) ⊕ ROTR¹⁹(x) ⊕ SHR¹⁰(x).
    ///
    /// σ₁(x) = ROTR¹⁷(x) ⊕ ROTR¹⁹(x) ⊕ (x >>> 10)
    /// </summary>
    /// <param name="x">The input 32-bit word.</param>
    /// <returns>The result of σ₁(x).</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Sigma1(System.UInt32 x)
        => RotateRight(x, 17) ^ RotateRight(x, 19) ^ (x >> 10);
}