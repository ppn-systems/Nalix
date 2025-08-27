// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Cryptography.Hashing;
using Nalix.Shared.Extensions;

namespace Nalix.Cryptography.Asymmetric;

/// <summary>
/// Represents the Ed25519 cryptographic algorithm for public key signing and verification.
/// </summary>
public static class Ed25519
{
    #region Constants

    /// <summary>
    /// Size of the public key in bytes.
    /// </summary>
    public const System.Byte PublicKeySize = 32;

    /// <summary>
    /// Size of the private key in bytes.
    /// </summary>
    public const System.Byte SignatureSize = 64;

    #endregion Constants

    #region APIs

    /// <summary>
    /// Signs a message with the provided private key using the Ed25519 algorithm.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="privateKey">The private key to sign the message with.</param>
    /// <returns>The generated signature.</returns>
    public static System.Byte[] Sign(System.Byte[] message, System.Byte[] privateKey)
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
        System.Byte[] h = ComputeHash(privateKey);
        System.Numerics.BigInteger a = ClampScalar(System.MemoryExtensions.AsSpan(h, 0, 32));
        System.ReadOnlySpan<System.Byte> prefix = new(h, 32, h.Length - 32);

        // r = Hashing(prefix || message) mod L, using Span overload
        System.Numerics.BigInteger r = HashToScalar(prefix, message);
        Point mul = ScalarMul(B, r);

        // Compute public key A = ScalarMul(B, a) and encode it
        Point mul2 = ScalarMul(B, a);
        System.Span<System.Byte> aEncoded = stackalloc System.Byte[PublicKeySize];
        EncodePoint(mul2, aEncoded);

        // Build the data: R (32 bytes) || AEncoded (32 bytes) || message
        System.Byte[] data = new System.Byte[32 + PublicKeySize + message.Length];
        EncodePoint(mul, System.MemoryExtensions.AsSpan(data, 0, 32));
        aEncoded.CopyTo(System.MemoryExtensions.AsSpan(data, 32, PublicKeySize));
        message.CopyTo(data, 64);

        // s = (r + Hashing(data) * a) mod L
        System.Numerics.BigInteger s = (r + HashToScalar(System.MemoryExtensions.AsSpan(data))) * a;
        s %= L; // Using Mod extension below

        // Create signature: R (32 bytes) || s (32 bytes)
        System.Byte[] signature = new System.Byte[SignatureSize];
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
    public static System.Boolean Verify(System.Byte[] signature, System.Byte[] message, System.Byte[] publicKey)
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
        System.Byte[] data = new System.Byte[64 + message.Length];
        System.MemoryExtensions.AsSpan(signature, 0, 32)
                               .CopyTo(System.MemoryExtensions
                               .AsSpan(data, 0, 32));

        publicKey.CopyTo(data, 32);
        message.CopyTo(data, 64);

        // Compute hash and perform verification
        System.Numerics.BigInteger h = HashToScalar(System.MemoryExtensions.AsSpan(data));
        Point sB = ScalarMul(B, s);
        Point hA = ScalarMul(a, h);
        Point rplusH = Edwards(r, hA);

