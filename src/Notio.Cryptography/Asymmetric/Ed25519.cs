using Notio.Cryptography.Extensions;
using Notio.Cryptography.Hash;
using System;
using System.Numerics;
using System.Threading;

namespace Notio.Cryptography.Asymmetric;

/// <summary>
/// Represents the Ed25519 cryptographic algorithm for public key signing and verification.
/// </summary>
public sealed class Ed25519
{
    /// <summary>
    /// Size of the public key in bytes.
    /// </summary>
    public const int PublicKeySize = 32;

    /// <summary>
    /// Size of the signature in bytes.
    /// </summary>
    public const int SignatureSize = 64;

    /// <summary>
    /// Computes the SHA-512 hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The hash of the data as a byte array.</returns>
    public static byte[] ComputeHash(ReadOnlySpan<byte> data)
        => (Sha512.Value ?? new SHA256()).ComputeHash(data);

    /// <summary>
    /// Signs a message with the provided private key using the Ed25519 algorithm.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="privateKey">The private key to sign the message with.</param>
    /// <returns>The generated signature.</returns>
    public static byte[] Sign(byte[] message, byte[] privateKey)
    {
        if (message == null || message.Length == 0)
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        if (privateKey == null || privateKey.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes long.", nameof(privateKey));

        // Compute the hash of the private key and split into two halves
        var h = ComputeHash(privateKey);
        var a = ClampScalar(h.AsSpan(0, 32));
        ReadOnlySpan<byte> prefix = h.AsSpan(32, 32);

        // r = Hash(prefix || message) mod L, using Span overload
        var r = HashToScalar(prefix, message);
        var R = ScalarMul(B, r);

        // Compute public key A = ScalarMul(B, a) and encode it
        var A = ScalarMul(B, a);
        Span<byte> AEncoded = stackalloc byte[PublicKeySize];
        EncodePoint(A, AEncoded);

        // Build the data: R (32 bytes) || AEncoded (32 bytes) || message
        byte[] data = new byte[32 + PublicKeySize + message.Length];
        EncodePoint(R, data.AsSpan(0, 32));
        AEncoded.CopyTo(data.AsSpan(32, PublicKeySize));
        message.CopyTo(data, 64);

        // s = (r + Hash(data) * a) mod L
        var s = (r + HashToScalar(data.AsSpan())) * a;
        s %= L; // Using Mod extension below

        // Create signature: R (32 bytes) || s (32 bytes)
        byte[] signature = new byte[SignatureSize];
        EncodePoint(R, signature.AsSpan(0, 32));
        EncodeScalar(s, signature.AsSpan(32, 32));
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
    /// <exception cref="ArgumentException">
    /// Thrown if any of the inputs (<paramref name="signature"/>, <paramref name="message"/>, or <paramref name="publicKey"/>) is null
    /// or if their lengths are invalid.
    /// </exception>
    public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
    {
        // Validate input arguments
        if (signature == null)
            throw new ArgumentException("Signature cannot be null.", nameof(signature));
        if (message == null)
            throw new ArgumentException("Message cannot be null.", nameof(message));
        if (publicKey == null)
            throw new ArgumentException("Public key cannot be null.", nameof(publicKey));

        if (signature.Length != SignatureSize)
            throw new ArgumentException($"Signature must be {SignatureSize} bytes long.", nameof(signature));
        if (publicKey.Length != 32)
            throw new ArgumentException("Public key must be 32 bytes long.", nameof(publicKey));

        // Decode R, A, and s from the signature and publicKey
        var R = DecodePoint(signature.AsSpan(0, 32));
        var A = DecodePoint(publicKey);
        var s = DecodeScalar(signature.AsSpan(32, 32));

        // Build data: R (32 bytes) || publicKey (32 bytes) || message
        byte[] data = new byte[64 + message.Length];
        signature.AsSpan(0, 32).CopyTo(data.AsSpan(0, 32));
        publicKey.CopyTo(data, 32);
        message.CopyTo(data, 64);

        // Compute hash and perform verification
        var h = HashToScalar(data.AsSpan());
        var sB = ScalarMul(B, s);
        var hA = ScalarMul(A, h);
        var RplusH = Edwards(R, hA);

        return PointEquals(sB, RplusH);
    }

    /// <summary>
    /// Computes the modular inverse of the given value using Fermat's little theorem.
    /// </summary>
    /// <param name="x">The value to invert.</param>
    /// <returns>The modular inverse of the value.</returns>
    private static BigInteger Inv(BigInteger x) => BigInteger.ModPow(x, Q - 2, Q);

    /// <summary>
    /// Performs optimized point addition on the Edwards curve.
    /// </summary>
    /// <param name="p">First point to add.</param>
    /// <param name="q">Second point to add.</param>
    /// <returns>The result of the point addition.</returns>
    private static Point Edwards(Point p, Point q)
    {
        var a = p.Y.ModAdd(p.X, Q);
        var b = q.Y.ModAdd(q.X, Q);
        var c = p.Y.ModSub(p.X, Q);
        var d = q.Y.ModSub(q.X, Q);
        var e = a.MultiplyMod(b, Q);
        var f = c.MultiplyMod(d, Q);

        // Precompute factor for x3 and y3
        var factor = D.MultiplyMod(e.MultiplyMod(f, Q), Q);
        var inv1 = Inv(factor.ModAdd(BigInteger.One, Q));
        var inv2 = Inv(BigInteger.One.ModSub(factor, Q));

        var x3 = e.ModSub(f, Q).MultiplyMod(inv1, Q);
        var y3 = e.ModAdd(f, Q).MultiplyMod(inv2, Q);
        return new Point(x3, y3);
    }

    /// <summary>
    /// Performs scalar multiplication on a point using the double-and-add algorithm.
    /// </summary>
    /// <param name="p">The point to multiply.</param>
    /// <param name="e">The scalar to multiply the point by.</param>
    /// <returns>The resulting point from the scalar multiplication.</returns>
    private static Point ScalarMul(Point p, BigInteger e)
    {
        Point result = new(BigInteger.Zero, BigInteger.One);
        Point current = p;
        while (e > 0)
        {
            if (!e.IsEven)
                result = Edwards(result, current);
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
    private static BigInteger ClampScalar(ReadOnlySpan<byte> s)
    {
        // Create a 32-byte buffer to modify bits as needed
        Span<byte> scalarBytes = stackalloc byte[32];
        s.CopyTo(scalarBytes);
        // Clear/Set bits: clear lowest 3 bits, clear highest bit, set second highest bit
        scalarBytes[0] &= 0xF8;
        scalarBytes[31] &= 0x7F;
        scalarBytes[31] |= 0x40;
        return new BigInteger(scalarBytes, isUnsigned: true, isBigEndian: true) % L;
    }

    /// <summary>
    /// Hashes data into a scalar value modulo L.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The scalar result of hashing the data.</returns>
    private static BigInteger HashToScalar(ReadOnlySpan<byte> data)
        => new BigInteger(ComputeHash(data), isUnsigned: true, isBigEndian: true) % L;

    private static BigInteger HashToScalar(ReadOnlySpan<byte> prefix, byte[] message)
    {
        Span<byte> buffer = message.Length <= 1024
            ? stackalloc byte[prefix.Length + message.Length]
            : new byte[prefix.Length + message.Length];

        prefix.CopyTo(buffer);
        message.CopyTo(buffer[prefix.Length..]);
        return new BigInteger(ComputeHash(buffer), isUnsigned: true, isBigEndian: true) % L;
    }

    /// <summary>
    /// Encodes a point to a fixed 32-byte representation using TryWriteBytes to avoid allocations.
    /// </summary>
    /// <param name="p">The point to encode.</param>
    /// <param name="destination">The destination span to write the encoded point.</param>
    private static void EncodePoint(Point p, Span<byte> destination)
    {
        // Encode y coordinate as 32 bytes in big-endian order
        if (!p.Y.TryWriteBytes(destination, out _, isUnsigned: true, isBigEndian: true))
        {
            throw new InvalidOperationException("Failed to encode point y coordinate.");
        }
        // Set the most significant bit to indicate x's parity
        if (!p.X.IsEven)
            destination[^1] |= 0x80;
    }

    private static Point DecodePoint(ReadOnlySpan<byte> data)
    {
        // Decode y coordinate (32 bytes big-endian)
        var y = new BigInteger(data, isUnsigned: true, isBigEndian: true);
        var x = RecoverX(y);
        // Use the high bit of the last byte to recover x parity
        bool xParity = (data[^1] & 0x80) != 0;
        if (x.IsEven != !xParity)
            x = Q - x;
        return new Point(x, y);
    }

    private static BigInteger RecoverX(BigInteger y)
    {
        // Recover x using curve equation: x^2 = (y^2 - 1) / (D*y^2 + 1)
        var numerator = (y * y - BigInteger.One).Mod(Q);
        var denominator = (D * y * y + BigInteger.One).Mod(Q);
        var xx = numerator * Inv(denominator) % Q;
        var x = BigInteger.ModPow(xx, (Q + 3) / 8, Q);
        return x * x % Q == xx ? x : x * I % Q;
    }

    private static bool PointEquals(Point a, Point b)
        => a.X == b.X && a.Y == b.Y;

    /// <summary>
    /// Encodes a scalar to a fixed 32-byte representation.
    /// </summary>
    /// <param name="s">The scalar to encode.</param>
    /// <param name="destination">The destination span to write the encoded scalar.</param>
    private static void EncodeScalar(BigInteger s, Span<byte> destination)
    {
        if (!s.TryWriteBytes(destination, out int bytesWritten, isUnsigned: true, isBigEndian: true))
            throw new InvalidOperationException("Failed to encode scalar.");
        // If fewer than 32 bytes were written, pad the beginning with zeros.
        if (bytesWritten < destination.Length)
        {
            destination[..^bytesWritten].Clear();
        }
    }

    private static BigInteger DecodeScalar(ReadOnlySpan<byte> data)
        => new BigInteger(data, isUnsigned: true, isBigEndian: true) % L;

    // Precomputed constants

    private static readonly BigInteger Q = BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");
    private static readonly BigInteger L = BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");
    private static readonly BigInteger D = BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740");
    private static readonly BigInteger I = BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752");

    // Base point B
    private static readonly Point B = new(
        BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202").Mod(Q),
        BigInteger.Parse("46316835694926478169428394003475163141307993866256256256850187133169737347974").Mod(Q)
    );

    // Point struct (immutable)
    private readonly struct Point(BigInteger x, BigInteger y)
    {
        public readonly BigInteger X = x;
        public readonly BigInteger Y = y;
    }

    // Optimized SHA-512 with buffer reuse (thread-local instance)
    private static readonly ThreadLocal<SHA256> Sha512 = new();
}
