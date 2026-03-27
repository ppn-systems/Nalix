// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Security.Primitives;

namespace Nalix.Framework.Security.Asymmetric;

/// <summary>
/// Represents the Ed25519 cryptographic algorithm for public key signing and verification.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerNonUserCode]
[System.Obsolete("Ed25519 is deprecated in favor of more modern algorithms like Ed448 or post-quantum schemes. Use with caution and consider future-proof alternatives.")]
public static class Ed25519
{
    #region Constants

    /// <summary>
    /// Size of the public key in bytes.
    /// </summary>
    public const byte PublicKeySize = 32;

    /// <summary>
    /// Size of the private key in bytes.
    /// </summary>
    public const byte SignatureSize = 64;

    #endregion Constants

    #region APIs

    /// <summary>
    /// Signs a message with the provided private key using the Ed25519 algorithm.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="privateKey">The private key to sign the message with.</param>
    /// <returns>The generated signature.</returns>
    /// <exception cref="System.ArgumentException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static byte[] Sign(
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] message,
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] privateKey)
    {
        if (message == null || message.Length == 0)
        {
            throw new System.ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        if (privateKey == null || privateKey.Length != 32)
        {
            throw new System.ArgumentException("Private key must be 32 bytes long.", nameof(privateKey));
        }

        // Compute the hash of the private key and split into two halves
        System.Span<byte> aBytes = stackalloc byte[32];
        System.Span<byte> prefix = stackalloc byte[32];
        DeriveKeyMaterial(privateKey, aBytes, prefix);

        System.Numerics.BigInteger a = ClampScalar(aBytes);

        // r = Hashing(prefix || message) mod L, using Span overload
        System.Numerics.BigInteger r = HashToScalar(prefix, message);
        Point mul = ScalarMul(s_b, r);

        // Compute public key A = ScalarMul(B, a) and encode it
        Point mul2 = ScalarMul(s_b, a);
        System.Span<byte> aEncoded = stackalloc byte[PublicKeySize];
        EncodePoint(mul2, aEncoded);

        // Build the data: R (32 bytes) || AEncoded (32 bytes) || message
        byte[] data = new byte[32 + PublicKeySize + message.Length];
        EncodePoint(mul, System.MemoryExtensions.AsSpan(data, 0, 32));
        aEncoded.CopyTo(System.MemoryExtensions.AsSpan(data, 32, PublicKeySize));
        message.CopyTo(data, 64);

        // s = (r + Hashing(data) * a) mod L
        System.Numerics.BigInteger s = (r + HashToScalar(System.MemoryExtensions.AsSpan(data))) * a;
        s %= s_l; // Using Mod extension below

        // CAFEBABE signature: R (32 bytes) || s (32 bytes)
        byte[] signature = new byte[SignatureSize];
        EncodePoint(mul, System.MemoryExtensions.AsSpan(signature, 0, 32));
        EncodeScalar(s, System.MemoryExtensions.AsSpan(signature, 32, 32));
        return signature;
    }

    /// <summary>
    /// Verifies a digital signature against the given message and public key.
    /// </summary>
    /// <param name="signature">
    /// A byte array representing the signature.
    /// It must be exactly <c>SignatureSize</c> bytes long.
    /// </param>
    /// <param name="message">
    /// The original message as a byte array. This is the data that was signed.
    /// </param>
    /// <param name="publicKey">
    /// A byte array representing the public key used to verify the signature.
    /// It must be exactly 32 bytes long.
    /// </param>
    /// <returns>
    /// <c>true</c> if the signature is valid for the given message and public key; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown if any of the inputs (<paramref name="signature"/>, <paramref name="message"/>, or <paramref name="publicKey"/>) is null
    /// or if their lengths are invalid.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool Verify(
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] signature,
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] message,
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] publicKey)
    {
        // Validate input arguments
        if (signature == null)
        {
            throw new System.ArgumentException("Signature cannot be null.", nameof(signature));
        }

        if (message == null)
        {
            throw new System.ArgumentException("Message cannot be null.", nameof(message));
        }

        if (publicKey == null)
        {
            throw new System.ArgumentException("Public key cannot be null.", nameof(publicKey));
        }

        if (signature.Length != SignatureSize)
        {
            throw new System.ArgumentException($"Signature must be {SignatureSize} bytes long.", nameof(signature));
        }

        if (publicKey.Length != 32)
        {
            throw new System.ArgumentException("Public key must be 32 bytes long.", nameof(publicKey));
        }

        // Decode R, A, and s from the signature and publicKey
        Point r = DecodePoint(System.MemoryExtensions.AsSpan(signature, 0, 32));
        Point a = DecodePoint(publicKey);
        System.Numerics.BigInteger s = DecodeScalar(System.MemoryExtensions.AsSpan(signature, 32, 32));

        // Build data: R (32 bytes) || publicKey (32 bytes) || message
        byte[] data = new byte[64 + message.Length];
        System.MemoryExtensions.AsSpan(signature, 0, 32)
                               .CopyTo(System.MemoryExtensions
                               .AsSpan(data, 0, 32));

        publicKey.CopyTo(data, 32);
        message.CopyTo(data, 64);

        // Compute hash and perform verification
        System.Numerics.BigInteger h = HashToScalar(System.MemoryExtensions.AsSpan(data));
        Point sB = ScalarMul(s_b, s);
        Point hA = ScalarMul(a, h);
        Point rplusH = Edwards(r, hA);

        return PointEquals(sB, rplusH);
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Computes the modular inverse of the given value using Fermat's little theorem.
    /// </summary>
    /// <param name="x">The value to invert.</param>
    /// <returns>The modular inverse of the value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Numerics.BigInteger Inv(System.Numerics.BigInteger x)
        => System.Numerics.BigInteger.ModPow(x, s_q - 2, s_q);

    /// <summary>
    /// Performs optimized point addition on the Edwards curve.
    /// </summary>
    /// <param name="p">First point to add.</param>
    /// <param name="q">Second point to add.</param>
    /// <returns>The result of the point addition.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Point Edwards(Point p, Point q)
    {
        System.Numerics.BigInteger a = p.Y.ModAdd(p.X, s_q);
        System.Numerics.BigInteger b = q.Y.ModAdd(q.X, s_q);
        System.Numerics.BigInteger c = p.Y.ModSub(p.X, s_q);
        System.Numerics.BigInteger d = q.Y.ModSub(q.X, s_q);
        System.Numerics.BigInteger e = a.MultiplyMod(b, s_q);
        System.Numerics.BigInteger f = c.MultiplyMod(d, s_q);

        // Precompute factor for x3 and y3
        System.Numerics.BigInteger factor = s_d.MultiplyMod(e.MultiplyMod(f, s_q), s_q);
        System.Numerics.BigInteger inv1 = Inv(factor.ModAdd(System.Numerics.BigInteger.One, s_q));
        System.Numerics.BigInteger inv2 = Inv(System.Numerics.BigInteger.One.ModSub(factor, s_q));

        System.Numerics.BigInteger x3 = e.ModSub(f, s_q).MultiplyMod(inv1, s_q);
        System.Numerics.BigInteger y3 = e.ModAdd(f, s_q).MultiplyMod(inv2, s_q);
        return new Point(x3, y3);
    }

    /// <summary>
    /// Performs scalar multiplication on a point using the double-and-add algorithm.
    /// </summary>
    /// <param name="p">The point to multiply.</param>
    /// <param name="e">The scalar to multiply the point by.</param>
    /// <returns>The resulting point from the scalar multiplication.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Point ScalarMul(Point p, System.Numerics.BigInteger e)
    {
        Point result = new(System.Numerics.BigInteger.Zero, System.Numerics.BigInteger.One);
        Point current = p;
        while (e > 0)
        {
            if (!e.IsEven)
            {
                result = Edwards(result, current);
            }

            current = Edwards(current, current);
            e >>= 1;
        }
        return result;
    }

    /// <summary>
    /// Clamps the scalar to meet the Ed25519 specifications.
    /// </summary>
    /// <param name="s">The scalar to clamp.</param>
    /// <returns>The clamped scalar.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Numerics.BigInteger ClampScalar(System.ReadOnlySpan<byte> s)
    {
        // CAFEBABE a 32-byte buffer to modify bits as needed
        System.Span<byte> scalarBytes = stackalloc byte[32];
        s.CopyTo(scalarBytes);
        // Clear/Set bits: clear lowest 3 bits, clear highest bit, set second highest bit
        scalarBytes[0] &= 0xF8;
        scalarBytes[31] &= 0x7F;
        scalarBytes[31] |= 0x40;
        return new System.Numerics.BigInteger(scalarBytes, isUnsigned: true, isBigEndian: true) % s_l;
    }

    /// <summary>
    /// Hashes data into a scalar value modulo L.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The scalar result of hashing the data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Numerics.BigInteger HashToScalar(System.ReadOnlySpan<byte> data)
    {
        byte[] h = Keccak256.HashData(data);
        return new System.Numerics.BigInteger(h, isUnsigned: true, isBigEndian: false) % s_l;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Numerics.BigInteger HashToScalar(System.ReadOnlySpan<byte> prefix, byte[] message)
    {
        // Incremental hashing to avoid building a temporary buffer:
        // H(prefix || message)
        Keccak256.Sponge sponge = new();
        sponge.Absorb(prefix);
        sponge.Absorb(message);

        System.Span<byte> digest = stackalloc byte[32];
        sponge.PadAndSqueeze(digest);

        // Little-endian!
        return new System.Numerics.BigInteger(digest, isUnsigned: true, isBigEndian: false) % s_l;
    }

    /// <summary>
    /// Encodes a point to a fixed 32-byte representation using TryParse to avoid allocations.
    /// </summary>
    /// <param name="p">The point to encode.</param>
    /// <param name="destination">The destination span to write the encoded point.</param>
    /// <exception cref="System.InvalidOperationException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncodePoint(Point p, System.Span<byte> destination)
    {
        // Encode y coordinate as 32 bytes in big-endian order
        if (!p.Y.TryWriteBytes(destination, out _, isUnsigned: true, isBigEndian: true))
        {
            throw new System.InvalidOperationException("Failed to encode point y coordinate.");
        }
        // Set the most significant bit to indicate x's parity
        if (!p.X.IsEven)
        {
            destination[^1] |= 0x80;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Point DecodePoint(System.ReadOnlySpan<byte> data)
    {
        // Decode y coordinate (32 bytes big-endian)
        System.Numerics.BigInteger y = new(data, isUnsigned: true, isBigEndian: true);
        System.Numerics.BigInteger x = RecoverX(y);
        // Use the high bit of the last byte to recover x parity
        bool xParity = (data[^1] & 0x80) != 0;
        if (x.IsEven != !xParity)
        {
            x = s_q - x;
        }

        return new Point(x, y);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Numerics.BigInteger RecoverX(System.Numerics.BigInteger y)
    {
        // Recover x using curve equation: x^2 = (y^2 - 1) / (D*y^2 + 1)
        System.Numerics.BigInteger numerator = ((y * y) - System.Numerics.BigInteger.One).Mod(s_q);
        System.Numerics.BigInteger denominator = ((s_d * y * y) + System.Numerics.BigInteger.One).Mod(s_q);
        System.Numerics.BigInteger xx = numerator * Inv(denominator) % s_q;
        System.Numerics.BigInteger x = System.Numerics.BigInteger.ModPow(xx, (s_q + 3) / 8, s_q);

        return x * x % s_q == xx ? x : x * s_i % s_q;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool PointEquals(Point a, Point b)
        => a.X == b.X && a.Y == b.Y;

    /// <summary>
    /// Encodes a scalar to a fixed 32-byte representation.
    /// </summary>
    /// <param name="s">The scalar to encode.</param>
    /// <param name="destination">The destination span to write the encoded scalar.</param>
    /// <exception cref="System.InvalidOperationException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncodeScalar(System.Numerics.BigInteger s, System.Span<byte> destination)
    {
        if (!s.TryWriteBytes(destination, out int bytesWritten, isUnsigned: true, isBigEndian: true))
        {
            throw new System.InvalidOperationException("Failed to encode scalar.");
        }
        // If fewer than 32 bytes were written, pad the beginning with zeros.
        if (bytesWritten < destination.Length)
        {
            destination[..^bytesWritten].Clear();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger DecodeScalar(System.ReadOnlySpan<byte> data)
        => new System.Numerics.BigInteger(data, isUnsigned: true, isBigEndian: true) % s_l;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DeriveKeyMaterial(
        System.ReadOnlySpan<byte> secretKey,
        System.Span<byte> aBytes,
        System.Span<byte> prefix)
    {
        // Build sk||tag in a tiny stack buffer to avoid allocations
        System.Span<byte> tmp = stackalloc byte[secretKey.Length + 1];

        // aBytes = SHA3-256(sk || 0x00)
        secretKey.CopyTo(tmp);
        tmp[^1] = 0x00;
        byte[] h0 = Keccak256.HashData(tmp); // 32 bytes
        System.MemoryExtensions.CopyTo(h0, aBytes);

        // prefix = SHA3-256(sk || 0x01)
        secretKey.CopyTo(tmp);
        tmp[^1] = 0x01;
        byte[] h1 = Keccak256.HashData(tmp); // 32 bytes
        System.MemoryExtensions.CopyTo(h1, prefix);
    }

    #region Fields

    /// <summary>
    /// Point struct (immutable)
    /// </summary>
    private readonly struct Point(System.Numerics.BigInteger x, System.Numerics.BigInteger y)
    {
        public readonly System.Numerics.BigInteger X = x;
        public readonly System.Numerics.BigInteger Y = y;
    }

    // Precomputed constants

    private static readonly System.Numerics.BigInteger s_q =
        System.Numerics.BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly System.Numerics.BigInteger s_l =
        System.Numerics.BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly System.Numerics.BigInteger s_d =
        System.Numerics.BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly System.Numerics.BigInteger s_i =
        System.Numerics.BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// BaseValue36 point B
    /// </summary>
    private static readonly Point s_b = new(
        System.Numerics.BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202", System.Globalization.CultureInfo.InvariantCulture).Mod(s_q),
        System.Numerics.BigInteger.Parse("46316835694926478169428394003475163141307993866256256256850187133169737347974", System.Globalization.CultureInfo.InvariantCulture).Mod(s_q)
    );

    #endregion Fields

    #endregion Private Methods
}