        return PointEquals(sB, rplusH);
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Computes the SHA-512 hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The hash of the data as a byte array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data)
        => (Sha512.Value ?? new SHA512()).ComputeHash(data);

    /// <summary>
    /// Computes the modular inverse of the given value using Fermat's little theorem.
    /// </summary>
    /// <param name="x">The value to invert.</param>
    /// <returns>The modular inverse of the value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger Inv(System.Numerics.BigInteger x)
        => System.Numerics.BigInteger.ModPow(x, Q - 2, Q);

    /// <summary>
    /// Performs optimized point addition on the Edwards curve.
    /// </summary>
    /// <param name="p">First point to add.</param>
    /// <param name="q">Second point to add.</param>
    /// <returns>The result of the point addition.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Point Edwards(Point p, Point q)
    {
        System.Numerics.BigInteger a = p.Y.ModAdd(p.X, Q);
        System.Numerics.BigInteger b = q.Y.ModAdd(q.X, Q);
        System.Numerics.BigInteger c = p.Y.ModSub(p.X, Q);
        System.Numerics.BigInteger d = q.Y.ModSub(q.X, Q);
        System.Numerics.BigInteger e = a.MultiplyMod(b, Q);
        System.Numerics.BigInteger f = c.MultiplyMod(d, Q);

        // Precompute factor for x3 and y3
        System.Numerics.BigInteger factor = D.MultiplyMod(e.MultiplyMod(f, Q), Q);
        System.Numerics.BigInteger inv1 = Inv(factor.ModAdd(System.Numerics.BigInteger.One, Q));
        System.Numerics.BigInteger inv2 = Inv(System.Numerics.BigInteger.One.ModSub(factor, Q));

        System.Numerics.BigInteger x3 = e.ModSub(f, Q).MultiplyMod(inv1, Q);
        System.Numerics.BigInteger y3 = e.ModAdd(f, Q).MultiplyMod(inv2, Q);
        return new Point(x3, y3);
    }

    /// <summary>
    /// Performs scalar multiplication on a point using the double-and-add algorithm.
    /// </summary>
    /// <param name="p">The point to multiply.</param>
    /// <param name="e">The scalar to multiply the point by.</param>
    /// <returns>The resulting point from the scalar multiplication.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger ClampScalar(System.ReadOnlySpan<System.Byte> s)
    {
        // Create a 32-byte buffer to modify bits as needed
        System.Span<System.Byte> scalarBytes = stackalloc System.Byte[32];
        s.CopyTo(scalarBytes);
        // Clear/Set bits: clear lowest 3 bits, clear highest bit, set second highest bit
        scalarBytes[0] &= 0xF8;
        scalarBytes[31] &= 0x7F;
        scalarBytes[31] |= 0x40;
        return new System.Numerics.BigInteger(scalarBytes, isUnsigned: true, isBigEndian: true) % L;
    }

    /// <summary>
    /// Hashes data into a scalar value modulo L.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The scalar result of hashing the data.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger HashToScalar(System.ReadOnlySpan<System.Byte> data)
    {
        System.Byte[] h = ComputeHash(data);
        return new System.Numerics.BigInteger(h, isUnsigned: true, isBigEndian: false) % L;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger HashToScalar(
        System.ReadOnlySpan<System.Byte> prefix, System.Byte[] message)
    {
        System.Int32 len = prefix.Length + message.Length;
        if (len <= 1024)
        {
            System.Span<System.Byte> buf = stackalloc System.Byte[len];
            prefix.CopyTo(buf);
            System.MemoryExtensions.CopyTo(message, buf[prefix.Length..]);
            var h = ComputeHash(buf);
            // Little-endian!
            return new System.Numerics.BigInteger(h, isUnsigned: true, isBigEndian: false) % L;
        }
        else
        {
            // Use pooled array to avoid LOH
            var pool = System.Buffers.ArrayPool<System.Byte>.Shared;
            System.Byte[] rented = pool.Rent(len);
            try
            {
                var span = System.MemoryExtensions.AsSpan(rented, 0, len);
                prefix.CopyTo(span);
                System.MemoryExtensions.CopyTo(message, span[prefix.Length..]);
                var h = ComputeHash(span);
                return new System.Numerics.BigInteger(h, isUnsigned: true, isBigEndian: false) % L;
            }
            finally { pool.Return(rented, clearArray: true); }
        }
    }

    /// <summary>
    /// Encodes a point to a fixed 32-byte representation using TryDeserialize to avoid allocations.
    /// </summary>
    /// <param name="p">The point to encode.</param>
    /// <param name="destination">The destination span to write the encoded point.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncodePoint(Point p, System.Span<System.Byte> destination)
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
    private static Point DecodePoint(System.ReadOnlySpan<System.Byte> data)
    {
        // Decode y coordinate (32 bytes big-endian)
        System.Numerics.BigInteger y = new(data, isUnsigned: true, isBigEndian: true);
        System.Numerics.BigInteger x = RecoverX(y);
        // Use the high bit of the last byte to recover x parity
        System.Boolean xParity = (data[^1] & 0x80) != 0;
        if (x.IsEven != !xParity)
        {
            x = Q - x;
        }

        return new Point(x, y);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger RecoverX(System.Numerics.BigInteger y)
    {
        // Recover x using curve equation: x^2 = (y^2 - 1) / (D*y^2 + 1)
        System.Numerics.BigInteger numerator = ((y * y) - System.Numerics.BigInteger.One).Mod(Q);
        System.Numerics.BigInteger denominator = ((D * y * y) + System.Numerics.BigInteger.One).Mod(Q);
        System.Numerics.BigInteger xx = numerator * Inv(denominator) % Q;
        System.Numerics.BigInteger x = System.Numerics.BigInteger.ModPow(xx, (Q + 3) / 8, Q);

        return x * x % Q == xx ? x : x * I % Q;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean PointEquals(Point a, Point b)
        => a.X == b.X && a.Y == b.Y;

    /// <summary>
    /// Encodes a scalar to a fixed 32-byte representation.
    /// </summary>
    /// <param name="s">The scalar to encode.</param>
    /// <param name="destination">The destination span to write the encoded scalar.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncodeScalar(System.Numerics.BigInteger s, System.Span<System.Byte> destination)
    {
        if (!s.TryWriteBytes(destination, out System.Int32 bytesWritten, isUnsigned: true, isBigEndian: true))
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
    private static System.Numerics.BigInteger DecodeScalar(System.ReadOnlySpan<System.Byte> data)
        => new System.Numerics.BigInteger(data, isUnsigned: true, isBigEndian: true) % L;

    #region Fields

    // Point struct (immutable)
    private readonly struct Point(System.Numerics.BigInteger x, System.Numerics.BigInteger y)
    {
        public readonly System.Numerics.BigInteger X = x;
        public readonly System.Numerics.BigInteger Y = y;
    }

    // Optimized SHA-512 with buffer reuse (thread-local instance)
    private static readonly System.Threading.ThreadLocal<SHA512> Sha512 = new();

    // Precomputed constants

    private static readonly System.Numerics.BigInteger Q =
        System.Numerics.BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");

    private static readonly System.Numerics.BigInteger L =
        System.Numerics.BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");

    private static readonly System.Numerics.BigInteger D =
        System.Numerics.BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740");

    private static readonly System.Numerics.BigInteger I =
        System.Numerics.BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752");

    // BaseValue36 point B
    private static readonly Point B = new(
        System.Numerics.BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202").Mod(Q),
        System.Numerics.BigInteger.Parse("46316835694926478169428394003475163141307993866256256256850187133169737347974").Mod(Q)
    );

    #endregion Fields

    #endregion Private Methods
}